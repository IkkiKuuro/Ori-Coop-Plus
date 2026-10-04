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
        private static readonly Dictionary<ActionVisualState, TextureAnimationWithTransitions> s_fallbackStates =
            new Dictionary<ActionVisualState, TextureAnimationWithTransitions>();

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

                string lower = clip.name.ToLower();
                if (lower.Contains("idle") && !s_fallbackStates.ContainsKey(ActionVisualState.Idle))
                {
                    s_fallbackStates[ActionVisualState.Idle] = clip;
                }
                else if (lower.Contains("run") && !s_fallbackStates.ContainsKey(ActionVisualState.Running))
                {
                    s_fallbackStates[ActionVisualState.Running] = clip;
                }
                else if (lower.Contains("jump") && !lower.Contains("double") && !s_fallbackStates.ContainsKey(ActionVisualState.Jump))
                {
                    s_fallbackStates[ActionVisualState.Jump] = clip;
                }
                else if (lower.Contains("doublejump") && !s_fallbackStates.ContainsKey(ActionVisualState.DoubleJump))
                {
                    s_fallbackStates[ActionVisualState.DoubleJump] = clip;
                }
                else if (lower.Contains("fall") && !s_fallbackStates.ContainsKey(ActionVisualState.Falling))
                {
                    s_fallbackStates[ActionVisualState.Falling] = clip;
                }
                else if (lower.Contains("wallslide") && !s_fallbackStates.ContainsKey(ActionVisualState.WallSlide))
                {
                    s_fallbackStates[ActionVisualState.WallSlide] = clip;
                }
                else if (lower.Contains("bash") && !s_fallbackStates.ContainsKey(ActionVisualState.Bash))
                {
                    s_fallbackStates[ActionVisualState.Bash] = clip;
                }
            }
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

            if (s_fallbackStates.TryGetValue(fallbackState, out result))
            {
                return result;
            }

            if (s_fallbackStates.TryGetValue(ActionVisualState.Idle, out result))
            {
                return result;
            }

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
