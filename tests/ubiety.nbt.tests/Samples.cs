using Ubiety.Nbt;

namespace Ubiety.Nbt.Tests;

internal static class Samples
{
    /// <summary>The classic "hello_world.nbt" test file from the original NBT specification.</summary>
    public static readonly byte[] HelloWorld =
    [
        0x0A, 0x00, 0x0B, 0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F, 0x72, 0x6C, 0x64,
        0x08, 0x00, 0x04, 0x6E, 0x61, 0x6D, 0x65, 0x00, 0x09, 0x42, 0x61, 0x6E, 0x61, 0x6E, 0x72, 0x61, 0x6D, 0x61,
        0x00,
    ];

    /// <summary>A compound containing every tag type, including edge-case values.</summary>
    public static NbtCompound AllTypes() => new()
    {
        ["byte"] = (byte)0xFF,
        ["bool"] = true,
        ["short"] = short.MinValue,
        ["int"] = -123456,
        ["long"] = long.MaxValue,
        ["float"] = 0.1f,
        ["double"] = -1e300,
        ["string"] = "Hello, 世界 \U0001F600 with a \0 null and \"quotes\"",
        ["empty string"] = string.Empty,
        ["byteArray"] = new byte[] { 0, 1, 127, 128, 255 },
        ["intArray"] = new[] { int.MinValue, -1, 0, 1, int.MaxValue },
        ["longArray"] = new[] { long.MinValue, -1L, 0L, 1L, long.MaxValue },
        ["emptyList"] = new NbtList(),
        ["intList"] = new NbtList { 1, 2, 3 },
        ["listOfLists"] = new NbtList { new NbtList { "a" }, new NbtList { 1.5 } },
        ["compoundList"] = new NbtList { new NbtCompound { ["id"] = "minecraft:stone", ["count"] = (byte)64 } },
        ["nested"] = new NbtCompound { ["inner"] = new NbtCompound { ["deep"] = 42L } },
    };
}
