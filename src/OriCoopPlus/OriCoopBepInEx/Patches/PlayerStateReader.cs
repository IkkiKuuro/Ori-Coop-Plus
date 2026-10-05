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
            // TEMP-TRACER: grounded real ainda a confirmar via Assembly-CSharp;
            // heurística provisória até o plano 02.
            snapshot.Animation.IsGrounded = Mathf.Abs(speed.y) < 1.0f;
            snapshot.Animation.State = AnimationSyncData.DeriveState(
                speed.x, speed.y, snapshot.Animation.IsGrounded, s_lastDerivedState);
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
    }
}
