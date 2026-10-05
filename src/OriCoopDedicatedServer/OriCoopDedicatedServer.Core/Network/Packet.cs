using System;
using System.Collections.Generic;
using System.Text;
using OriCoopDedicatedServer.Core.API;

namespace OriCoopDedicatedServer.Core.Network;

public class Packet : IDisposable
{
	private List<byte> buffer;

	private byte[] readableBuffer;

	private int readPos;

	private bool disposed = false;

	public Packet()
	{
		buffer = new List<byte>();
		readPos = 0;
	}

	public Packet(int id)
	{
		buffer = new List<byte>();
		readPos = 0;
		Write(id);
	}

	public Packet(byte[] data)
	{
		buffer = new List<byte>();
		readPos = 0;
		SetBytes(data);
	}

	public void SetBytes(byte[] data)
	{
		Write(data);
		readableBuffer = buffer.ToArray();
	}

	public void WriteLength()
	{
		buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));
	}

	public void InsertInt(int value)
	{
		buffer.InsertRange(0, BitConverter.GetBytes(value));
	}

	public byte[] ToArray()
	{
		readableBuffer = buffer.ToArray();
		return readableBuffer;
	}

	public int Length()
	{
		return buffer.Count;
	}

	public int UnreadLength()
	{
		return Length() - readPos;
	}

	public void Reset(bool shouldReset = true)
	{
		if (shouldReset)
		{
			buffer.Clear();
			readableBuffer = null;
			readPos = 0;
		}
		else
		{
			readPos -= 4;
		}
	}

	public void Write(byte value)
	{
		buffer.Add(value);
	}

	public void Write(byte[] value)
	{
		buffer.AddRange(value);
	}

	public void Write(short value)
	{
		buffer.AddRange(BitConverter.GetBytes(value));
	}

	public void Write(int value)
	{
		buffer.AddRange(BitConverter.GetBytes(value));
	}

	public void Write(long value)
	{
		buffer.AddRange(BitConverter.GetBytes(value));
	}

	public void Write(float value)
	{
		buffer.AddRange(BitConverter.GetBytes(value));
	}

	public void Write(bool value)
	{
		buffer.AddRange(BitConverter.GetBytes(value));
	}

	public void Write(string value)
	{
		Write(value.Length);
		buffer.AddRange(Encoding.ASCII.GetBytes(value));
	}

	public void Write(Vector3 value)
	{
		Write(value.X);
		Write(value.Y);
		Write(value.Z);
	}

	public void Write(Quaternion value)
	{
		Write(value.X);
		Write(value.Y);
		Write(value.Z);
		Write(value.W);
	}

	public byte ReadByte(bool moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			byte result = readableBuffer[readPos];
			if (moveReadPos)
			{
				readPos++;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'byte'!");
	}

	public byte[] ReadBytes(int length, bool moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			byte[] result = buffer.GetRange(readPos, length).ToArray();
			if (moveReadPos)
			{
				readPos += length;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'byte[]'!");
	}

	public short ReadShort(bool moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			short result = BitConverter.ToInt16(readableBuffer, readPos);
			if (moveReadPos)
			{
				readPos += 2;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'short'!");
	}

	public int ReadInt(bool moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			int result = BitConverter.ToInt32(readableBuffer, readPos);
			if (moveReadPos)
			{
				readPos += 4;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'int'!");
	}

	public long ReadLong(bool moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			long result = BitConverter.ToInt64(readableBuffer, readPos);
			if (moveReadPos)
			{
				readPos += 8;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'long'!");
	}

	public float ReadFloat(bool moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			float result = BitConverter.ToSingle(readableBuffer, readPos);
			if (moveReadPos)
			{
				readPos += 4;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'float'!");
	}

	public bool ReadBool(bool moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			bool result = BitConverter.ToBoolean(readableBuffer, readPos);
			if (moveReadPos)
			{
				readPos++;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'bool'!");
	}

	public string ReadString(bool moveReadPos = true)
	{
		if (buffer.Count <= readPos)
		{
			throw new Exception("Could not read value of type 'string'!");
		}

		try
		{
			int remaining = buffer.Count - readPos;

			// Format 1: 4-byte Int32 length prefix (used by WriteLegacyString)
			if (remaining >= 4)
			{
				int intLen = BitConverter.ToInt32(readableBuffer, readPos);
				if (intLen >= 0 && intLen <= (remaining - 4))
				{
					string s = Encoding.ASCII.GetString(readableBuffer, readPos + 4, intLen);
					if (moveReadPos)
					{
						readPos += 4 + intLen;
					}
					return s;
				}
			}

			// Format 2: 7-bit encoded int / 1-byte length prefix (standard .NET BinaryWriter.Write(string))
			int lebLen = 0;
			int shift = 0;
			int curPos = readPos;
			while (curPos < buffer.Count && shift < 32)
			{
				byte b = readableBuffer[curPos++];
				lebLen |= (b & 0x7F) << shift;
				if ((b & 0x80) == 0)
				{
					break;
				}
				shift += 7;
			}

			if (lebLen >= 0 && lebLen <= (buffer.Count - curPos))
			{
				string s = Encoding.UTF8.GetString(readableBuffer, curPos, lebLen);
				if (moveReadPos)
				{
					readPos = curPos + lebLen;
				}
				return s;
			}

			throw new Exception("String length out of bounds.");
		}
		catch
		{
			throw new Exception("Could not read value of type 'string'!");
		}
	}

	public Vector3 ReadVector3(bool moveReadPos = true)
	{
		return new Vector3(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
	}

	public Quaternion ReadQuaternion(bool moveReadPos = true)
	{
		return new Quaternion(ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos), ReadFloat(moveReadPos));
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposed)
		{
			if (disposing)
			{
				buffer = null;
				readableBuffer = null;
				readPos = 0;
			}
			disposed = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

