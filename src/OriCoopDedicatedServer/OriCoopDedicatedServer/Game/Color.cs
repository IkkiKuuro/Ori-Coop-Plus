using System;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Game
{
    public class Color
    {
        public byte R;
        public byte G;
        public byte B;

        public Color(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static Color RandomNew()
        {
            Random random = new Random((int)DateTime.Now.Ticks);
            int num = random.Next(50, 256);
            int num2 = random.Next(50, 256);
            int num3 = random.Next(50, 256);
            return new Color((byte)num, (byte)num2, (byte)num3);
        }

        public static Color FromPacket(ref Packet packet)
        {
            byte r = packet.ReadByte();
            byte g = packet.ReadByte();
            byte b = packet.ReadByte();
            return new Color(r, g, b);
        }

        public void WritePacket(ref Packet packet)
        {
            packet.Write(R);
            packet.Write(G);
            packet.Write(B);
        }

        public override string ToString()
        {
            return $"R: {R}, G: {G}, B: {B}";
        }
    }
}

