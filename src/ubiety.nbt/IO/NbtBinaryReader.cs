using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Ubiety.Nbt.IO;

/// <summary>
/// Reads NBT tags from an uncompressed binary stream.
/// </summary>
/// <remarks>
/// Use <see cref="NbtFile"/> for files, which also handles compression.
/// Use this class directly for network data or other embedded NBT.
/// </remarks>
public sealed class NbtBinaryReader : IDisposable
{
    /// <summary>The default maximum nesting depth of lists and compounds, matching Minecraft.</summary>
    public const int DefaultMaxDepth = 512;

    // Lengths above this are read incrementally, so a corrupt or hostile length prefix can't
    // force a huge allocation before the data is known to exist.
    private const int MaxUpfrontAllocation = 1 << 20;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    /// <summary>Initializes a new instance of the <see cref="NbtBinaryReader"/> class.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="format">The binary format of the data.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="stream"/> open when the reader is disposed.</param>
    public NbtBinaryReader(Stream stream, NbtFormat format = NbtFormat.JavaEdition, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown NBT format.");
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
        Format = format;
    }

    /// <summary>Gets the binary format being read.</summary>
    public NbtFormat Format { get; }

    /// <summary>Gets the maximum nesting depth of lists and compounds. Deeper data is rejected.</summary>
    public int MaxDepth { get; init; } = DefaultMaxDepth;

    /// <summary>Reads a named tag: a type byte, a name, then the payload. This is how NBT files are stored.</summary>
    /// <param name="name">The tag's name.</param>
    /// <returns>The tag.</returns>
    /// <exception cref="NbtFormatException">The data is malformed or truncated.</exception>
    public NbtTag ReadTag(out string name)
    {
        try
        {
            var type = ReadRootType();
            name = ReadString();
            return ReadPayload(type, 0);
        }
        catch (EndOfStreamException e)
        {
            throw new NbtFormatException("Unexpected end of NBT data.", e);
        }
    }

    /// <summary>
    /// Reads a tag without a name: a type byte then the payload. Java Edition uses this on the network since 1.20.2.
    /// </summary>
    /// <returns>The tag.</returns>
    /// <exception cref="NbtFormatException">The data is malformed or truncated.</exception>
    public NbtTag ReadNamelessTag()
    {
        try
        {
            return ReadPayload(ReadRootType(), 0);
        }
        catch (EndOfStreamException e)
        {
            throw new NbtFormatException("Unexpected end of NBT data.", e);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private NbtTagType ReadRootType()
    {
        var type = ReadTagType();
        return type == NbtTagType.End ? throw new NbtFormatException("The root tag cannot be TAG_End.") : type;
    }

    private NbtTag ReadPayload(NbtTagType type, int depth) => type switch
    {
        NbtTagType.Byte => new NbtByte(ReadByte()),
        NbtTagType.Short => new NbtShort(ReadInt16()),
        NbtTagType.Int => new NbtInt(ReadInt32()),
        NbtTagType.Long => new NbtLong(ReadInt64()),
        NbtTagType.Float => new NbtFloat(ReadSingle()),
        NbtTagType.Double => new NbtDouble(ReadDouble()),
        NbtTagType.ByteArray => new NbtByteArray(ReadBytes(ReadLength())),
        NbtTagType.String => new NbtString(ReadString()),
        NbtTagType.List => ReadList(depth),
        NbtTagType.Compound => ReadCompound(depth),
        NbtTagType.IntArray => new NbtIntArray(ReadInt32Array()),
        NbtTagType.LongArray => new NbtLongArray(ReadInt64Array()),
        _ => throw new NbtFormatException($"Unexpected tag type {type}."),
    };

    private NbtList ReadList(int depth)
    {
        CheckDepth(depth);
        var elementType = ReadTagType();
        var count = ReadLength();
        if (elementType == NbtTagType.End && count > 0)
        {
            throw new NbtFormatException("A non-empty list has element type TAG_End.");
        }

        var list = new NbtList(elementType);
        for (var i = 0; i < count; i++)
        {
            list.Add(ReadPayload(elementType, depth + 1));
        }

        return list;
    }

    private NbtCompound ReadCompound(int depth)
    {
        CheckDepth(depth);
        var compound = new NbtCompound();
        while (true)
        {
            var type = ReadTagType();
            if (type == NbtTagType.End)
            {
                return compound;
            }

            var name = ReadString();

            // Duplicate names are invalid, but Minecraft keeps the last one, so we do too.
            compound[name] = ReadPayload(type, depth + 1);
        }
    }

    private void CheckDepth(int depth)
    {
        if (depth >= MaxDepth)
        {
            throw new NbtFormatException($"NBT data is nested deeper than the maximum depth of {MaxDepth}.");
        }
    }

    private NbtTagType ReadTagType()
    {
        var value = ReadByte();
        return value <= (byte)NbtTagType.LongArray
            ? (NbtTagType)value
            : throw new NbtFormatException($"Unknown tag type {value}.");
    }

    private int ReadLength()
    {
        var length = ReadInt32();
        return length >= 0 ? length : throw new NbtFormatException($"Negative length {length}.");
    }

    private byte ReadByte()
    {
        var value = _stream.ReadByte();
        return value >= 0 ? (byte)value : throw new EndOfStreamException();
    }

    private short ReadInt16()
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        _stream.ReadExactly(buffer);
        return Format == NbtFormat.JavaEdition
            ? BinaryPrimitives.ReadInt16BigEndian(buffer)
            : BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    private int ReadInt32()
    {
        if (Format == NbtFormat.BedrockNetwork)
        {
            var zigZag = (uint)ReadVarUInt(5);
            return (int)(zigZag >> 1) ^ -(int)(zigZag & 1);
        }

        Span<byte> buffer = stackalloc byte[sizeof(int)];
        _stream.ReadExactly(buffer);
        return Format == NbtFormat.JavaEdition
            ? BinaryPrimitives.ReadInt32BigEndian(buffer)
            : BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    private long ReadInt64()
    {
        if (Format == NbtFormat.BedrockNetwork)
        {
            var zigZag = ReadVarUInt(10);
            return (long)(zigZag >> 1) ^ -(long)(zigZag & 1);
        }

        Span<byte> buffer = stackalloc byte[sizeof(long)];
        _stream.ReadExactly(buffer);
        return Format == NbtFormat.JavaEdition
            ? BinaryPrimitives.ReadInt64BigEndian(buffer)
            : BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    private float ReadSingle()
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        _stream.ReadExactly(buffer);
        return Format == NbtFormat.JavaEdition
            ? BinaryPrimitives.ReadSingleBigEndian(buffer)
            : BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    private double ReadDouble()
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        _stream.ReadExactly(buffer);
        return Format == NbtFormat.JavaEdition
            ? BinaryPrimitives.ReadDoubleBigEndian(buffer)
            : BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    private ulong ReadVarUInt(int maxBytes)
    {
        ulong result = 0;
        for (var shift = 0; shift < maxBytes * 7; shift += 7)
        {
            var b = ReadByte();
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }
        }

        throw new NbtFormatException("Variable-length integer is too long.");
    }

    private string ReadString()
    {
        int length;
        if (Format == NbtFormat.BedrockNetwork)
        {
            var value = (uint)ReadVarUInt(5);
            length = value <= short.MaxValue ? (int)value : throw new NbtFormatException($"String length {value} is too long.");
        }
        else
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            _stream.ReadExactly(buffer);
            length = Format == NbtFormat.JavaEdition
                ? BinaryPrimitives.ReadUInt16BigEndian(buffer)
                : BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        var bytes = ReadBytes(length);
        return Format == NbtFormat.JavaEdition ? ModifiedUtf8.GetString(bytes) : Encoding.UTF8.GetString(bytes);
    }

    private byte[] ReadBytes(int count)
    {
        if (count <= MaxUpfrontAllocation)
        {
            var bytes = new byte[count];
            _stream.ReadExactly(bytes);
            return bytes;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (count > 0)
        {
            var read = _stream.Read(chunk, 0, Math.Min(chunk.Length, count));
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            buffer.Write(chunk, 0, read);
            count -= read;
        }

        return buffer.ToArray();
    }

    private int[] ReadInt32Array()
    {
        var count = ReadLength();
        if (Format == NbtFormat.BedrockNetwork)
        {
            var values = new List<int>(Math.Min(count, 4096));
            for (var i = 0; i < count; i++)
            {
                values.Add(ReadInt32());
            }

            return [.. values];
        }

        if (count > Array.MaxLength / sizeof(int))
        {
            throw new NbtFormatException($"Int array length {count} is too large.");
        }

        var bytes = ReadBytes(count * sizeof(int));
        var result = new int[count];
        var source = MemoryMarshal.Cast<byte, int>(bytes);
        if (IsNativeByteOrder)
        {
            source.CopyTo(result);
        }
        else
        {
            BinaryPrimitives.ReverseEndianness(source, result);
        }

        return result;
    }

    private long[] ReadInt64Array()
    {
        var count = ReadLength();
        if (Format == NbtFormat.BedrockNetwork)
        {
            var values = new List<long>(Math.Min(count, 4096));
            for (var i = 0; i < count; i++)
            {
                values.Add(ReadInt64());
            }

            return [.. values];
        }

        if (count > Array.MaxLength / sizeof(long))
        {
            throw new NbtFormatException($"Long array length {count} is too large.");
        }

        var bytes = ReadBytes(count * sizeof(long));
        var result = new long[count];
        var source = MemoryMarshal.Cast<byte, long>(bytes);
        if (IsNativeByteOrder)
        {
            source.CopyTo(result);
        }
        else
        {
            BinaryPrimitives.ReverseEndianness(source, result);
        }

        return result;
    }

    private bool IsNativeByteOrder => BitConverter.IsLittleEndian == (Format != NbtFormat.JavaEdition);
}
