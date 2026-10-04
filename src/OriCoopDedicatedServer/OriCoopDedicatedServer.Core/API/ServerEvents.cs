using System;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Core.API;

public static class ServerEvents
{
	public static Action<Client> OnClientConnected;

	public static Action<int, Client, Packet> OnPacketRecived;

	public static Action OnServerStarted;
}

