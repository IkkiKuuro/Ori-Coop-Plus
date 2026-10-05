using System;
using OriCoop;
using OriCoopBepInEx.Domain;
using UnityEngine;

namespace OriCoopBepInEx.Patches
{
    internal static class PlayerStateReader
    {
        private static ActionVisualState s_lastDerivedState = ActionVisualState.Idle;

        public static PlayerSnapshot Read(object seinCharacterInstance)
        {
            SeinCharacter sein = seinCharacterInstance as SeinCharacter;
            if (sein == null)
            {
                Component component = seinCharacterInstance as Component;
                if (component == null)
                {
                    return null;
                }

                PlayerSnapshot fallbackSnapshot = new PlayerSnapshot();
                Vector3 p = component.transform.position;
                fallbackSnapshot.Position = new Vector3Data(p.x, p.y, p.z);
                fallbackSnapshot.Velocity = new Vector2Data(0f, 0f);
                fallbackSnapshot.Timestamp = DateTime.UtcNow.Ticks;
                return fallbackSnapshot;
            }

            Vector3 pos = sein.transform.position;
            Vector3 speed = sein.Speed;

            PlayerSnapshot snapshot = new PlayerSnapshot();
            snapshot.Position = new Vector3Data(pos.x, pos.y, pos.z);
            snapshot.Velocity = new Vector2Data(speed.x, speed.y);
            snapshot.Animation.FacingLeft = sein.FaceLeft;
            snapshot.Animation.Name = ReadCurrentAnimationName(sein);
            snapshot.Animation.IsGrounded = ReadGrounded(sein, speed);
            snapshot.Animation.State = DeriveFullState(sein, speed, snapshot.Animation.IsGrounded, snapshot.Animation.Name);
            s_lastDerivedState = snapshot.Animation.State;
            snapshot.Animation.AnimNameHash = AnimationSyncData.ComputeFnv1aHash(
                snapshot.Animation.Name);
            snapshot.Timestamp = DateTime.UtcNow.Ticks;

            return snapshot;
        }

        private static string ReadCurrentAnimationName(SeinCharacter sein)
        {
            if (sein.Animation != null && sein.Animation.Animator != null)
            {
                TextureAnimation current = sein.Animation.Animator.CurrentAnimation;
                if (current != null)
                {
                    return current.name;
                }
            }
            return string.Empty;
        }

        private static bool ReadGrounded(SeinCharacter sein, Vector3 speed)
        {
            try
            {
                if (sein.IsOnGround)
                {
                    return true;
                }
            }
            catch { }
            return Mathf.Abs(speed.y) < 1.0f;
        }

        private static ActionVisualState DeriveFullState(SeinCharacter sein, Vector3 speed, bool grounded, string animName)
        {
            // 1. Habilidades reais do controller têm prioridade sobre velocidade.
            // Sem isso Bash/Dash/Glide/Stomp/ChargeJump nunca eram derivados
            // (DeriveState só conhecia Idle/Run/Jump/Fall) e o puppet caía
            // no fallback errado — parte das "animações que não aparecem".
            try
            {
                if (sein.Controller != null)
                {
                    if (sein.Controller.IsBashing)
                    {
                        return ActionVisualState.Bash;
                    }
                    if (sein.Controller.IsStomping)
                    {
                        return ActionVisualState.Stomp;
                    }
                    if (sein.Controller.IsDashing)
                    {
                        return ActionVisualState.Dash;
                    }
                    if (sein.Controller.IsGliding)
                    {
                        return ActionVisualState.Glide;
                    }
                    if (sein.Controller.IsChargingJump)
                    {
                        return ActionVisualState.ChargeJump;
                    }
                    if (sein.Controller.IsGrabbingWall)
                    {
                        return ActionVisualState.WallSlide;
                    }
                }
            }
            catch { }

            // 2. Nome do clipe local é autoritativo: cobre DoubleJump/WallJump/
            // ChargeJump mesmo quando a física ainda não denuncia o estado.
            ActionVisualState fromName;
            if (TryDeriveFromName(animName, out fromName))
            {
                // Nome manda para estados especiais; para Idle/Run/Jump/Fall
                // deixa a velocidade decidir (evita Idle congelado no ar).
                if (fromName == ActionVisualState.Bash
                    || fromName == ActionVisualState.Dash
                    || fromName == ActionVisualState.Glide
                    || fromName == ActionVisualState.Stomp
                    || fromName == ActionVisualState.ChargeJump
                    || fromName == ActionVisualState.DoubleJump
                    || fromName == ActionVisualState.WallSlide
                    || fromName == ActionVisualState.WallJump)
                {
                    return fromName;
                }
            }

            return AnimationSyncData.DeriveState(speed.x, speed.y, grounded, s_lastDerivedState);
        }

        private static bool TryDeriveFromName(string animName, out ActionVisualState state)
        {
            state = ActionVisualState.Idle;
            if (string.IsNullOrEmpty(animName))
            {
                return false;
            }
            string lower = animName.ToLowerInvariant();
            if (lower.Contains("doublejump") || lower.Contains("backflip") || (lower.Contains("flip") && lower.Contains("jump")))
            {
                state = ActionVisualState.DoubleJump;
                return true;
            }
            if (lower.Contains("walljump"))
            {
                state = ActionVisualState.WallJump;
                return true;
            }
            if (lower.Contains("wallslide") || lower.Contains("wall_slide") || lower.Contains("grabwall") || lower.Contains("slideup") || lower.Contains("slidedown"))
            {
                state = ActionVisualState.WallSlide;
                return true;
            }
            if (lower.Contains("bash"))
            {
                state = ActionVisualState.Bash;
                return true;
            }
            if (lower.Contains("glide") || lower.Contains("feather") || lower.Contains("parachute"))
            {
                state = ActionVisualState.Glide;
                return true;
            }
            if (lower.Contains("chargejump") || lower.Contains("charge_jump") || lower.Contains("superjump"))
            {
                state = ActionVisualState.ChargeJump;
                return true;
            }
            if (lower.Contains("stomp") || lower.Contains("groundpound"))
            {
                state = ActionVisualState.Stomp;
                return true;
            }
            if (lower.Contains("chargedash") || (lower.Contains("dash") && !lower.Contains("dashboard")))
            {
                state = ActionVisualState.Dash;
                return true;
            }
            return false;
        }
    }
}
