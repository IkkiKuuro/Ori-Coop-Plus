using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using OriCoopDedicatedServer.Core.API;

namespace OriCoopDedicatedServer.Core.Network;

public static class Server
{
	public static bool StopTheServer = false;

	public static Dictionary<int, Client> Clients = new Dictionary<int, Client>();

	private static UdpClient _udpListener;
	private static readonly object _udpListenerLock = new object();

	// Flood de "connection was reset": no Windows, um UdpClient que enviou
	// datagrama para um endpoint morto recebe ICMP Port Unreachable e o
	// proximo EndReceive estoura SocketError.ConnectionReset. Como o relay
	// envia snapshots sem parar, um cliente fechado sem DISCONNECT gera um
	// warning por pacote. SIO_UDP_CONNRESET faz o socket ignorar o ICMP.
	private const int SIO_UDP_CONNRESET = -1744830452;
	private static DateTime _lastResetWarnUtc = DateTime.MinValue;

	public static int LatesNetId = 0;

	public static bool EnableFakePackets = false;

	public static Dictionary<string, string> NetworkVars = new Dictionary<string, string>();

	public static bool IgnoreIpCheck = false;

	public static int MaxPlayers { get; private set; }

	public static int Port { get; private set; }

	public static void Start(int _maxplayers, int _port)
	{
		MaxPlayers = _maxplayers;
		Port = _port;
		Logger.Info("SERVER", "Starting server...");
		InitializeServerData();
		_udpListener = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
		DisableUdpConnectionReset(_udpListener);
		_udpListener.BeginReceive(UDPReciveCallback, null);
		Logger.Info("SERVER", $"Server started on {Port} maxplayers: {MaxPlayers}");
		LogLanAddresses();
		if (ServerEvents.OnServerStarted != null)
		{
			ServerEvents.OnServerStarted();
		}
	}

	private static void UDPReciveCallback(IAsyncResult ar)
	{
		try
		{
			IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
			byte[] array = _udpListener.EndReceive(ar, ref remoteEP);
			if (array == null || array.Length < 4)
			{
				Logger.Warning("SERVER", $"Ignored invalid UDP packet from {remoteEP}.");
				return;
			}

			Packet packet = new Packet(array);
			int num = packet.ReadInt();
			if (num <= -1)
			{
				Logger.Info("SERVER", $"Incoming connection from {remoteEP}...");
				if (!AddClient(remoteEP))
				{
					Logger.Warning("SERVER", $"{remoteEP} failed to connect: Server Full!");
				}
			}
			else if (Clients.TryGetValue(num, out Client client))
			{
				Client.UDP udp = client.udp;
				if (udp.endPoint != null && udp.endPoint.Equals(remoteEP))
				{
					udp.HandleData(packet, num);
				}
			}
			else
			{
				Logger.Warning("SERVER", $"Ignored packet with unknown client ID {num} from {remoteEP}.");
			}
		}
		catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
		{
			ThrottledResetWarning("A UDP client connection was reset; continuing to listen.");
		}
		catch (Exception arg)
		{
			Logger.Error("SERVER", $"RECIVE UDP CALLBACK ERROR: {arg}");
		}
		finally
		{
			BeginReceiveOrRestart();
		}
	}

	private static void BeginReceiveOrRestart()
	{
		lock (_udpListenerLock)
		{
			if (_udpListener == null)
			{
				return;
			}

			try
			{
				_udpListener.BeginReceive(UDPReciveCallback, null);
			}
			catch (ObjectDisposedException)
			{
				// The server is shutting down.
			}
			catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
			{
				Logger.Warning("SERVER", "UDP listener was reset; recreating it.");
				try
				{
					_udpListener.Close();
					_udpListener = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
					DisableUdpConnectionReset(_udpListener);
					_udpListener.BeginReceive(UDPReciveCallback, null);
					Logger.Info("SERVER", $"UDP listener restarted on {Port}.");
				}
				catch (Exception restartException)
				{
					Logger.Error("SERVER", $"FAILED TO RECREATE UDP LISTENER: {restartException}");
				}
			}
			catch (SocketException ex)
			{
				Logger.Error("SERVER", $"FAILED TO RESTART UDP RECEIVE: {ex}");
			}
		}
	}

	private static void DisableUdpConnectionReset(UdpClient udp)
	{
		try
		{
			if (udp != null && udp.Client != null)
			{
				udp.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
			}
		}
		catch
		{
			// Plataforma sem suporte ao IOControl: mantém comportamento anterior.
		}
	}

	private static void ThrottledResetWarning(string message)
	{
		DateTime now = DateTime.UtcNow;
		if ((now - _lastResetWarnUtc).TotalSeconds >= 5.0)
		{
			_lastResetWarnUtc = now;
			Logger.Warning("SERVER", message);
		}
	}

	private static bool AddClient(IPEndPoint clientEndPoint)
	{
		for (int i = 0; i < Clients.Count; i++)
		{
			Client client = Clients[i];
			if (!IgnoreIpCheck && client.ClientAddress == clientEndPoint.Address.ToString())
			{
				client.udp.Connect(clientEndPoint);
				return true;
			}
			if (!client.IsReady)
			{
				client.udp.Connect(clientEndPoint);
				return true;
			}
		}
		return false;
	}

	public static void SendUDPData(IPEndPoint _clientEndPoint, Packet packet)
	{
		try
		{
			if (_clientEndPoint != null)
			{
				_udpListener.BeginSend(packet.ToArray(), packet.Length(), _clientEndPoint, null, null);
			}
		}
		catch (Exception arg)
		{
			Logger.Error("SERVER", $"SEND UDP DATA ERROR: {arg}");
		}
	}

	private static void InitializeServerData()
	{
		Clients.Clear();
		for (int i = 0; i < MaxPlayers; i++)
		{
			Clients.Add(i, new Client(i));
		}
	}

	private static void LogLanAddresses()
	{
		IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
		bool foundAddress = false;
		foreach (IPAddress address in addresses)
		{
			if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
			{
				Logger.Info("SERVER", $"LAN address: {address}:{Port}");
				foundAddress = true;
			}
		}

		if (!foundAddress)
		{
			Logger.Warning("SERVER", "No non-loopback IPv4 address was found. Check the network adapter.");
		}
	}
}

