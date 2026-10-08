using System.Buffers.Binary;

namespace Ubiety.Nbt.IO;

/// <summary>
/// The raw LZ4 block format (no framing): see <c>lz4_Block_format.md</c> in the LZ4 project.
/// </summary>
internal static class Lz4Block
{
    private const int MinMatch = 4;
    private const int LastLiterals = 5;
    private const int MatchFindLimit = 12;
    private const int MaxOffset = ushort.MaxValue;
    private const int HashLog = 12;

    /// <summary>Gets the largest possible compressed size of <paramref name="length"/> bytes.</summary>
    public static int MaxCompressedLength(int length) => length + (length / 255) + 16;

    /// <summary>Decompresses a block into <paramref name="destination"/>.</summary>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="NbtFormatException">The block is malformed or does not fit.</exception>
    public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var s = 0;
        var d = 0;
        while (true)
        {
            if (s >= source.Length)
            {
                throw Corrupt();
            }

            var token = source[s++];
            var literalLength = ReadLength(source, ref s, token >> 4, destination.Length);
            if (literalLength > source.Length - s || literalLength > destination.Length - d)
            {
                throw Corrupt();
            }

            source.Slice(s, literalLength).CopyTo(destination[d..]);
            s += literalLength;
            d += literalLength;

            // The last sequence holds only literals.
            if (s == source.Length)
            {
                return d;
            }

            if (source.Length - s < 2)
            {
                throw Corrupt();
            }

            var offset = BinaryPrimitives.ReadUInt16LittleEndian(source[s..]);
            s += 2;
            if (offset == 0 || offset > d)
            {
                throw Corrupt();
            }

            var matchLength = ReadLength(source, ref s, token & 0x0F, destination.Length) + MinMatch;
            if (matchLength > destination.Length - d)
            {
                throw Corrupt();
            }

            if (offset >= matchLength)
            {
                destination.Slice(d - offset, matchLength).CopyTo(destination[d..]);
                d += matchLength;
            }
            else
            {
                // Overlapping match: bytes repeat with period `offset`, so copy one at a time.
                for (var end = d + matchLength; d < end; d++)
                {
                    destination[d] = destination[d - offset];
                }
            }
        }
    }

    /// <summary>
    /// Compresses <paramref name="source"/> into <paramref name="destination"/>, which must hold at least
    /// <see cref="MaxCompressedLength"/> bytes.
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var d = 0;
        var anchor = 0;

        // Matches must start at least 12 bytes before the end and leave the last 5 bytes as literals.
        if (source.Length > MatchFindLimit)
        {
            Span<int> table = stackalloc int[1 << HashLog];
            table.Fill(-1);
            var matchLimit = source.Length - LastLiterals;
            var searchLimit = source.Length - MatchFindLimit;
            var p = 0;

            while (p <= searchLimit)
            {
                var sequence = BinaryPrimitives.ReadUInt32LittleEndian(source[p..]);
                var hash = (int)((sequence * 2654435761U) >> (32 - HashLog));
                var candidate = table[hash];
                table[hash] = p;

                if (candidate < 0 || p - candidate > MaxOffset ||
                    BinaryPrimitives.ReadUInt32LittleEndian(source[candidate..]) != sequence)
                {
                    p++;
                    continue;
                }

                var length = MinMatch;
                while (p + length < matchLimit && source[candidate + length] == source[p + length])
                {
                    length++;
                }

                d = WriteSequence(source[anchor..p], p - candidate, length, destination, d);
                p += length;
                anchor = p;
            }
        }

        var literals = source[anchor..];
        d = WriteToken(destination, d, literals.Length, 0);
        literals.CopyTo(destination[d..]);
        return d + literals.Length;
    }

    private static int ReadLength(ReadOnlySpan<byte> source, ref int s, int length, int limit)
    {
        if (length != 15)
        {
            return length;
        }

        byte b;
        do
        {
            if (s >= source.Length)
            {
                throw Corrupt();
            }

            b = source[s++];
            length += b;
            if (length > limit)
            {
                throw Corrupt();
            }
        }
        while (b == byte.MaxValue);

        return length;
    }

    private static int WriteSequence(ReadOnlySpan<byte> literals, int offset, int matchLength, Span<byte> destination, int d)
    {
        d = WriteToken(destination, d, literals.Length, matchLength - MinMatch);
        literals.CopyTo(destination[d..]);
        d += literals.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[d..], (ushort)offset);
        d += 2;
        return WriteExtraLength(destination, d, matchLength - MinMatch);
    }

    private static int WriteToken(Span<byte> destination, int d, int literalLength, int matchCode)
    {
        destination[d++] = (byte)((Math.Min(literalLength, 15) << 4) | Math.Min(matchCode, 15));
        return WriteExtraLength(destination, d, literalLength);
    }

    private static int WriteExtraLength(Span<byte> destination, int d, int length)
    {
        if (length < 15)
        {
            return d;
        }

        for (length -= 15; length >= byte.MaxValue; length -= byte.MaxValue)
        {
            destination[d++] = byte.MaxValue;
        }

        destination[d++] = (byte)length;
        return d;
    }

    private static NbtFormatException Corrupt() => new("The LZ4 data is corrupt.");
}
