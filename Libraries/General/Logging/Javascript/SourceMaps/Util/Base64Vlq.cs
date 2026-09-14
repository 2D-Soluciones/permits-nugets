namespace DDS.General.Logging.Javascript.SourceMaps.Util;

internal static class Base64Vlq
{
	private const int VLQ_BASE = 1 << VLQ_BASE_SHIFT;
	private const int VLQ_BASE_MASK = VLQ_BASE - 1;
	private const int VLQ_BASE_SHIFT = 5;
	private const int VLQ_CONTINUATION_BIT = VLQ_BASE;

	public static int Decode(string str, ref int index)
	{
		var strLen = str.Length;
		var result = 0;
		var shift = 0;
		bool continuation;

		do
		{
			if (index >= strLen)
			{
				throw new("Expected more digits in base 64 VLQ value.");
			}

			var digit = Base64.Decode(str[index++]);
			continuation = (digit & VLQ_CONTINUATION_BIT) == VLQ_CONTINUATION_BIT;
			digit &= VLQ_BASE_MASK;

			//sin este tope, un VLQ con muchos digitos de continuacion desborda el int -en C# el shift se toma modulo
			//32- y devuelve numeros de linea inventados en vez de avisar que el mapa esta roto.
			if (shift >= 32)
			{
				throw new FormatException("Base 64 VLQ value is too large.");
			}

			result += digit << shift;
			shift += VLQ_BASE_SHIFT;
		} while (continuation);

		return FromVlqSigned(result);
	}

	private static int FromVlqSigned(int value)
	{
		var isNegative = (value & 1) == 1;
		var shifted = value >> 1;

		return isNegative
			? -shifted
			: shifted;
	}
}