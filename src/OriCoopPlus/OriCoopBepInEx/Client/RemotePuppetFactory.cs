using System;
using Game;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    public static class RemotePuppetFactory
    {
        private static GameObject s_templatePrefab;

        public static RemotePlayerPuppet CreatePuppet(int playerId, string nickname, Vector3 initialPosition)
        {
            EnsureTemplate();

            if (s_templatePrefab == null)
            {
                Debug.LogWarning("[OriCoop] Template prefab could not be initialized from Game.Characters.Sein.");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(s_templatePrefab, initialPosition, Quaternion.identity) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = string.Format("RemotePlayer_{0}_{1}", playerId, nickname);
            RemotePlayerPuppet puppet = instance.GetComponent<RemotePlayerPuppet>() ?? instance.AddComponent<RemotePlayerPuppet>();
            puppet.Setup(playerId, nickname);

            try
            {
                instance.SetActive(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OriCoop] Non-fatal exception activating puppet: " + ex.Message);
            }

            UnityEngine.Object.DontDestroyOnLoad(instance);
            return puppet;
        }

        private static void EnsureTemplate()
        {
            if (s_templatePrefab != null)
            {
                return;
            }

            GameObject sein = null;
            if (Game.Characters.Sein != null)
            {
                sein = Game.Characters.Sein.gameObject;
            }
            if (sein == null)
            {
                sein = GameObject.Find("Characters/Sein") ?? GameObject.Find("Sein");
            }
            if (sein == null)
            {
                return;
            }

            s_templatePrefab = UnityEngine.Object.Instantiate(sein) as GameObject;
            if (s_templatePrefab == null)
            {
                return;
            }

            s_templatePrefab.name = "RemotePlayer_Template";
            s_templatePrefab.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(s_templatePrefab);

            CleanPuppetComponents(s_templatePrefab);
        }

        private static void CleanPuppetComponents(GameObject root)
        {
            RemoteVisualController.StripFrustumOptimizers(root);

            // Destroy child GameObjects that represent gameplay hints, UI meters, or nested skill prefabs
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform t = children[i];
                if (t == null || t == root.transform)
                {
                    continue;
                }

                string name = t.gameObject.name;
                if (name.Contains("bentBar") ||
                    name.Contains("radialEnemyHighlight") ||
                    name.Contains("PlayerGrab") ||
                    name.Contains("Hint") ||
                    name.Contains("Skill") ||
                    name.Contains("mistErase") ||
                    name.Contains("lightTrail"))
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            }

            // Strip ALL components that are not essential visual / animation / puppet controllers
            Component[] allComps = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < allComps.Length; i++)
            {
                Component c = allComps[i];
                if (c == null)
                {
                    continue;
                }

                if (c is Transform ||
                    c is Renderer ||
                    c is MeshFilter ||
                    c is SpriteAnimatorWithTransitions ||
                    c is CharacterSpriteMirror ||
                    c is RemotePlayerPuppet ||
                    c is RemoteVisualController)
                {
                    continue;
                }

                // Destroys all Sein*, Character*, Platform*, Rigidbody, Colliders, etc.
                UnityEngine.Object.DestroyImmediate(c);
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].isTrigger = true;
            }
        }
    }
}
