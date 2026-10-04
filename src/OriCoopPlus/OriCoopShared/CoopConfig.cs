using System;

namespace OriCoop
{
    [Serializable]
    public class CoopConfig
    {
        public bool AllowTeleport = false;
        public bool ShareAbilities = false;
        public bool ShareStoryOnly = false;
        public bool ShareWorldEvents = false;
        public bool ShareDoorsAndLevers = false;
        public bool ShowNicknames = false;
        public bool AllowCustomColors = false;

        public void CopyFrom(CoopConfig other)
        {
            if (other == null) return;
            AllowTeleport = other.AllowTeleport;
            ShareAbilities = other.ShareAbilities;
            ShareStoryOnly = other.ShareStoryOnly;
            ShareWorldEvents = other.ShareWorldEvents;
            ShareDoorsAndLevers = other.ShareDoorsAndLevers;
            ShowNicknames = other.ShowNicknames;
            AllowCustomColors = other.AllowCustomColors;
        }
    }
}
