using System.Collections.Frozen;

namespace DDS.General.Logging.Javascript.SourceMaps.Util;

internal static class Base64
{
	private const string BASE64_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
	private static readonly FrozenDictionary<char, byte> _decodeTable = GetDecodeTable();

	public static byte Decode(char value)
	{
		return _decodeTable.TryGetValue(value, out var toReturn) ? toReturn : throw new FormatException($"Invalid base64 digit: '{value}'.");
	}

	private static FrozenDictionary<char, byte> GetDecodeTable()
	{
		var length = BASE64_ALPHABET.Length;
		var table = new Dictionary<char, byte>(length);

		for (var idx = 0; idx < length; idx++)
		{
			table[BASE64_ALPHABET[idx]] = (byte) idx;
		}

		return table.ToFrozenDictionary();
	}
}
