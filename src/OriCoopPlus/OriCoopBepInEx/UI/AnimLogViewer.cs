using System;
using System.IO;
using OriCoopBepInEx.Diagnostics;
using UnityEngine;

namespace OriCoopBepInEx.UI
{
    public sealed class AnimLogViewer : MonoBehaviour
    {
        private static readonly string[] TabNames = new string[] { "Logs do mod", "BepInEx" };

        private bool _isOpen;
        private int _tab;
        private Vector2 _scroll;
        private Rect _window = new Rect(60f, 60f, 640f, 420f);
        private string[] _bepInLines = new string[0];

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _isOpen = !_isOpen;
                if (_isOpen && _tab == 1)
                {
                    RefreshBepIn();
                }
            }
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                _isOpen = false;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen)
            {
                return;
            }
            _window = GUI.Window(424242, _window, DrawWindow, "Ori Coop — Logs");
        }

        private void DrawWindow(int windowId)
        {
            int newTab = GUILayout.Toolbar(_tab, TabNames);
            if (newTab != _tab)
            {
                _tab = newTab;
                if (_tab == 1)
                {
                    RefreshBepIn();
                }
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(330f));
            if (_tab == 0)
            {
                string[] lines = ReplicationObservability.Snapshot();
                if (lines.Length == 0)
                {
                    GUILayout.Label("Sem transicoes registradas ainda (ligue Diagnostics/AnimVerbose para [ANIM]).");
                }
                for (int i = 0; i < lines.Length; i++)
                {
                    GUILayout.Label(lines[i]);
                }
            }
            else
            {
                for (int i = 0; i < _bepInLines.Length; i++)
                {
                    GUILayout.Label(_bepInLines[i]);
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Fechar"))
            {
                _isOpen = false;
            }
            GUI.DragWindow();
        }

        private void RefreshBepIn()
        {
            try
            {
                string gameDir = Path.GetDirectoryName(Application.dataPath);
                string logPath = Path.Combine(gameDir, "BepInEx/LogOutput.log");
                string[] all = File.ReadAllLines(logPath);
                int skip = Math.Max(0, all.Length - 200);
                string[] tail = new string[all.Length - skip];
                Array.Copy(all, skip, tail, 0, tail.Length);
                _bepInLines = tail;
            }
            catch (Exception)
            {
                _bepInLines = new string[] { "Log do BepInEx indisponivel." };
            }
        }
    }
}
