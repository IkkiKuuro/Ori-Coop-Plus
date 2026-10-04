namespace OriCoopBepInEx.Domain
{
    public struct Vector2Data
    {
        public float X;
        public float Y;

        public Vector2Data(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public struct Vector3Data
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct PlayerInputState
    {
        public bool Left;
        public bool Right;
        public bool Jump;
        public bool Spirit;
        public bool Stomp;
    }

    public struct AnimationState
    {
        public string Name;
        public bool FacingLeft;
    }

    public sealed class PlayerSnapshot
    {
        public int PlayerId;
        public string Nick;
        public Vector3Data Position;
        public Vector2Data Velocity;
        public PlayerInputState Input;
        public AnimationState Animation;
        public long Timestamp;
    }
}
