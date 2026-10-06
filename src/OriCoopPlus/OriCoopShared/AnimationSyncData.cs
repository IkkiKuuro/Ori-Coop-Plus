using System;

namespace OriCoop
{
    public enum ActionVisualState : byte
    {
        Idle = 0,
        Running = 1,
        Jump = 2,
        DoubleJump = 3,
        Falling = 4,
        WallSlide = 5,
        WallJump = 6,
        Bash = 7,
        Glide = 8,
        ChargeJump = 9,
        Stomp = 10,
        Dash = 11,
        // Novos estados (coop): o puppet remoto precisa de acesso a TODAS as
        // animacoes do Sein, nao so locomocao. Append-only para nao quebrar
        // o protocolo (State viaja como byte opaco).
        Swim = 12,
        Carry = 13,
        GrabWall = 14,
        GrabBlock = 15,
        Hurt = 16,
        Crouch = 17,
        LookUp = 18,
        AimThrow = 19,
        PushAgainstWall = 20,
        EdgeClamber = 21,
        Lever = 22,
        StandingOnEdge = 23
    }

    public struct AnimationSyncData
    {
        public ActionVisualState State;
        public byte Flags; // Bit 0: FacingLeft, Bit 1: IsGrounded
        public uint AnimNameHash;
        public float SpeedX;
        public float SpeedY;

        public bool FacingLeft
        {
            get { return (Flags & 1) != 0; }
            set { Flags = value ? (byte)(Flags | 1) : (byte)(Flags & ~1); }
        }

        public bool IsGrounded
        {
            get { return (Flags & 2) != 0; }
            set { Flags = value ? (byte)(Flags | 2) : (byte)(Flags & ~2); }
        }

        public static uint ComputeFnv1aHash(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash = (hash ^ (uint)text[i]) * 16777619u;
            }
            return hash;
        }

        public const float RunEnterSpeed = 0.6f;
        public const float RunExitSpeed = 0.3f;

        public static ActionVisualState DeriveState(float speedX, float speedY, bool isGrounded)
        {
            return DeriveState(speedX, speedY, isGrounded, ActionVisualState.Idle);
        }

        public static ActionVisualState DeriveState(float speedX, float speedY, bool isGrounded, ActionVisualState lastState)
        {
            if (!isGrounded)
            {
                if (speedY > 1.0f)
                {
                    return ActionVisualState.Jump;
                }
                if (speedY < -1.0f)
                {
                    return ActionVisualState.Falling;
                }
                if (lastState == ActionVisualState.Jump
                    || lastState == ActionVisualState.DoubleJump
                    || lastState == ActionVisualState.Falling
                    || lastState == ActionVisualState.Glide)
                {
                    return lastState;
                }
                return ActionVisualState.Falling;
            }

            float horizontalSpeed = speedX >= 0f ? speedX : -speedX;
            if (lastState == ActionVisualState.Running)
            {
                return horizontalSpeed > RunExitSpeed ? ActionVisualState.Running : ActionVisualState.Idle;
            }
            return horizontalSpeed > RunEnterSpeed ? ActionVisualState.Running : ActionVisualState.Idle;
        }
    }
}
