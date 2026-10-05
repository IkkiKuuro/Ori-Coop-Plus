using System;
using System.Collections.Generic;
using OriCoop;
using UnityEngine;

namespace OriCoopBepInEx.Client
{
    public static class AnimationRegistry
    {
        private static readonly Dictionary<uint, TextureAnimationWithTransitions> s_animHashCache =
            new Dictionary<uint, TextureAnimationWithTransitions>();
        private static readonly Dictionary<string, TextureAnimationWithTransitions> s_animNameCache =
            new Dictionary<string, TextureAnimationWithTransitions>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ActionVisualState, TextureAnimationWithTransitions> s_stateClips =
            new Dictionary<ActionVisualState, TextureAnimationWithTransitions>();

        // SEED: aliases best-effort — calibrar nomes exatos via dump F8
        // (plano 02). Miss aqui nunca vira sprite aleatório: resolve
        // desconhecido retorna null e o puppet mantém a última anim.
        private static readonly Dictionary<string, ActionVisualState> s_nameToState =
            new Dictionary<string, ActionVisualState>(StringComparer.OrdinalIgnoreCase)
            {
                { "idle", ActionVisualState.Idle },
                { "oriidle", ActionVisualState.Idle },
                { "seinidle", ActionVisualState.Idle },
                { "stand", ActionVisualState.Idle },
                { "oristand", ActionVisualState.Idle },
                { "run", ActionVisualState.Running },
                { "running", ActionVisualState.Running },
                { "orirun", ActionVisualState.Running },
                { "seinrun", ActionVisualState.Running },
                { "jump", ActionVisualState.Jump },
                { "orijump", ActionVisualState.Jump },
                { "seinjump", ActionVisualState.Jump },
                { "jumpup", ActionVisualState.Jump },
                { "rise", ActionVisualState.Jump },
                { "doublejump", ActionVisualState.DoubleJump },
                { "oridoublejump", ActionVisualState.DoubleJump },
                { "flip", ActionVisualState.DoubleJump },
                { "fall", ActionVisualState.Falling },
                { "falling", ActionVisualState.Falling },
                { "orifall", ActionVisualState.Falling },
                { "seinfall", ActionVisualState.Falling },
                { "drop", ActionVisualState.Falling },
                { "wallslide", ActionVisualState.WallSlide },
                { "oriwallslide", ActionVisualState.WallSlide },
                { "slide", ActionVisualState.WallSlide },
                { "walljump", ActionVisualState.WallJump },
                { "oriwalljump", ActionVisualState.WallJump },
                { "bash", ActionVisualState.Bash },
                { "oribash", ActionVisualState.Bash },
                { "seinbash", ActionVisualState.Bash },
                { "glide", ActionVisualState.Glide },
                { "origlide", ActionVisualState.Glide },
                { "seinglide", ActionVisualState.Glide },
                { "feather", ActionVisualState.Glide },
                { "slowfall", ActionVisualState.Glide },
                { "chargejump", ActionVisualState.ChargeJump },
                { "orichargejump", ActionVisualState.ChargeJump },
                { "charge", ActionVisualState.ChargeJump },
                { "stomp", ActionVisualState.Stomp },
                { "oristomp", ActionVisualState.Stomp },
                { "seinstomp", ActionVisualState.Stomp },
                { "groundpound", ActionVisualState.Stomp },
                { "dash", ActionVisualState.Dash },
                { "oridash", ActionVisualState.Dash },
                { "seindash", ActionVisualState.Dash },
                { "airdash", ActionVisualState.Dash },
                { "chargedash", ActionVisualState.Dash },
            };

        public static bool IsPrewarmed { get; private set; }

        public static void Prewarm()
        {
            if (IsPrewarmed)
            {
                return;
            }

            TextureAnimationWithTransitions[] allClips = Resources.FindObjectsOfTypeAll<TextureAnimationWithTransitions>();
            if (allClips != null && allClips.Length > 0)
            {
                RegisterClips(allClips);
                IsPrewarmed = true;
                Debug.Log(string.Format("[OriCoop] AnimationRegistry pré-aquecido: {0} clipes catalogados.", s_animNameCache.Count));
            }
        }

        public static void RegisterClips(IEnumerable<TextureAnimationWithTransitions> clips)
        {
            if (clips == null)
            {
                return;
            }

            foreach (TextureAnimationWithTransitions clip in clips)
            {
                if (clip == null || string.IsNullOrEmpty(clip.name))
                {
                    continue;
                }

                uint hash = AnimationSyncData.ComputeFnv1aHash(clip.name);
                s_animHashCache[hash] = clip;
                s_animNameCache[clip.name] = clip;

                ActionVisualState mappedState;
                if (s_nameToState.TryGetValue(clip.name, out mappedState)
                    && !s_stateClips.ContainsKey(mappedState))
                {
                    s_stateClips[mappedState] = clip;
                }
            }
        }

        public static void DumpCatalog()
        {
            foreach (KeyValuePair<string, TextureAnimationWithTransitions> entry in s_animNameCache)
            {
                ActionVisualState mapped;
                string stateText = s_nameToState.TryGetValue(entry.Key, out mapped)
                    ? mapped.ToString() : "UNKNOWN";
                Debug.Log(string.Format("[ANIM-DUMP] name={0} hash=0x{1:X8} estado?={2}",
                    entry.Key, AnimationSyncData.ComputeFnv1aHash(entry.Key), stateText));
            }
            Debug.Log(string.Format("[OriCoop] ANIM-DUMP concluído: {0} clipes.", s_animNameCache.Count));
        }

        public static TextureAnimationWithTransitions Resolve(string animName, uint animHash, ActionVisualState fallbackState)
        {
            TextureAnimationWithTransitions result;

            if (animHash != 0 && s_animHashCache.TryGetValue(animHash, out result))
            {
                return result;
            }

            if (!string.IsNullOrEmpty(animName) && s_animNameCache.TryGetValue(animName, out result))
            {
                return result;
            }

            if (s_stateClips.TryGetValue(fallbackState, out result))
            {
                return result;
            }

            // Desconhecido: retorna null e o chamador mantém a última anim
            // (fail-closed). Sem chute para Idle genérico.
            return null;
        }

        public static ActionVisualState InferStateFromMovement(Vector3 velocity, bool isGrounded)
        {
            if (!isGrounded)
            {
                if (velocity.y < -1.0f)
                {
                    return ActionVisualState.Falling;
                }
                if (velocity.y > 1.0f)
                {
                    return ActionVisualState.Jump;
                }
            }

            float horizontalSpeed = Mathf.Abs(velocity.x);
            if (horizontalSpeed > 0.4f)
            {
                return ActionVisualState.Running;
            }

            return ActionVisualState.Idle;
        }
    }
}
