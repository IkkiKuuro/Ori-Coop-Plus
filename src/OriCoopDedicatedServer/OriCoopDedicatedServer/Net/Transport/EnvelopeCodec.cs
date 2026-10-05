using System;
using System.Buffers.Binary;
using OriCoop;

namespace OriCoopDedicatedServer.Net.Transport
{
    /// <summary>
    /// Header decodificado do envelope de 24 bytes (D-02/D-03).
    /// Payload segue apos o header, sem tamanho prefixado (resto do datagrama).
    /// </summary>
    public struct NetEnvelope
    {
        public byte Flags;
        public uint Seq;
        public int ClientId;
        public uint Token;
        public int PacketId;
        public uint AckSeq;
    }

    /// <summary>
    /// Codec assimétrico do envelope: servidor usa BinaryPrimitives
    /// little-endian explicito; o cliente espelha com BinaryWriter/Reader
    /// campo-a-campo nos mesmos offsets de <see cref="NetProtocol"/>.
    /// Nenhuma leitura acontece antes de validar length + magic + versao (D-03).
    /// </summary>
    public static class EnvelopeCodec
    {
        public static bool TryEncode(
            byte flags,
            uint seq,
            int clientId,
            uint token,
            int packetId,
            uint ackSeq,
            byte[] payload,
            out byte[] datagram,
            out string error)
        {
            datagram = Array.Empty<byte>();
            error = string.Empty;
            try
            {
                int payloadLength = payload != null ? payload.Length : 0;
                byte[] buffer = new byte[NetProtocol.HeaderSize + payloadLength];
                Span<byte> header = buffer.AsSpan(0, NetProtocol.HeaderSize);
                BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(NetProtocol.OffMagic, 2), NetProtocol.Magic);
                header[NetProtocol.OffVersion] = NetProtocol.Version;
                header[NetProtocol.OffFlags] = flags;
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(NetProtocol.OffSeq, 4), seq);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(NetProtocol.OffClientId, 4), clientId);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(NetProtocol.OffToken, 4), token);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(NetProtocol.OffPacketId, 4), packetId);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(NetProtocol.OffAckSeq, 4), ackSeq);
                if (payload != null && payloadLength > 0)
                {
                    Buffer.BlockCopy(payload, 0, buffer, NetProtocol.HeaderSize, payloadLength);
                }
                datagram = buffer;
                return true;
            }
            catch (Exception ex)
            {
                error = "falha ao codificar envelope: " + ex.GetType().Name;
                return false;
            }
        }

        public static bool TryDecode(
            byte[] datagram,
            out NetEnvelope header,
            out byte[] payload,
            out string rejectReason)
        {
            header = default;
            payload = Array.Empty<byte>();
            rejectReason = string.Empty;

            if (datagram == null || datagram.Length < NetProtocol.HeaderSize)
            {
                rejectReason = "datagrama menor que o header de 24B (recebido "
                    + (datagram == null ? 0 : datagram.Length) + "B); esperado magic 0x4F43 + versao 2";
                return false;
            }

            ReadOnlySpan<byte> raw = datagram.AsSpan(0, NetProtocol.HeaderSize);
            ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(NetProtocol.OffMagic, 2));
            if (magic != NetProtocol.Magic)
            {
                rejectReason = "magic invalido; esperado 0x4F43 — build antigo ou protocolo desconhecido, atualize cliente e servidor para o mesmo build";
                return false;
            }

            byte version = raw[NetProtocol.OffVersion];
            if (version != NetProtocol.Version)
            {
                rejectReason = "versao de envelope incompativel (" + version + "); esperado 2 — atualize cliente e servidor para o mesmo build";
                return false;
            }

            header = new NetEnvelope
            {
                Flags = raw[NetProtocol.OffFlags],
                Seq = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(NetProtocol.OffSeq, 4)),
                ClientId = BinaryPrimitives.ReadInt32LittleEndian(raw.Slice(NetProtocol.OffClientId, 4)),
                Token = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(NetProtocol.OffToken, 4)),
                PacketId = BinaryPrimitives.ReadInt32LittleEndian(raw.Slice(NetProtocol.OffPacketId, 4)),
                AckSeq = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(NetProtocol.OffAckSeq, 4)),
            };

            int payloadLength = datagram.Length - NetProtocol.HeaderSize;
            byte[] body = new byte[payloadLength];
            if (payloadLength > 0)
            {
                Buffer.BlockCopy(datagram, NetProtocol.HeaderSize, body, 0, payloadLength);
            }
            payload = body;
            return true;
        }
    }
}
