using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    public static class RemotePuppetFactory
    {
        private static bool s_seinClipsRegistered;

        public static RemotePlayerPuppet CreatePuppet(int playerId, string nickname, Vector3 initialPosition)
        {
            GameObject seinGo = FindSeinObject();
            if (seinGo == null)
            {
                Debug.LogWarning("[OriCoop] Sein não encontrado; puppet remoto não criado.");
                return null;
            }

            EnsureSeinClips(seinGo);

            // Puppet leve: em vez de clonar o Sein inteiro (raiz com
            // singletons Game.Characters.Sein/Current, câmera, física e
            // dezenas de scripts), instancia SÓ a subárvore visual que
            // contém o SpriteAnimatorWithTransitions. Nenhum Awake de
            // gameplay roda, nenhum singleton é tocado.
            SpriteAnimatorWithTransitions[] animators;
            try
            {
                animators = seinGo.GetComponentsInChildren<SpriteAnimatorWithTransitions>(true);
            }
            catch
            {
                animators = null;
            }
            if (animators == null || animators.Length == 0)
            {
                Debug.LogWarning("[OriCoop] Nenhum animator visual no Sein; puppet remoto não criado.");
                return null;
            }

            GameObject root = new GameObject(string.Format("RemotePlayer_{0}_{1}", playerId, nickname));
            root.transform.position = initialPosition;

            int visualsCopied = 0;
            for (int i = 0; i < animators.Length; i++)
            {
                SpriteAnimatorWithTransitions src = animators[i];
                if (src == null || src.gameObject == null)
                {
                    continue;
                }
                GameObject srcGo = src.gameObject;
                GameObject clone = null;
                try
                {
                    clone = UnityEngine.Object.Instantiate(srcGo) as GameObject;
                }
                catch
                {
                    clone = null;
                }
                if (clone == null)
                {
                    continue;
                }
                clone.name = srcGo.name;
                try
                {
                    clone.transform.SetParent(root.transform, false);
                    clone.transform.localPosition = srcGo.transform.localPosition;
                    clone.transform.localRotation = srcGo.transform.localRotation;
                    clone.transform.localScale = srcGo.transform.localScale;
                }
                catch { }
                CleanPuppetComponents(clone);
                visualsCopied++;
            }

            if (visualsCopied == 0)
            {
                try { UnityEngine.Object.Destroy(root); }
                catch { }
                return null;
            }

            // Espelho de sprite: vem junto se estava na subárvore visual;
            // senão cria um novo copiando o FaceLeft atual.
            try
            {
                if (root.GetComponentInChildren<CharacterSpriteMirror>() == null)
                {
                    CharacterSpriteMirror srcMirror = seinGo.GetComponentInChildren<CharacterSpriteMirror>();
                    CharacterSpriteMirror mirror = root.AddComponent<CharacterSpriteMirror>();
                    if (srcMirror != null && mirror != null)
                    {
                        try { mirror.FaceLeft = srcMirror.FaceLeft; }
                        catch { }
                    }
                }
            }
            catch { }

            RemoteVisualController.StripFrustumOptimizers(root);
            RemoteVisualController.StripExtraLights(root);

            RemotePlayerPuppet puppet = null;
            try
            {
                puppet = root.AddComponent<RemotePlayerPuppet>();
                puppet.Setup(playerId, nickname);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OriCoop] Falha ao montar puppet leve: " + ex.Message);
                try { UnityEngine.Object.Destroy(root); }
                catch { }
                return null;
            }

            try
            {
                root.SetActive(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OriCoop] Non-fatal exception activating puppet: " + ex.Message);
            }

            UnityEngine.Object.DontDestroyOnLoad(root);
            Plugin.OriCoopPlugin.EnsureCameraFollowsLocalPlayer();
            return puppet;
        }

        private static GameObject FindSeinObject()
        {
            try
            {
                if (Game.Characters.Sein != null && Game.Characters.Sein.gameObject != null)
                {
                    return Game.Characters.Sein.gameObject;
                }
            }
            catch { }
            GameObject go = GameObject.Find("Characters/Sein");
            if (go == null)
            {
                go = GameObject.Find("Sein");
            }
            return go;
        }

        private static void EnsureSeinClips(GameObject seinGo)
        {
            // Refresh idempotente (so adiciona): o Sein pode nao estar
            // totalmente inicializado no primeiro puppet, entao re-coleta
            // sempre em vez de travar na primeira coleta parcial.
            try
            {
                int added = AnimationRegistry.RefreshFromSein();
                if (added > 0 || !s_seinClipsRegistered)
                {
                    s_seinClipsRegistered = true;
                    Debug.Log(string.Format("[OriCoop] Sein clips atualizados (+{0}).", added));
                }
            }
            catch { }
        }

        private static void CleanPuppetComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            RemoteVisualController.StripFrustumOptimizers(root);
            RemoteVisualController.StripExtraLights(root);

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
                // CharacterAnimationSystem e mantido (desligado) para nao quebrar o
                // SpriteAnimator; destrui-lo congelava animacoes (bug #2).
                if (b.GetType().Name == "CharacterAnimationSystem")
                {
                    try { b.enabled = false; } catch { }
                    continue;
                }
                // Lights ja removidas em StripExtraLights; garante resto desligado.
                if (b is Light)
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
                // Preserva o driver de animacao (desligado acima); sem ele o
                // SpriteAnimator nao troca de clipe (bug #2).
                if (mb.GetType().Name == "CharacterAnimationSystem")
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
                    // Lights/halos/flares ja tratados; garante remocao se sobrou algum.
                    if (c is Light)
                    {
                        try { UnityEngine.Object.DestroyImmediate(c); } catch { }
                        continue;
                    }
                    string tn = c.GetType().Name;
                    if (tn == "CharacterAnimationSystem" || tn == "Halo" ||
                        tn == "LensFlare" || tn == "FlareLayer" || tn == "Projector")
                    {
                        if (tn == "CharacterAnimationSystem")
                        {
                            continue;
                        }
                        try { UnityEngine.Object.DestroyImmediate(c); } catch { }
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
