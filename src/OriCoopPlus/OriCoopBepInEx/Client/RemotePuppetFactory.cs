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
            instance.SetActive(true);
            UnityEngine.Object.DontDestroyOnLoad(instance);

            RemotePlayerPuppet puppet = instance.GetComponent<RemotePlayerPuppet>() ?? instance.AddComponent<RemotePlayerPuppet>();
            puppet.Setup(playerId, nickname);

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

            string[] componentsToDestroy = new string[]
            {
                "SeinCharacter",
                "SeinController",
                "SeinInput",
                "SeinMovement",
                "SeinCutsceneBlocked",
                "SeinCutsceneMovement",
                "SeinMortality",
                "SeinEnergy",
                "SeinInventory",
                "SeinPickupProcessor",
                "PlatformBehaviour",
                "Rigidbody",
                "CharacterAnimationSystem"
            };

            Component[] allComps = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < allComps.Length; i++)
            {
                Component c = allComps[i];
                if (c == null)
                {
                    continue;
                }

                string name = c.GetType().Name;
                for (int j = 0; j < componentsToDestroy.Length; j++)
                {
                    if (name == componentsToDestroy[j])
                    {
                        UnityEngine.Object.DestroyImmediate(c);
                        break;
                    }
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].isTrigger = true;
            }
        }
    }
}
