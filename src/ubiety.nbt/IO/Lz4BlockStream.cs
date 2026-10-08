using System.Buffers;
using System.Buffers.Binary;

namespace Ubiety.Nbt.IO;

/// <summary>
/// The framing written by lz4-java's <c>LZ4BlockOutputStream</c>, which Minecraft uses for LZ4 chunk compression.
/// </summary>
/// <remarks>
/// <para>
/// Each block is a 21-byte header followed by its data: the magic <c>LZ4Block</c>, a token byte (compression
/// method in the high nibble, log2 of the block size minus 10 in the low nibble), then the little-endian
/// compressed length, original length, and xxHash32 checksum of the original data. The checksum uses seed
/// <c>0x9747B28C</c> and is masked to 28 bits. A block with both lengths zero ends the stream.
/// </para>
/// <para>This is not the standard LZ4 frame format, which tools like the <c>lz4</c> CLI produce.</para>
/// </remarks>
internal static class Lz4BlockStream
{
    private const int HeaderLength = 21;
    private const byte MethodRaw = 0x10;
    private const byte MethodLz4 = 0x20;
    private const int LevelBase = 10;
    private const int BlockSize = 1 << 16;
    private const byte BlockSizeLevel = 16 - LevelBase;
    private const uint ChecksumSeed = 0x9747B28C;

    private static ReadOnlySpan<byte> Magic => "LZ4Block"u8;

    public static bool HasMagic(ReadOnlySpan<byte> data) => data.StartsWith(Magic);

    public static byte[] Decode(ReadOnlySpan<byte> data)
    {
        var output = new ArrayBufferWriter<byte>(Math.Max(data.Length * 2, 256));
        var position = 0;

        // lz4-java requires the empty end block, but tolerating its absence costs nothing.
        while (position < data.Length)
        {
            if (data.Length - position < HeaderLength || !data[position..].StartsWith(Magic))
            {
                throw Corrupt("bad block header");
            }

            var header = data.Slice(position, HeaderLength);
            var token = header[8];
            var method = (byte)(token & 0xF0);
            var blockSize = 1 << (LevelBase + (token & 0x0F));
            var compressedLength = BinaryPrimitives.ReadInt32LittleEndian(header[9..]);
            var originalLength = BinaryPrimitives.ReadInt32LittleEndian(header[13..]);
            var checksum = BinaryPrimitives.ReadInt32LittleEndian(header[17..]);
            position += HeaderLength;

            if (method is not (MethodRaw or MethodLz4) ||
                originalLength < 0 || originalLength > blockSize || compressedLength < 0 ||
                (originalLength == 0) != (compressedLength == 0) ||
                (method == MethodRaw && compressedLength != originalLength))
            {
                throw Corrupt("invalid block header");
            }

            if (originalLength == 0)
            {
                return checksum == 0 ? output.WrittenSpan.ToArray() : throw Corrupt("end block has a non-zero checksum");
            }

            if (data.Length - position < compressedLength)
            {
                throw Corrupt("truncated block");
            }

            var block = data.Slice(position, compressedLength);
            position += compressedLength;

            var destination = output.GetSpan(originalLength)[..originalLength];
            if (method == MethodRaw)
            {
                block.CopyTo(destination);
            }
            else if (Lz4Block.Decompress(block, destination) != originalLength)
            {
                throw Corrupt("block decompressed to the wrong length");
            }

            if (Checksum(destination) != checksum)
            {
                throw Corrupt("checksum mismatch");
            }

            output.Advance(originalLength);
        }

        return output.WrittenSpan.ToArray();
    }

    public static byte[] Encode(ReadOnlySpan<byte> data)
    {
        var output = new ArrayBufferWriter<byte>(data.Length + HeaderLength * 2);

        for (var position = 0; position < data.Length; position += BlockSize)
        {
            var block = data.Slice(position, Math.Min(BlockSize, data.Length - position));
            var span = output.GetSpan(HeaderLength + Lz4Block.MaxCompressedLength(block.Length));

            var method = MethodLz4;
            var compressedLength = Lz4Block.Compress(block, span[HeaderLength..]);
            if (compressedLength >= block.Length)
            {
                // Incompressible: store as-is, as lz4-java does.
                method = MethodRaw;
                compressedLength = block.Length;
                block.CopyTo(span[HeaderLength..]);
            }

            WriteHeader(span, method, compressedLength, block.Length, Checksum(block));
            output.Advance(HeaderLength + compressedLength);
        }

        WriteHeader(output.GetSpan(HeaderLength), MethodRaw, 0, 0, 0);
        output.Advance(HeaderLength);
        return output.WrittenSpan.ToArray();
    }

    private static void WriteHeader(Span<byte> span, byte method, int compressedLength, int originalLength, int checksum)
    {
        Magic.CopyTo(span);
        span[8] = (byte)(method | BlockSizeLevel);
        BinaryPrimitives.WriteInt32LittleEndian(span[9..], compressedLength);
        BinaryPrimitives.WriteInt32LittleEndian(span[13..], originalLength);
        BinaryPrimitives.WriteInt32LittleEndian(span[17..], checksum);
    }

    private static int Checksum(ReadOnlySpan<byte> data) => (int)(XxHash32.Hash(data, ChecksumSeed) & 0x0FFFFFFF);

    private static NbtFormatException Corrupt(string reason) => new($"The LZ4 chunk data is corrupt: {reason}.");
}
