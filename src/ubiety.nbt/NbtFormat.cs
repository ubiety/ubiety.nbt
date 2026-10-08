namespace Ubiety.Nbt;

/// <summary>
/// The binary encoding used for NBT data.
/// </summary>
public enum NbtFormat
{
    /// <summary>Java Edition: big-endian numbers and Modified UTF-8 strings.</summary>
    JavaEdition,

    /// <summary>Bedrock Edition files (e.g. <c>level.dat</c> payloads): little-endian numbers and UTF-8 strings.</summary>
    BedrockEdition,

    /// <summary>
    /// Bedrock Edition network protocol: like <see cref="BedrockEdition"/>, but ints, longs and lengths are
    /// zig-zag encoded variable-length integers and string lengths are unsigned variable-length integers.
    /// </summary>
    BedrockNetwork,
}
