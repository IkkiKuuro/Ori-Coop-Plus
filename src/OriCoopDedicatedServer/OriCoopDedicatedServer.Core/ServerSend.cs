using System.Collections.Generic;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Core;

public static class ServerSend
{
	public static void SetNetworkVar(string name, string value)
	{
		name = name.ToUpper();
		if (Server.NetworkVars.ContainsKey(name))
		{
			Server.NetworkVars[name] = value;
		}
		else
		{
			Server.NetworkVars.Add(name, value);
		}
		Packet packet = new Packet(-3);
		packet.Write(name);
		packet.Write(value);
		SendToAll(packet);
	}

	public static void SyncNetworkVars(Client client)
	{
		foreach (KeyValuePair<string, string> networkVar in Server.NetworkVars)
		{
			Packet packet = new Packet(-3);
			packet.Write(networkVar.Key);
			packet.Write(networkVar.Value);
			client.Send(packet);
		}
	}

	public static void SendToClient(int _toClient, Packet packet)
	{
		Server.Clients[_toClient].udp.SendData(packet);
	}

	public static void SendToAll(Packet packet)
	{
		for (int i = 0; i < Server.MaxPlayers; i++)
		{
			Server.Clients[i].udp.SendData(packet);
		}
	}

	public static void SendToAll(int IgnoreClient, Packet packet)
	{
		if (Server.EnableFakePackets)
		{
			SendToAll(packet);
			return;
		}
		for (int i = 0; i < Server.MaxPlayers; i++)
		{
			if (i != IgnoreClient)
			{
				Server.Clients[i].udp.SendData(packet);
			}
		}
	}

	public static void SendChatMessage(string Message)
	{
		Packet packet = new Packet(-5);
		packet.Write("<color=red>SERVER</color>");
		packet.Write(Message);
		SendToAll(packet);
	}

	public static void Send(this Client client, Packet packet)
	{
		SendToClient(client.Id, packet);
	}
}

