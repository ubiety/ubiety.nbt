using System.Buffers.Binary;
using System.Numerics;

namespace Ubiety.Nbt.IO;

/// <summary>
/// The 32-bit xxHash algorithm, used for LZ4 block checksums.
/// </summary>
internal static class XxHash32
{
    private const uint Prime1 = 2654435761U;
    private const uint Prime2 = 2246822519U;
    private const uint Prime3 = 3266489917U;
    private const uint Prime4 = 668265263U;
    private const uint Prime5 = 374761393U;

    public static uint Hash(ReadOnlySpan<byte> data, uint seed)
    {
        var i = 0;
        uint hash;

        if (data.Length >= 16)
        {
            var v1 = seed + Prime1 + Prime2;
            var v2 = seed + Prime2;
            var v3 = seed;
            var v4 = seed - Prime1;

            for (; i <= data.Length - 16; i += 16)
            {
                v1 = Round(v1, BinaryPrimitives.ReadUInt32LittleEndian(data[i..]));
                v2 = Round(v2, BinaryPrimitives.ReadUInt32LittleEndian(data[(i + 4)..]));
                v3 = Round(v3, BinaryPrimitives.ReadUInt32LittleEndian(data[(i + 8)..]));
                v4 = Round(v4, BinaryPrimitives.ReadUInt32LittleEndian(data[(i + 12)..]));
            }

            hash = BitOperations.RotateLeft(v1, 1) + BitOperations.RotateLeft(v2, 7) +
                   BitOperations.RotateLeft(v3, 12) + BitOperations.RotateLeft(v4, 18);
        }
        else
        {
            hash = seed + Prime5;
        }

        hash += (uint)data.Length;

        for (; i <= data.Length - 4; i += 4)
        {
            hash += BinaryPrimitives.ReadUInt32LittleEndian(data[i..]) * Prime3;
            hash = BitOperations.RotateLeft(hash, 17) * Prime4;
        }

        for (; i < data.Length; i++)
        {
            hash += data[i] * Prime5;
            hash = BitOperations.RotateLeft(hash, 11) * Prime1;
        }

        hash ^= hash >> 15;
        hash *= Prime2;
        hash ^= hash >> 13;
        hash *= Prime3;
        hash ^= hash >> 16;
        return hash;
    }

    private static uint Round(uint accumulator, uint input) =>
        BitOperations.RotateLeft(accumulator + (input * Prime2), 13) * Prime1;
}
