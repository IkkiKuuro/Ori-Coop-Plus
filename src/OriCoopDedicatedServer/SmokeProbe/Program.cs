using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

// SmokeProbe: harness headless BCL-only (sem NuGet) do tracer 02-01.
// --test all (padrao): sobe o servidor --net2 na porta de teste, executa
//   invalid-magic -> handshake -> relay -> ping, derruba o servidor.
// --test handshake|relay|ping: conecta num servidor --net2 ja em pe (--port).
// Saida final SMOKE_OK (exit 0) ou SMOKE_FAIL: motivo (exit 1).
internal static class Program
{
    private const ushort Magic = 0x4F43;
    private const byte Version = 2;
    private const int HeaderSize = 24;
    private const int MsgHello = 100;
    private const int MsgWelcome = 101;
    private const int MsgConfirm = 102;
    private const int MsgPing = 104;
    private const int MsgPong = 105;
    private const int MsgReject = 106;
    private const int PlayerStateId = 18;

    private static int Main(string[] args)
    {
        int port = 7779;
        string test = "all";
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            if (arg == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedPort))
            {
                port = parsedPort;
                i++;
            }
            else if (arg == "--test" && i + 1 < args.Length)
            {
                test = args[i + 1].ToLowerInvariant();
                i++;
            }
        }

        try
        {
            if (test == "all")
            {
                return RunAll(port);
            }
            return RunSingle(test, port);
        }
        catch (Exception ex)
        {
            Console.WriteLine("SMOKE_FAIL: " + ex.GetType().Name + " " + ex.Message);
            return 1;
        }
    }

    private static int RunAll(int port)
    {
        string serverProject = FindServerProject();
        var startInfo = new ProcessStartInfo("dotnet", "run --project \"" + serverProject + "\" --configuration Release -- --net2 --auto --port " + port + " --max-players 10")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(serverProject),
        };
        var serverLog = new StringBuilder();
        using (var server = new Process { StartInfo = startInfo })
        {
            server.OutputDataReceived += (sender, e) => { if (e.Data != null) { lock (serverLog) { serverLog.AppendLine(e.Data); } } };
            server.ErrorDataReceived += (sender, e) => { if (e.Data != null) { lock (serverLog) { serverLog.AppendLine("STDERR: " + e.Data); } } };
            server.Start();
            server.BeginOutputReadLine();
            server.BeginErrorReadLine();
            try
            {
                WaitForServer(port, TimeSpan.FromSeconds(120));
                FailUnless(TestInvalidMagic(port), "invalid-magic sem Reject com mensagem");
                FailUnless(TestHandshake(port, 1), "handshake falhou");
                FailUnless(TestRelay(port), "relay falhou");
                FailUnless(TestPing(port), "ping falhou");
                Console.WriteLine("SMOKE_OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SMOKE_FAIL: " + ex.GetType().Name + " " + ex.Message);
                Console.WriteLine("--- server log (tail) ---");
                Console.WriteLine(Tail(serverLog.ToString(), 30));
                return 1;
            }
            finally
            {
                try
                {
                    if (!server.HasExited)
                    {
                        server.Kill();
                    }
                    server.WaitForExit(5000);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private static int RunSingle(string test, int port)
    {
        bool ok;
        if (test == "handshake")
        {
            ok = TestHandshake(port, -1);
        }
        else if (test == "relay")
        {
            ok = TestRelay(port);
        }
        else if (test == "ping")
        {
            ok = TestPing(port);
        }
        else
        {
            Console.WriteLine("SMOKE_FAIL: teste desconhecido '" + test + "' (use all|handshake|relay|ping)");
            return 1;
        }
        Console.WriteLine(ok ? "SMOKE_OK" : "SMOKE_FAIL: " + test);
        return ok ? 0 : 1;
    }

    private static void FailUnless(bool ok, string reason)
    {
        if (!ok)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private static string Tail(string text, int lines)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(sem saida do servidor)";
        }
        string[] parts = text.Split(new char[] { '\n' });
        int start = parts.Length > lines ? parts.Length - lines : 0;
        var sb = new StringBuilder();
        for (int i = start; i < parts.Length; i++)
        {
            sb.AppendLine(parts[i].TrimEnd('\r'));
        }
        return sb.ToString();
    }

    private static string FindServerProject()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            string candidate = Path.Combine(dir, "OriCoopDedicatedServer", "OriCoopDedicatedServer.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            candidate = Path.Combine(dir, "OriCoopDedicatedServer.csproj");
            if (File.Exists(candidate) && Path.GetFileName(Path.GetDirectoryName(candidate)) == "OriCoopDedicatedServer")
            {
                return candidate;
            }
            string trimmed = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            dir = Path.GetDirectoryName(trimmed) ?? string.Empty;
        }
        throw new FileNotFoundException("OriCoopDedicatedServer.csproj nao encontrado a partir de " + AppContext.BaseDirectory);
    }

    private static void WaitForServer(int port, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        using (var udp = new UdpClient(AddressFamily.InterNetwork))
        {
            udp.Client.ReceiveTimeout = 1000;
            var server = new IPEndPoint(IPAddress.Loopback, port);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] garbage = Encoding.ASCII.GetBytes("SMOKE_PROBE_READINESS_PING");
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    udp.Send(garbage, garbage.Length, server);
                    byte[] reply = udp.Receive(ref remote);
                    if (TryParseHeader(reply, out int packetId, out _, out _, out _, out byte[] payload)
                        && packetId == MsgReject && payload.Length > 4)
                    {
                        Console.WriteLine("PASS readiness (Reject recebido, servidor --net2 no ar)");
                        return;
                    }
                }
                catch (SocketException)
                {
                }
            }
        }
        throw new TimeoutException("servidor --net2 nao respondeu Reject na porta " + port + " em " + timeout.TotalSeconds + "s");
    }

    private static bool TestInvalidMagic(int port)
    {
        using (var udp = NewClient())
        {
            var server = new IPEndPoint(IPAddress.Loopback, port);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] garbage = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            udp.Send(garbage, garbage.Length, server);
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    byte[] reply = udp.Receive(ref remote);
                    if (TryParseHeader(reply, out int packetId, out _, out _, out _, out byte[] payload) && packetId == MsgReject)
                    {
                        string reason = ReadLegacyString(payload);
                        if (!string.IsNullOrEmpty(reason))
                        {
                            Console.WriteLine("PASS invalid-magic (Reject: " + reason + ")");
                            return true;
                        }
                    }
                }
                catch (SocketException)
                {
                }
            }
            Console.WriteLine("FAIL invalid-magic (sem Reject com mensagem)");
            return false;
        }
    }

    private static bool TestHandshake(int port, int expectBaseId)
    {
        using (var clientA = NewClient())
        using (var clientB = NewClient())
        {
            var server = new IPEndPoint(IPAddress.Loopback, port);
            uint seqA = 0;
            uint seqB = 0;

            if (!HelloConfirm(clientA, server, "Probe_A", ref seqA, out int idA, out uint tokenA) || idA <= 0)
            {
                Console.WriteLine("FAIL handshake (A sem ID valido)");
                return false;
            }
            if (!HelloConfirm(clientB, server, "Probe_B", ref seqB, out int idB, out uint tokenB) || idB <= 0 || idB == idA)
            {
                Console.WriteLine("FAIL handshake (B sem ID valido e distinto)");
                return false;
            }
            if (expectBaseId > 0 && (idA != expectBaseId || idB != expectBaseId + 1))
            {
                Console.WriteLine("FAIL handshake (esperava IDs " + expectBaseId + "/" + (expectBaseId + 1) + ", veio " + idA + "/" + idB + ")");
                return false;
            }
            if (idA == 0 || idB == 0 || idA == 999 || idB == 999)
            {
                Console.WriteLine("FAIL handshake (ID reservado 0/999 entregue)");
                return false;
            }

            // Negativo: Confirm com token errado deve ser recusado.
            byte[] badConfirm = BuildEnvelope(0, ++seqA, idA, tokenA + 1, MsgConfirm, Array.Empty<byte>());
            clientA.Send(badConfirm, badConfirm.Length, server);
            if (!ExpectReject(clientA, 3000))
            {
                Console.WriteLine("FAIL handshake (Confirm com token errado nao foi recusado)");
                return false;
            }

            // Negativo: Confirm de sessao inexistente deve ser recusado.
            byte[] ghostConfirm = BuildEnvelope(0, ++seqA, 777, 1234, MsgConfirm, Array.Empty<byte>());
            clientA.Send(ghostConfirm, ghostConfirm.Length, server);
            if (!ExpectReject(clientA, 3000))
            {
                Console.WriteLine("FAIL handshake (Confirm fantasma nao foi recusado)");
                return false;
            }

            Console.WriteLine("PASS handshake (IDs " + idA + " e " + idB + ", IsReady apos Confirm, recusas ok)");
            return true;
        }
    }

    private static bool HelloConfirm(UdpClient client, IPEndPoint server, string nick, ref uint seq, out int assignedId, out uint token)
    {
        assignedId = -1;
        token = 0;
        byte[] hello = BuildEnvelope(0, ++seq, -1, 0, MsgHello, BuildHelloPayload(nick));
        client.Send(hello, hello.Length, server);
        var remote = new IPEndPoint(IPAddress.Any, 0);
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                byte[] reply = client.Receive(ref remote);
                if (!TryParseHeader(reply, out int packetId, out _, out _, out _, out byte[] payload))
                {
                    continue;
                }
                if (packetId == MsgWelcome && payload.Length >= 9)
                {
                    assignedId = ReadI32(payload, 0);
                    token = ReadU32(payload, 4);
                    byte serverVer = payload[8];
                    if (serverVer != Version || assignedId <= 0)
                    {
                        return false;
                    }
                    byte[] confirm = BuildEnvelope(0, ++seq, assignedId, token, MsgConfirm, Array.Empty<byte>());
                    client.Send(confirm, confirm.Length, server);
                    return true;
                }
            }
            catch (SocketException)
            {
            }
        }
        return false;
    }

    private static bool ExpectReject(UdpClient client, int timeoutMs)
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        int saved = client.Client.ReceiveTimeout;
        client.Client.ReceiveTimeout = timeoutMs;
        try
        {
            byte[] reply = client.Receive(ref remote);
            return TryParseHeader(reply, out int packetId, out _, out _, out _, out _) && packetId == MsgReject;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            client.Client.ReceiveTimeout = saved;
        }
    }

    private static bool TestRelay(int port)
    {
        using (var clientA = NewClient())
        using (var clientB = NewClient())
        {
            var server = new IPEndPoint(IPAddress.Loopback, port);
            uint seqA = 0;
            uint seqB = 0;
            if (!HelloConfirm(clientA, server, "Relay_A", ref seqA, out int idA, out uint tokenA) || idA <= 0)
            {
                Console.WriteLine("FAIL relay (handshake A)");
                return false;
            }
            if (!HelloConfirm(clientB, server, "Relay_B", ref seqB, out int idB, out uint tokenB) || idB <= 0 || idB == idA)
            {
                Console.WriteLine("FAIL relay (handshake B)");
                return false;
            }

            byte[] body = BuildPlayerStateBody();
            uint stateSeq = ++seqA;
            byte[] state = BuildEnvelope(0, stateSeq, idA, tokenA, PlayerStateId, body);
            clientA.Send(state, state.Length, server);

            // B deve receber o relay com clientId + seq + corpo preservados.
            var remote = new IPEndPoint(IPAddress.Any, 0);
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            bool relayOk = false;
            while (DateTime.UtcNow < deadline && !relayOk)
            {
                try
                {
                    byte[] reply = clientB.Receive(ref remote);
                    if (TryParseHeader(reply, out int packetId, out uint rseq, out int rclient, out _, out byte[] rbody)
                        && packetId == PlayerStateId && rclient == idA && rseq == stateSeq && BytesEqual(rbody, body))
                    {
                        relayOk = true;
                    }
                }
                catch (SocketException)
                {
                }
            }
            if (!relayOk)
            {
                Console.WriteLine("FAIL relay (B nao recebeu snapshot de A intacto)");
                return false;
            }

            // A nao deve receber eco do proprio snapshot.
            clientA.Client.ReceiveTimeout = 600;
            try
            {
                byte[] echo = clientA.Receive(ref remote);
                if (TryParseHeader(echo, out int packetId, out _, out _, out _, out _) && packetId == PlayerStateId)
                {
                    Console.WriteLine("FAIL relay (eco para o remetente)");
                    return false;
                }
            }
            catch (SocketException)
            {
            }

            // Reenvio com a mesma seq deve ser descartado (drop-old).
            clientA.Send(state, state.Length, server);
            clientB.Client.ReceiveTimeout = 600;
            try
            {
                byte[] dup = clientB.Receive(ref remote);
                if (TryParseHeader(dup, out int packetId, out uint rseq, out _, out _, out _) && packetId == PlayerStateId && rseq == stateSeq)
                {
                    Console.WriteLine("FAIL relay (seq repetida foi repassada)");
                    return false;
                }
            }
            catch (SocketException)
            {
            }

            Console.WriteLine("PASS relay (B recebeu snapshot de A intacto; sem eco; drop-old ok)");
            return true;
        }
    }

    private static bool TestPing(int port)
    {
        using (var client = NewClient())
        {
            var server = new IPEndPoint(IPAddress.Loopback, port);
            uint seq = 0;
            if (!HelloConfirm(client, server, "Ping_C", ref seq, out int id, out uint token) || id <= 0)
            {
                Console.WriteLine("FAIL ping (handshake)");
                return false;
            }
            long sentTicks = DateTime.UtcNow.Ticks;
            byte[] ticksBytes = BitConverter.GetBytes(sentTicks);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(ticksBytes);
            }
            byte[] ping = BuildEnvelope(0, ++seq, id, token, MsgPing, ticksBytes);
            client.Send(ping, ping.Length, server);

            var remote = new IPEndPoint(IPAddress.Any, 0);
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    byte[] reply = client.Receive(ref remote);
                    if (TryParseHeader(reply, out int packetId, out _, out _, out _, out byte[] payload) && packetId == MsgPong)
                    {
                        long echoed = ReadI64(payload, 0);
                        if (echoed != sentTicks)
                        {
                            continue;
                        }
                        long rttMs = (DateTime.UtcNow.Ticks - sentTicks) / TimeSpan.TicksPerMillisecond;
                        if (rttMs > 1000)
                        {
                            Console.WriteLine("FAIL ping (eco em " + rttMs + " ms > 1000 ms)");
                            return false;
                        }
                        Console.WriteLine("PASS ping (eco em " + rttMs + " ms)");
                        return true;
                    }
                }
                catch (SocketException)
                {
                }
            }
            Console.WriteLine("FAIL ping (sem Pong)");
            return false;
        }
    }

    private static UdpClient NewClient()
    {
        var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.ReceiveTimeout = 2000;
        return udp;
    }

    private static byte[] BuildHelloPayload(string nick)
    {
        byte[] nickBytes = Encoding.ASCII.GetBytes(nick ?? string.Empty);
        byte[] payload = new byte[1 + 4 + nickBytes.Length];
        payload[0] = Version;
        WriteI32(payload, 1, nickBytes.Length);
        Buffer.BlockCopy(nickBytes, 0, payload, 5, nickBytes.Length);
        return payload;
    }

    private static byte[] BuildPlayerStateBody()
    {
        // Corpo legado byte-identico: leading int 18 + ordem de campos atual.
        using (var stream = new MemoryStream())
        {
            WriteI32Stream(stream, PlayerStateId);
            WriteF32Stream(stream, 10.5f);
            WriteF32Stream(stream, 20.25f);
            WriteF32Stream(stream, 0f);
            stream.WriteByte(3);
            stream.WriteByte(1 | 2);
            WriteI32Stream(stream, unchecked((int)0xDEADBEEFu));
            WriteF32Stream(stream, 1.5f);
            WriteF32Stream(stream, -2.5f);
            byte[] nickBytes = Encoding.ASCII.GetBytes("Relay_A");
            WriteI32Stream(stream, nickBytes.Length);
            stream.Write(nickBytes, 0, nickBytes.Length);
            return stream.ToArray();
        }
    }

    private static byte[] BuildEnvelope(byte flags, uint seq, int clientId, uint token, int packetId, byte[] payload)
    {
        byte[] datagram = new byte[HeaderSize + payload.Length];
        WriteU16(datagram, 0, Magic);
        datagram[2] = Version;
        datagram[3] = flags;
        WriteU32(datagram, 4, seq);
        WriteI32(datagram, 8, clientId);
        WriteU32(datagram, 12, token);
        WriteI32(datagram, 16, packetId);
        WriteU32(datagram, 20, 0);
        Buffer.BlockCopy(payload, 0, datagram, HeaderSize, payload.Length);
        return datagram;
    }

    private static bool TryParseHeader(byte[] datagram, out int packetId, out uint seq, out int clientId, out uint token, out byte[] payload)
    {
        packetId = 0;
        seq = 0;
        clientId = 0;
        token = 0;
        payload = Array.Empty<byte>();
        if (datagram == null || datagram.Length < HeaderSize)
        {
            return false;
        }
        if (ReadU16(datagram, 0) != Magic || datagram[2] != Version)
        {
            return false;
        }
        seq = ReadU32(datagram, 4);
        clientId = ReadI32(datagram, 8);
        token = ReadU32(datagram, 12);
        packetId = ReadI32(datagram, 16);
        payload = new byte[datagram.Length - HeaderSize];
        Buffer.BlockCopy(datagram, HeaderSize, payload, 0, payload.Length);
        return true;
    }

    private static string ReadLegacyString(byte[] payload)
    {
        if (payload == null || payload.Length < 4)
        {
            return string.Empty;
        }
        int length = ReadI32(payload, 0);
        if (length < 0 || length > payload.Length - 4)
        {
            return string.Empty;
        }
        return Encoding.ASCII.GetString(payload, 4, length);
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static void WriteU16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteI32(byte[] buffer, int offset, int value)
    {
        WriteU32(buffer, offset, unchecked((uint)value));
    }

    private static void WriteU32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static ushort ReadU16(byte[] buffer, int offset)
    {
        return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
    }

    private static int ReadI32(byte[] buffer, int offset)
    {
        return unchecked((int)ReadU32(buffer, offset));
    }

    private static uint ReadU32(byte[] buffer, int offset)
    {
        return (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
    }

    private static long ReadI64(byte[] buffer, int offset)
    {
        uint lo = ReadU32(buffer, offset);
        uint hi = ReadU32(buffer, offset + 4);
        return unchecked((long)(((ulong)hi << 32) | lo));
    }

    private static void WriteI32Stream(MemoryStream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteF32Stream(MemoryStream stream, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        stream.Write(bytes, 0, bytes.Length);
    }
}
