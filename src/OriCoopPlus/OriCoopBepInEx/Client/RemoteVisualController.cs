using System;
using System.Collections.Generic;
using OriCoopBepInEx.Diagnostics;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    public sealed class RemoteVisualController : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material> _materials = new List<Material>();
        private bool _isInitialized;
        private int _playerId;

        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private float _lastWatchdogTime;

        public void InitializeHierarchy(int playerId)
        {
            _playerId = playerId;
            StripFrustumOptimizers(gameObject);

            _renderers.Clear();
            _materials.Clear();

            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer r = allRenderers[i];
                if (r == null || r is ParticleSystemRenderer ||
                    r.name.Contains("NameTag") || r.name.Contains("Shadow") ||
                    r.name.Contains("bentBar") || r.name.Contains("radialEnemyHighlight") ||
                    r.name.Contains("ghostTrail") || r.name.Contains("mistErase") ||
                    r.name.Contains("CoopNameTag"))
                {
                    continue;
                }

                _renderers.Add(r);
                if (r.sharedMaterial != null)
                {
                    _materials.Add(r.sharedMaterial);
                }
            }

            _isInitialized = true;
            EnforceVisibility("InitializeHierarchy");
        }

        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                return;
            }

            // Throttle watchdog check to 2x per second to prevent per-frame CPU overhead
            if (Time.time - _lastWatchdogTime < 0.5f)
            {
                return;
            }
            _lastWatchdogTime = Time.time;

            EnforceVisibility("LateUpdate-Watchdog");
        }

        public void EnforceVisibility(string callerContext)
        {
            bool isInit = string.Equals(callerContext, "InitializeHierarchy", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (!r.enabled)
                {
                    r.enabled = true;
                    if (isInit)
                    {
                        ReplicationObservability.LogVisibilityEvent(_playerId, string.Format("Renderer '{0}' reenabled by [{1}]", r.name, callerContext), true);
                    }
                }

                if (!r.gameObject.activeSelf)
                {
                    r.gameObject.SetActive(true);
                    if (isInit)
                    {
                        ReplicationObservability.LogVisibilityEvent(_playerId, string.Format("GameObject '{0}' reenabled by [{1}]", r.gameObject.name, callerContext), true);
                    }
                }
            }

            for (int i = 0; i < _materials.Count; i++)
            {
                Material mat = _materials[i];
                if (mat != null && mat.HasProperty(ColorPropId))
                {
                    Color c = mat.color;
                    if (c.a < 0.95f)
                    {
                        c.a = 1.0f;
                        mat.color = c;
                    }
                }
            }
        }

        public static void StripFrustumOptimizers(GameObject root)
        {
            string[] cullingComponents = new string[]
            {
                "CameraFrustumOptimizer",
                "MeshRendererFrustrumOptimiser",
                "DisableRendererWhenOutOfFrustrum",
                "DisableGameObjectWhenOutOfFrustrum",
                "SuspendWhenOutOfFrustrum",
                "CharacterAnimationSystem"
            };

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component comp = components[i];
                if (comp == null)
                {
                    continue;
                }

                string typeName = comp.GetType().Name;
                for (int j = 0; j < cullingComponents.Length; j++)
                {
                    if (typeName == cullingComponents[j])
                    {
                        DestroyImmediate(comp);
                        break;
                    }
                }
            }
        }
    }
}
