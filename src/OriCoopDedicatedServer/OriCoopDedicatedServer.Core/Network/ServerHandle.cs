using System;
using System.Collections.Generic;
using OriCoopDedicatedServer.Core.API;

namespace OriCoopDedicatedServer.Core.Network;

public static class ServerHandle
{
	public delegate void PacketHandler(Client client, Packet _packet);

	public static Dictionary<int, PacketHandler> PacketHandlers = new Dictionary<int, PacketHandler>
	{
		{ -1, ClientDoneMessage },
		{ -2, ReciveNBMessage },
		{ -4, ReceiveDisconnectMessage },
		{ -5, ReceiveChatMessage },
		{ -7, ReceivePingMessage },
		{ -6, ReceiveRpcMessage }
	};

	public static void ServerRecive(Client client, Packet packet)
	{
		if (client.udp.endPoint == null)
		{
			return;
		}
		int num = packet.ReadInt();
		if (PacketHandlers.TryGetValue(num, out var value))
		{
			value(client, packet);
			return;
		}
		try
		{
			ServerEvents.OnPacketRecived?.Invoke(num, client, packet);
		}
		catch (Exception arg)
		{
			Logger.Error("SERVER", $"FAILED TO EXECUTE UNKNOWN HANDLER ID: {num} EXEPTION: {arg}");
		}
	}

	public static void ClientDoneMessage(Client client, Packet packet)
	{
		string text = packet.ReadString();
		Logger.Info("SERVER", $"{client.Address} connected successfully and is new player: {text}");
		client.IsReady = true;
		client.Nick = text;
		ServerSend.SyncNetworkVars(client);
		ServerSend.SendChatMessage("<color=green>+" + client.Nick + "</color>");
		if (ServerEvents.OnClientConnected != null)
		{
			ServerEvents.OnClientConnected(client);
		}
	}

	private static void ReceiveRpcMessage(Client client, Packet _packet)
	{
		try
		{
			byte[] value = _packet.ReadBytes(_packet.UnreadLength());
			Packet packet = new Packet(-6);
			packet.Write(client.Id);
			packet.Write(value);
			ServerSend.SendToAll(packet);
		}
		catch (Exception arg)
		{
			Logger.Error("RPC", $"FAILED TO PROCESS RPC FROM CLIENT {client.Id}! REASON: {arg}");
		}
	}

	private static void ReceivePingMessage(Client client, Packet packet)
	{
		long sentTicks = packet.ReadLong();
		Packet response = new Packet(-7);
		response.Write(sentTicks);
		client.Send(response);
	}

	private static void ReceiveChatMessage(Client client, Packet _packet)
	{
		try
		{
			string text = _packet.ReadString();
			if (text.Length > 350)
			{
				text = text.Remove(350);
			}
			text = text.Replace("<", string.Empty).Replace(">", string.Empty);

			string lowerText = text.ToLowerInvariant().Trim();
			if (lowerText == "h" || lowerText == "help" || lowerText == "/h" || lowerText == "/help")
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.Append("Commands: ");
				foreach (var cmd in OriCoopDedicatedServer.Core.CommandSystem.CommandProcessor.AllCommands)
				{
					sb.Append("/").Append(cmd.Command).Append(" ");
				}
				Packet helpPacket = new Packet(-5);
				helpPacket.Write("<color=yellow>SERVER</color>");
				helpPacket.Write(sb.ToString());
				ServerSend.SendToClient(client.Id, helpPacket);
				return;
			}

			Packet packet = new Packet(-5);
			packet.Write("<color=green>" + client.Nick + "</color>");
			packet.Write(text);
			ServerSend.SendToAll(packet);
			Logger.Info($"CHAT [{client.Nick} {client.Id}]", text);
		}
		catch (Exception arg)
		{
			Logger.Error("CHAT", $"FAILED TO PROCESS MESSAGE FROM CLIENT {client.Id}! REASON: {arg}");
		}
	}

	private static void ReceiveDisconnectMessage(Client client, Packet _packet)
	{
		string arg = "UNKNOWN";
		try
		{
			arg = _packet.ReadString();
		}
		catch (Exception)
		{
		}
		Logger.Info("SERVER", $"CLIENT {client.Id} DISCONNECTED! REASON: {arg}");
		client.IsReady = false;
		ServerSend.SendChatMessage("<color=red>-" + client.Nick + "</color>");
	}

	private static void ReciveNBMessage(Client client, Packet _packet)
	{
		byte[] value = _packet.ReadBytes(_packet.UnreadLength());
		Packet packet = new Packet(-2);
		packet.Write(client.Id);
		packet.Write(value);
		ServerSend.SendToAll(client.Id, packet);
	}
}
