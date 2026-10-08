namespace Ubiety.Nbt;

/// <summary>
/// The type identifier of an NBT tag, as written in the binary format.
/// </summary>
public enum NbtTagType : byte
{
    /// <summary>Marks the end of a compound; also the element type of an empty list.</summary>
    End = 0,

    /// <summary>A signed 8-bit integer.</summary>
    Byte = 1,

    /// <summary>A signed 16-bit integer.</summary>
    Short = 2,

    /// <summary>A signed 32-bit integer.</summary>
    Int = 3,

    /// <summary>A signed 64-bit integer.</summary>
    Long = 4,

    /// <summary>A 32-bit IEEE 754 floating point number.</summary>
    Float = 5,

    /// <summary>A 64-bit IEEE 754 floating point number.</summary>
    Double = 6,

    /// <summary>An array of bytes.</summary>
    ByteArray = 7,

    /// <summary>A string.</summary>
    String = 8,

    /// <summary>A list of unnamed tags that all share one type.</summary>
    List = 9,

    /// <summary>A collection of named tags.</summary>
    Compound = 10,

    /// <summary>An array of 32-bit integers.</summary>
    IntArray = 11,

    /// <summary>An array of 64-bit integers.</summary>
    LongArray = 12,
}
