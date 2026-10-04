using System;
using System.Globalization;
using System.Text;

namespace OriCoopDedicatedServer.Core.API;

public struct Vector3 : IEquatable<Vector3>, IFormattable
{
	public float X;

	public float Y;

	public float Z;

	public static Vector3 Zero => default(Vector3);

	public static Vector3 One => new Vector3(1f, 1f, 1f);

	public static Vector3 UnitX => new Vector3(1f, 0f, 0f);

	public static Vector3 UnitY => new Vector3(0f, 1f, 0f);

	public static Vector3 UnitZ => new Vector3(0f, 0f, 1f);

	public override int GetHashCode()
	{
		int hashCode = X.GetHashCode();
		hashCode = HashHelpers.Combine(hashCode, Y.GetHashCode());
		return HashHelpers.Combine(hashCode, Z.GetHashCode());
	}

	public override bool Equals(object obj)
	{
		return obj is Vector3 && Equals((Vector3)obj);
	}

	public override string ToString()
	{
		return ToString("G", CultureInfo.CurrentCulture);
	}

	public string ToString(string format)
	{
		return ToString(format, CultureInfo.CurrentCulture);
	}

	public string ToString(string format, IFormatProvider formatProvider)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string numberGroupSeparator = NumberFormatInfo.GetInstance(formatProvider).NumberGroupSeparator;
		stringBuilder.Append('<');
		stringBuilder.Append(((IFormattable)X).ToString(format, formatProvider));
		stringBuilder.Append(numberGroupSeparator);
		stringBuilder.Append(' ');
		stringBuilder.Append(((IFormattable)Y).ToString(format, formatProvider));
		stringBuilder.Append(numberGroupSeparator);
		stringBuilder.Append(' ');
		stringBuilder.Append(((IFormattable)Z).ToString(format, formatProvider));
		stringBuilder.Append('>');
		return stringBuilder.ToString();
	}

	public float Length()
	{
		float x = X * X + Y * Y + Z * Z;
		return MathF.Sqrt(x);
	}

	public float LengthSquared()
	{
		return X * X + Y * Y + Z * Z;
	}

	public static float Distance(Vector3 value1, Vector3 value2)
	{
		float num = value1.X - value2.X;
		float num2 = value1.Y - value2.Y;
		float num3 = value1.Z - value2.Z;
		float x = num * num + num2 * num2 + num3 * num3;
		return MathF.Sqrt(x);
	}

	public static float DistanceSquared(Vector3 value1, Vector3 value2)
	{
		float num = value1.X - value2.X;
		float num2 = value1.Y - value2.Y;
		float num3 = value1.Z - value2.Z;
		return num * num + num2 * num2 + num3 * num3;
	}

	public static Vector3 Normalize(Vector3 value)
	{
		float x = value.X * value.X + value.Y * value.Y + value.Z * value.Z;
		float num = MathF.Sqrt(x);
		return new Vector3(value.X / num, value.Y / num, value.Z / num);
	}

	public static Vector3 Cross(Vector3 vector1, Vector3 vector2)
	{
		return new Vector3(vector1.Y * vector2.Z - vector1.Z * vector2.Y, vector1.Z * vector2.X - vector1.X * vector2.Z, vector1.X * vector2.Y - vector1.Y * vector2.X);
	}

	public static Vector3 Reflect(Vector3 vector, Vector3 normal)
	{
		float num = vector.X * normal.X + vector.Y * normal.Y + vector.Z * normal.Z;
		float num2 = normal.X * num * 2f;
		float num3 = normal.Y * num * 2f;
		float num4 = normal.Z * num * 2f;
		return new Vector3(vector.X - num2, vector.Y - num3, vector.Z - num4);
	}

	public static Vector3 Clamp(Vector3 value1, Vector3 min, Vector3 max)
	{
		float x = value1.X;
		x = ((min.X > x) ? min.X : x);
		x = ((max.X < x) ? max.X : x);
		float y = value1.Y;
		y = ((min.Y > y) ? min.Y : y);
		y = ((max.Y < y) ? max.Y : y);
		float z = value1.Z;
		z = ((min.Z > z) ? min.Z : z);
		z = ((max.Z < z) ? max.Z : z);
		return new Vector3(x, y, z);
	}

	public static Vector3 Lerp(Vector3 value1, Vector3 value2, float amount)
	{
		return new Vector3(value1.X + (value2.X - value1.X) * amount, value1.Y + (value2.Y - value1.Y) * amount, value1.Z + (value2.Z - value1.Z) * amount);
	}

	public static Vector3 Transform(Vector3 value, Quaternion rotation)
	{
		float num = rotation.X + rotation.X;
		float num2 = rotation.Y + rotation.Y;
		float num3 = rotation.Z + rotation.Z;
		float num4 = rotation.W * num;
		float num5 = rotation.W * num2;
		float num6 = rotation.W * num3;
		float num7 = rotation.X * num;
		float num8 = rotation.X * num2;
		float num9 = rotation.X * num3;
		float num10 = rotation.Y * num2;
		float num11 = rotation.Y * num3;
		float num12 = rotation.Z * num3;
		return new Vector3(value.X * (1f - num10 - num12) + value.Y * (num8 - num6) + value.Z * (num9 + num5), value.X * (num8 + num6) + value.Y * (1f - num7 - num12) + value.Z * (num11 - num4), value.X * (num9 - num5) + value.Y * (num11 + num4) + value.Z * (1f - num7 - num10));
	}

	public static Vector3 Add(Vector3 left, Vector3 right)
	{
		return left + right;
	}

	public static Vector3 Subtract(Vector3 left, Vector3 right)
	{
		return left - right;
	}

	public static Vector3 Multiply(Vector3 left, Vector3 right)
	{
		return left * right;
	}

	public static Vector3 Multiply(Vector3 left, float right)
	{
		return left * right;
	}

	public static Vector3 Multiply(float left, Vector3 right)
	{
		return left * right;
	}

	public static Vector3 Divide(Vector3 left, Vector3 right)
	{
		return left / right;
	}

	public static Vector3 Divide(Vector3 left, float divisor)
	{
		return left / divisor;
	}

	public static Vector3 Negate(Vector3 value)
	{
		return -value;
	}

	public Vector3(float value)
		: this(value, value, value)
	{
	}

	public Vector3(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	public void CopyTo(float[] array)
	{
		CopyTo(array, 0);
	}

	public void CopyTo(float[] array, int index)
	{
		if (array == null)
		{
			throw new NullReferenceException();
		}
		if (index < 0 || index >= array.Length)
		{
			throw new ArgumentOutOfRangeException($"index: {index}");
		}
		if (array.Length - index < 3)
		{
			throw new ArgumentException(index.ToString());
		}
		array[index] = X;
		array[index + 1] = Y;
		array[index + 2] = Z;
	}

	public bool Equals(Vector3 other)
	{
		return X == other.X && Y == other.Y && Z == other.Z;
	}

	public static float Dot(Vector3 vector1, Vector3 vector2)
	{
		return vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
	}

	public static Vector3 Min(Vector3 value1, Vector3 value2)
	{
		return new Vector3((value1.X < value2.X) ? value1.X : value2.X, (value1.Y < value2.Y) ? value1.Y : value2.Y, (value1.Z < value2.Z) ? value1.Z : value2.Z);
	}

	public static Vector3 Max(Vector3 value1, Vector3 value2)
	{
		return new Vector3((value1.X > value2.X) ? value1.X : value2.X, (value1.Y > value2.Y) ? value1.Y : value2.Y, (value1.Z > value2.Z) ? value1.Z : value2.Z);
	}

	public static Vector3 Abs(Vector3 value)
	{
		return new Vector3(MathF.Abs(value.X), MathF.Abs(value.Y), MathF.Abs(value.Z));
	}

	public static Vector3 SquareRoot(Vector3 value)
	{
		return new Vector3(MathF.Sqrt(value.X), MathF.Sqrt(value.Y), MathF.Sqrt(value.Z));
	}

	public static Vector3 operator +(Vector3 left, Vector3 right)
	{
		return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
	}

	public static Vector3 operator -(Vector3 left, Vector3 right)
	{
		return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
	}

	public static Vector3 operator *(Vector3 left, Vector3 right)
	{
		return new Vector3(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
	}

	public static Vector3 operator *(Vector3 left, float right)
	{
		return left * new Vector3(right);
	}

	public static Vector3 operator *(float left, Vector3 right)
	{
		return new Vector3(left) * right;
	}

	public static Vector3 operator /(Vector3 left, Vector3 right)
	{
		return new Vector3(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
	}

	public static Vector3 operator /(Vector3 value1, float value2)
	{
		return value1 / new Vector3(value2);
	}

	public static Vector3 operator -(Vector3 value)
	{
		return Zero - value;
	}

	public static bool operator ==(Vector3 left, Vector3 right)
	{
		return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
	}

	public static bool operator !=(Vector3 left, Vector3 right)
	{
		return left.X != right.X || left.Y != right.Y || left.Z != right.Z;
	}
}

