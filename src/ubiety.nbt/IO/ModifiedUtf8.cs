namespace Ubiety.Nbt.IO;

/// <summary>
/// Java's "Modified UTF-8" string encoding (see <c>java.io.DataInput</c>): U+0000 is written as two bytes and
/// supplementary characters are written as two three-byte surrogates.
/// </summary>
internal static class ModifiedUtf8
{
    private const int StackAllocThreshold = 256;

    public static byte[] GetBytes(string value)
    {
        var length = 0;
        foreach (var c in value)
        {
            length += c switch
            {
                >= '\u0001' and <= '\u007F' => 1,
                <= '߿' => 2,
                _ => 3,
            };
        }

        if (length > ushort.MaxValue)
        {
            throw new NbtFormatException($"String is too long to encode ({length} bytes; the maximum is {ushort.MaxValue}).");
        }

        var bytes = new byte[length];
        var i = 0;
        foreach (var c in value)
        {
            if (c is >= '\u0001' and <= '\u007F')
            {
                bytes[i++] = (byte)c;
            }
            else if (c <= '߿')
            {
                bytes[i++] = (byte)(0xC0 | (c >> 6));
                bytes[i++] = (byte)(0x80 | (c & 0x3F));
            }
            else
            {
                bytes[i++] = (byte)(0xE0 | (c >> 12));
                bytes[i++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                bytes[i++] = (byte)(0x80 | (c & 0x3F));
            }
        }

        return bytes;
    }

    public static string GetString(ReadOnlySpan<byte> bytes)
    {
        // Every byte decodes to at most one UTF-16 code unit, except 4-byte sequences, which decode to two.
        Span<char> chars = bytes.Length <= StackAllocThreshold ? stackalloc char[bytes.Length] : new char[bytes.Length];
        var count = 0;

        for (var i = 0; i < bytes.Length;)
        {
            int b = bytes[i];
            if (b < 0x80)
            {
                chars[count++] = (char)b;
                i++;
            }
            else if ((b & 0xE0) == 0xC0)
            {
                var b2 = Continuation(bytes, i + 1);
                chars[count++] = (char)(((b & 0x1F) << 6) | b2);
                i += 2;
            }
            else if ((b & 0xF0) == 0xE0)
            {
                var b2 = Continuation(bytes, i + 1);
                var b3 = Continuation(bytes, i + 2);
                chars[count++] = (char)(((b & 0x0F) << 12) | (b2 << 6) | b3);
                i += 3;
            }
            else if ((b & 0xF8) == 0xF0)
            {
                // Not valid Modified UTF-8, but some tools write standard UTF-8; accept it leniently.
                var codePoint = ((b & 0x07) << 18) | (Continuation(bytes, i + 1) << 12) |
                                (Continuation(bytes, i + 2) << 6) | Continuation(bytes, i + 3);
                if (codePoint is < 0x10000 or > 0x10FFFF)
                {
                    throw Malformed();
                }

                codePoint -= 0x10000;
                chars[count++] = (char)(0xD800 + (codePoint >> 10));
                chars[count++] = (char)(0xDC00 + (codePoint & 0x3FF));
                i += 4;
            }
            else
            {
                throw Malformed();
            }
        }

        return new string(chars[..count]);
    }

    private static int Continuation(ReadOnlySpan<byte> bytes, int index)
    {
        if (index >= bytes.Length || (bytes[index] & 0xC0) != 0x80)
        {
            throw Malformed();
        }

        return bytes[index] & 0x3F;
    }

    private static NbtFormatException Malformed() => new("Malformed Modified UTF-8 string.");
}
