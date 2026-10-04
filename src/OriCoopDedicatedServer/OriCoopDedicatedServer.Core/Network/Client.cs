using System.Net;

namespace OriCoopDedicatedServer.Core.Network;

public class Client
{
	public class UDP
	{
		public IPEndPoint endPoint;

		private int id;

		private Client Client;

		public UDP(int _id, Client _client)
		{
			id = _id;
			Client = _client;
		}

		public void Connect(IPEndPoint _endPoint)
		{
			Client.ClientAddress = _endPoint.Address.ToString();
			endPoint = _endPoint;
			Packet packet = new Packet(-1);
			packet.Write($"WELCOME TO THE SERVER YOUR ID: {id}");
			packet.Write(id);
			SendData(packet, IgnoreReady: true);
		}

		public void SendData(Packet packet, bool IgnoreReady = false)
		{
			if (Client.IsReady || IgnoreReady)
			{
				Server.SendUDPData(endPoint, packet);
			}
		}

		public void HandleData(Packet packet, int ClientId)
		{
			int length = packet.ReadInt();
			byte[] data = packet.ReadBytes(length);
			Packet packet2 = new Packet(data);
			if (ClientId != id)
			{
				Logger.Warning($"CLIENT [{id}]", $"TRIED TO SYNC WITH ID: {ClientId}");
			}
			else
			{
				ServerHandle.ServerRecive(Client, packet2);
			}
		}
	}

	public static int dataBufferSize = 4096;

	private bool _IsReady = false;

	public string Nick;

	public string ClientAddress = string.Empty;

	public int Id { get; }

	public UDP udp { get; }

	public bool IsReady
	{
		get
		{
			if (udp.endPoint == null)
			{
				return false;
			}
			return _IsReady;
		}
		set
		{
			_IsReady = value;
		}
	}

	public IPEndPoint Address => udp.endPoint;

	public Client(int _clientid)
	{
		Id = _clientid;
		udp = new UDP(Id, this);
	}
}

