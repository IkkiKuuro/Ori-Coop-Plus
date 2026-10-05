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

            SeinCharacter originalSein = Game.Characters.Sein;
            ICharacter originalCurrent = Game.Characters.Current;

            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(s_templatePrefab, initialPosition, Quaternion.identity) as GameObject;
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
            finally
            {
                if (originalSein != null && Game.Characters.Sein != originalSein)
                {
                    Game.Characters.Sein = originalSein;
                }
                if (originalCurrent != null && Game.Characters.Current != originalCurrent)
                {
                    Game.Characters.Current = originalCurrent;
                }
                Plugin.OriCoopPlugin.EnsureCameraFollowsLocalPlayer();
            }
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

            try
            {
                // Temporarily deactivate source object so clone is instantiated inactive,
                // preventing Awake() or OnEnable() from running on any components during cloning.
                bool wasActive = sein.activeSelf;
                sein.SetActive(false);
                try
                {
                    s_templatePrefab = UnityEngine.Object.Instantiate(sein) as GameObject;
                }
                finally
                {
                    sein.SetActive(wasActive);
                }

                if (s_templatePrefab == null)
                {
                    return;
                }

                s_templatePrefab.name = "RemotePlayer_Template";
                s_templatePrefab.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(s_templatePrefab);

                CleanPuppetComponents(s_templatePrefab);
            }
            finally
            {
                if (originalSein != null)
                {
                    Game.Characters.Sein = originalSein;
                }
                if (originalCurrent != null)
                {
                    Game.Characters.Current = originalCurrent;
                }
                Plugin.OriCoopPlugin.EnsureCameraFollowsLocalPlayer();
            }
        }

        private static void CleanPuppetComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            RemoteVisualController.StripFrustumOptimizers(root);

            // 1. Immediately disable all Behaviours so no unneeded script can ever run Update/FixedUpdate
            Behaviour[] allBehaviours = root.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                Behaviour b = allBehaviours[i];
                if (b == null) continue;
                if (b is SpriteAnimatorWithTransitions ||
                    b is CharacterSpriteMirror ||
                    b is RemotePlayerPuppet ||
                    b is RemoteVisualController)
                {
                    continue;
                }
                b.enabled = false;
            }

            // 2. Destroy child GameObjects that contain no renderers or animators
            // (e.g. gameplay triggers, audio emitters, leaf particle emitters, nested skill prefabs)
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform t = children[i];
                if (t == null || t == root.transform)
                {
                    continue;
                }

                Renderer r = t.GetComponentInChildren<Renderer>();
                SpriteAnimatorWithTransitions anim = t.GetComponentInChildren<SpriteAnimatorWithTransitions>();
                if (r == null && anim == null)
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            }

            // 3. Destroy all Colliders so remote puppets do not trigger scene events, triggers, or physics
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(colliders[i]);
                }
            }

            // 4. Destroy all Rigidbodies
            Rigidbody[] rbs = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(rbs[i]);
                }
            }

            // 5. Destroy all AudioSources and AudioListeners
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

            // 6. Destroy all non-whitelisted MonoBehaviours FIRST (to release RequireComponent dependencies)
            MonoBehaviour[] allScripts = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < allScripts.Length; i++)
            {
                MonoBehaviour mb = allScripts[i];
                if (mb == null) continue;
                if (mb is SpriteAnimatorWithTransitions ||
                    mb is CharacterSpriteMirror ||
                    mb is RemotePlayerPuppet ||
                    mb is RemoteVisualController)
                {
                    continue;
                }

                try
                {
                    UnityEngine.Object.DestroyImmediate(mb);
                }
                catch { }
            }

            // 7. Multi-pass destruction of remaining non-whitelisted components
            for (int pass = 0; pass < 3; pass++)
            {
                Component[] remaining = root.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < remaining.Length; i++)
                {
                    Component c = remaining[i];
                    if (c == null) continue;

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

                    try
                    {
                        UnityEngine.Object.DestroyImmediate(c);
                    }
                    catch { }
                }
            }
        }
    }
}
