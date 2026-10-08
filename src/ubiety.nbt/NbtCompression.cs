namespace Ubiety.Nbt;

/// <summary>
/// The compression applied to an NBT file.
/// </summary>
public enum NbtCompression
{
    /// <summary>Uncompressed.</summary>
    None,

    /// <summary>GZip (RFC 1952), used by most Java Edition files such as <c>level.dat</c>.</summary>
    GZip,

    /// <summary>ZLib (RFC 1950), used by Java Edition region file chunks.</summary>
    ZLib,
}
