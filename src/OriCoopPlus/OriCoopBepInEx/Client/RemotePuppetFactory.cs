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

            SeinCharacter originalSein = Game.Characters.Sein;
            ICharacter originalCurrent = Game.Characters.Current;

            bool wasActive = sein.activeSelf;
            if (wasActive)
            {
                sein.SetActive(false);
            }

            s_templatePrefab = UnityEngine.Object.Instantiate(sein) as GameObject;

            if (wasActive)
            {
                sein.SetActive(true);
            }

            if (s_templatePrefab == null)
            {
                return;
            }

            s_templatePrefab.name = "RemotePlayer_Template";
            s_templatePrefab.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(s_templatePrefab);

            // Restore global singleton references immediately
            if (originalSein != null)
            {
                Game.Characters.Sein = originalSein;
            }
            if (originalCurrent != null)
            {
                Game.Characters.Current = originalCurrent;
            }

            CleanPuppetComponents(s_templatePrefab);

            // Re-restore singleton references after cleaning
            if (originalSein != null)
            {
                Game.Characters.Sein = originalSein;
            }
            if (originalCurrent != null)
            {
                Game.Characters.Current = originalCurrent;
            }
        }

        private static void CleanPuppetComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            RemoteVisualController.StripFrustumOptimizers(root);

            // 1. Destroy child GameObjects that represent gameplay hints, UI meters, or nested skill prefabs
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

            // 2. Destroy all Colliders so remote puppets do not trigger scene events, triggers, or physics
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(colliders[i]);
                }
            }

            // 3. Destroy all Rigidbodies
            Rigidbody[] rbs = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(rbs[i]);
                }
            }

            // 4. Destroy all AudioSources and AudioListeners
            AudioSource[] audioSources = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(audioSources[i]);
                }
            }
            AudioListener[] audioListeners = root.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < audioListeners.Length; i++)
            {
                if (audioListeners[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(audioListeners[i]);
                }
            }

            // 5. Multi-pass whitelist cleanup: destroy all MonoBehaviours except the animation and puppet components
            for (int pass = 0; pass < 5; pass++)
            {
                MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                int destroyed = 0;

                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour mb = behaviours[i];
                    if (mb == null)
                    {
                        continue;
                    }

                    string typeName = mb.GetType().Name;
                    if (typeName == "SpriteAnimatorWithTransitions" ||
                        typeName == "CharacterSpriteMirror" ||
                        typeName == "RemotePlayerPuppet" ||
                        typeName == "RemoteVisualController")
                    {
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(mb);
                    destroyed++;
                }

                if (destroyed == 0)
                {
                    break;
                }
            }
        }
    }
}
