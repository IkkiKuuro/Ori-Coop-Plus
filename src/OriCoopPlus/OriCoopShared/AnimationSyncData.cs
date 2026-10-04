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
        Dash = 11
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
    }
}
