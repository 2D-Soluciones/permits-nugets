using System.Buffers.Binary;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Metodos de extension para trabajar con arreglos codificados en base62.
/// </summary>
[PublicAPI]
public static class BaseNExtensions
{
	private const int BASE32_LENGTH = 13;

	private const int BASE36_WORD_BASE = 16777216;
	private const int BASE62_CHAR_COUNT = 62;
	private const string BASE62_STRING_TABLE = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
	private const int BLOCK_BITS_COUNT = 29;
	private const int BLOCK_CHARS_COUNT = 5;

	private static readonly char[] _digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'];

	private static readonly char[] _encode32Chars =
	[
		'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
		'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k',
		'm', 'n', 'p', 'q', 'r', 's', 't', 'v', 'w', 'x', 'y', 'z'
	];

	private static readonly char[] _encode36Chars =
	[
		'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
		'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
		'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
		'u', 'v', 'w', 'x', 'y', 'z'
	];

	private static readonly int[] _invAlphabet = new int[123];
	private static readonly ulong[] _powN = [14776336, 238328, 3844, 62, 1];

	static BaseNExtensions()
	{
		for (var i = 0; i < _invAlphabet.Length; i++)
		{
			_invAlphabet[i] = -1;
		}

		for (var i = 0; i < BASE62_CHAR_COUNT; i++)
		{
			_invAlphabet[BASE62_STRING_TABLE[i]] = i;
		}
	}





	/// <summary>
	///     Codifica en base36 el <see cref="long" /> que se le pasa.
	/// </summary>
	/// <param name="value"></param>
	/// <returns>Una cadena de 13 caracteres con el <paramref name="value" /> codificado.</returns>
	public static string ToBase36(this long value)
	{
		return ToBase36((ulong) value);
	}

	/// <summary>
	///     Codifica en base36 el <see cref="ulong" /> que se le pasa.
	/// </summary>
	/// <param name="value">El valor a codificar.</param>
	/// <returns>Una cadena de 13 caracteres con el <paramref name="value" /> codificado.</returns>
	public static string ToBase36(this ulong value)
	{
		const int LONG_BUFFER_LENGTH = 13;

		return string.Create(LONG_BUFFER_LENGTH, value, (buffer, number) =>
		{
			Span<byte> dividend = stackalloc byte[8];

			dividend[7] = (byte) (number >> 56);
			dividend[6] = (byte) (number >> 48);
			dividend[5] = (byte) (number >> 40);
			dividend[4] = (byte) (number >> 32);
			dividend[3] = (byte) (number >> 24);
			dividend[2] = (byte) (number >> 16);
			dividend[1] = (byte) (number >> 8);
			dividend[0] = (byte) number;

			var resultIndex = LONG_BUFFER_LENGTH - 1;

			while (!IsZero(dividend))
			{
				var remainder = DivideBy36(dividend);
				buffer[resultIndex--] = _encode36Chars[remainder];
			}

			// Relleno con ceros
			while (resultIndex >= 0)
			{
				buffer[resultIndex--] = '0';
			}
		});
	}

	/// <summary>
	///     Codifica en base36 el <see cref="Guid" /> que se le pasa.
	/// </summary>
	/// <param name="value">El valor a codificar.</param>
	/// <returns>Una cadena de 25 caracteres con el <paramref name="value" /> codificado.</returns>
	public static string ToBase36(this Guid value)
	{
		Span<char> result = stackalloc char[25];
		Span<byte> buffer = stackalloc byte[16];

		value.TryWriteBytes(buffer);

		Span<byte> dividend = stackalloc byte[16];
		dividend[15] = buffer[3];
		dividend[14] = buffer[2];
		dividend[13] = buffer[1];
		dividend[12] = buffer[0];
		dividend[11] = buffer[5];
		dividend[10] = buffer[4];
		dividend[9] = buffer[7];
		dividend[8] = buffer[6];
		dividend[7] = buffer[8];
		dividend[6] = buffer[9];
		dividend[5] = buffer[10];
		dividend[4] = buffer[11];
		dividend[3] = buffer[12];
		dividend[2] = buffer[13];
		dividend[1] = buffer[14];
		dividend[0] = buffer[15];

		var resultIndex = 24;

		while (!IsZero(dividend))
		{
			var remainder = DivideBy36(dividend);
			result[resultIndex--] = _encode36Chars[remainder];
		}

		while (resultIndex >= 0)
		{
			result[resultIndex--] = '0';
		}

		return new(result);
	}


	// ReSharper disable once SuggestBaseTypeForParameter
	private static void AddBits64(byte[] data, ulong value, int bitPos, int bitsCount)
	{
		unchecked
		{
			var currentBytePos = bitPos / 8;
			var currentBitInBytePos = bitPos % 8;

			var xLength = Math.Min(bitsCount, 8 - currentBitInBytePos);

			if (xLength == 0)
			{
				return;
			}

			var x1 = (byte) ((value << (64 - bitsCount)) >> (56 + currentBitInBytePos));
			data[currentBytePos] |= x1;

			currentBytePos += (currentBitInBytePos + xLength) / 8;
			currentBitInBytePos = (currentBitInBytePos + xLength) % 8;

			var x2Length = bitsCount - xLength;

			if (x2Length > 8)
			{
				x2Length = 8;
			}

			while (x2Length > 0)
			{
				xLength += x2Length;
				var x2 = (byte) ((value >> (bitsCount - xLength)) << (8 - x2Length));
				data[currentBytePos] |= x2;

				currentBytePos += (currentBitInBytePos + x2Length) / 8;
				currentBitInBytePos = (currentBitInBytePos + x2Length) % 8;

				x2Length = bitsCount - xLength;

				if (x2Length > 8)
				{
					x2Length = 8;
				}
			}
		}
	}

	// ReSharper disable once SuggestBaseTypeForParameter
	private static void BitsToChars(char[] chars, int ind, int count, ulong block)
	{
		for (var i = 0; i < count; i++)
		{
			chars[ind + i] = BASE62_STRING_TABLE[(int) (block % BASE62_CHAR_COUNT)];
			block /= BASE62_CHAR_COUNT;
		}
	}

	private static ulong CharsToBits(string data, int ind, int count)
	{
		if (ind + count > data.Length)
		{
			//las longitudes salen de una cuenta aritmetica sobre data.Length. Un largo que ningun encoding valido
			//produce -o un caracter corrupto que empuja el recalculo del tail- pedia leer mas alla del final, y salia
			//un IndexOutOfRangeException en vez de "esto no es base62".
			throw new ArgumentException($"'{data}' is not a valid Base62 string.", nameof(data));
		}

		ulong result = 0;

		for (var i = 0; i < count; i++)
		{
			var ch = data[ind + i];

			if (ch >= _invAlphabet.Length)
			{
				throw new ArgumentException($"Invalid Base62 character '{ch}' at position {ind + i}.");
			}

			var val = _invAlphabet[ch];

			if (val < 0)
			{
				throw new ArgumentException($"Invalid Base62 character '{ch}' at position {ind + i}.");
			}

			result += (ulong) val * _powN[BLOCK_CHARS_COUNT - 1 - i];
		}

		return result;
	}

	private static void DecodeBlock(string src, byte[] dst, int beginInd, int endInd)
	{
		for (var ind = beginInd; ind < endInd; ind++)
		{
			var charInd = ind * BLOCK_CHARS_COUNT;
			var bitInd = ind * BLOCK_BITS_COUNT;
			var bits = CharsToBits(src, charInd, BLOCK_CHARS_COUNT);
			AddBits64(dst, bits, bitInd, BLOCK_BITS_COUNT);
		}
	}

	private static byte DivideBy36(Span<byte> bytes)
	{
		uint remainder = 0;

		for (var i = bytes.Length - 1; i >= 0; i--)
		{
			var temp = remainder * 256 + bytes[i];
			bytes[i] = (byte) (temp / 36);
			remainder = temp % 36;
		}

		return (byte) remainder;
	}

	// ReSharper disable once SuggestBaseTypeForParameter
	private static ulong GetBits64(byte[] data, int bitPos, int bitsCount)
	{
		ulong result = 0;

		var currentBytePos = bitPos / 8;
		var currentBitInBytePos = bitPos % 8;
		var xLength = Math.Min(bitsCount, 8 - currentBitInBytePos);

		if (xLength == 0)
		{
			return result;
		}

		result = (((ulong) data[currentBytePos] << (56 + currentBitInBytePos)) >> (64 - xLength)) << (bitsCount - xLength);

		currentBytePos += (currentBitInBytePos + xLength) / 8;
		currentBitInBytePos = (currentBitInBytePos + xLength) % 8;

		var x2Length = bitsCount - xLength;

		if (x2Length > 8)
		{
			x2Length = 8;
		}

		while (x2Length > 0)
		{
			xLength += x2Length;
			result |= ((ulong) data[currentBytePos] >> (8 - x2Length)) << (bitsCount - xLength);

			currentBytePos += (currentBitInBytePos + x2Length) / 8;
			currentBitInBytePos = (currentBitInBytePos + x2Length) % 8;

			x2Length = bitsCount - xLength;

			if (x2Length > 8)
			{
				x2Length = 8;
			}
		}

		return result;
	}

	private static bool IsZero(Span<byte> bytes)
	{
		return bytes.IndexOfAnyExcept((byte) 0) < 0;
	}
}
