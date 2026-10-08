using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Ubiety.Nbt.IO;

/// <summary>
/// Writes NBT tags to an uncompressed binary stream.
/// </summary>
/// <remarks>
/// Use <see cref="NbtFile"/> for files, which also handles compression.
/// Use this class directly for network data or other embedded NBT.
/// </remarks>
public sealed class NbtBinaryWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    /// <summary>Initializes a new instance of the <see cref="NbtBinaryWriter"/> class.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="format">The binary format to write.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="stream"/> open when the writer is disposed.</param>
    public NbtBinaryWriter(Stream stream, NbtFormat format = NbtFormat.JavaEdition, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite)
        {
            throw new ArgumentException("The stream must be writable.", nameof(stream));
        }

        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown NBT format.");
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
        Format = format;
    }

    /// <summary>Gets the binary format being written.</summary>
    public NbtFormat Format { get; }

    /// <summary>
    /// Gets the maximum nesting depth of lists and compounds. Deeper (or cyclic) tag trees are rejected.
    /// </summary>
    public int MaxDepth { get; init; } = NbtBinaryReader.DefaultMaxDepth;

    /// <summary>Writes a named tag: a type byte, a name, then the payload. This is how NBT files are stored.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="name">The tag's name. Usually empty for file roots.</param>
    /// <exception cref="NbtFormatException">The tag cannot be encoded, e.g. a string is too long.</exception>
    public void WriteTag(NbtTag tag, string name = "")
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(name);
        WriteByte((byte)tag.TagType);
        WriteString(name);
        WritePayload(tag, 0);
    }

    /// <summary>
    /// Writes a tag without a name: a type byte then the payload. Java Edition uses this on the network since 1.20.2.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <exception cref="NbtFormatException">The tag cannot be encoded, e.g. a string is too long.</exception>
    public void WriteNamelessTag(NbtTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        WriteByte((byte)tag.TagType);
        WritePayload(tag, 0);
    }

    /// <summary>Flushes the underlying stream.</summary>
    public void Flush() => _stream.Flush();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private void WritePayload(NbtTag tag, int depth)
    {
        switch (tag)
        {
            case NbtByte t:
                WriteByte(t.Value);
                break;
            case NbtShort t:
                WriteInt16(t.Value);
                break;
            case NbtInt t:
                WriteInt32(t.Value);
                break;
            case NbtLong t:
                WriteInt64(t.Value);
                break;
            case NbtFloat t:
                WriteSingle(t.Value);
                break;
            case NbtDouble t:
                WriteDouble(t.Value);
                break;
            case NbtString t:
                WriteString(t.Value);
                break;
            case NbtByteArray t:
                WriteInt32(t.Value.Length);
                _stream.Write(t.Value);
                break;
            case NbtIntArray t:
                WriteInt32Array(t.Value);
                break;
            case NbtLongArray t:
                WriteInt64Array(t.Value);
                break;
            case NbtList t:
                CheckDepth(depth);
                WriteByte((byte)t.ElementType);
                WriteInt32(t.Count);
                foreach (var item in t)
                {
                    WritePayload(item, depth + 1);
                }

                break;
            case NbtCompound t:
                CheckDepth(depth);
                foreach (var (name, child) in t)
                {
                    WriteByte((byte)child.TagType);
                    WriteString(name);
                    WritePayload(child, depth + 1);
                }

                WriteByte((byte)NbtTagType.End);
                break;
            default:
                throw new NbtFormatException($"Unsupported tag type {tag.GetType()}.");
        }
    }

    private void CheckDepth(int depth)
    {
        if (depth >= MaxDepth)
        {
            throw new NbtFormatException($"The tag tree is nested deeper than the maximum depth of {MaxDepth}. Does it contain a cycle?");
        }
    }

    private void WriteByte(byte value) => _stream.WriteByte(value);

    private void WriteInt16(short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        if (Format == NbtFormat.JavaEdition)
        {
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        }

        _stream.Write(buffer);
    }

    private void WriteInt32(int value)
    {
        if (Format == NbtFormat.BedrockNetwork)
        {
            WriteVarUInt((uint)((value << 1) ^ (value >> 31)));
            return;
        }

        Span<byte> buffer = stackalloc byte[sizeof(int)];
        if (Format == NbtFormat.JavaEdition)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        }

        _stream.Write(buffer);
    }

    private void WriteInt64(long value)
    {
        if (Format == NbtFormat.BedrockNetwork)
        {
            WriteVarUInt((ulong)((value << 1) ^ (value >> 63)));
            return;
        }

        Span<byte> buffer = stackalloc byte[sizeof(long)];
        if (Format == NbtFormat.JavaEdition)
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        }

        _stream.Write(buffer);
    }

    private void WriteSingle(float value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        if (Format == NbtFormat.JavaEdition)
        {
            BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        }

        _stream.Write(buffer);
    }

    private void WriteDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        if (Format == NbtFormat.JavaEdition)
        {
            BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
        }

        _stream.Write(buffer);
    }

    private void WriteVarUInt(ulong value)
    {
        Span<byte> buffer = stackalloc byte[10];
        var i = 0;
        while (value >= 0x80)
        {
            buffer[i++] = (byte)(value | 0x80);
            value >>= 7;
        }

        buffer[i++] = (byte)value;
        _stream.Write(buffer[..i]);
    }

    private void WriteString(string value)
    {
        if (Format == NbtFormat.JavaEdition)
        {
            var bytes = ModifiedUtf8.GetBytes(value);
            Span<byte> length = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)bytes.Length);
            _stream.Write(length);
            _stream.Write(bytes);
            return;
        }

        var utf8 = Encoding.UTF8.GetBytes(value);
        if (Format == NbtFormat.BedrockNetwork)
        {
            if (utf8.Length > short.MaxValue)
            {
                throw new NbtFormatException($"String is too long to encode ({utf8.Length} bytes; the maximum is {short.MaxValue}).");
            }

            WriteVarUInt((uint)utf8.Length);
        }
        else
        {
            if (utf8.Length > ushort.MaxValue)
            {
                throw new NbtFormatException($"String is too long to encode ({utf8.Length} bytes; the maximum is {ushort.MaxValue}).");
            }

            Span<byte> length = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(length, (ushort)utf8.Length);
            _stream.Write(length);
        }

        _stream.Write(utf8);
    }

    private void WriteInt32Array(int[] values)
    {
        WriteInt32(values.Length);
        if (Format == NbtFormat.BedrockNetwork)
        {
            foreach (var value in values)
            {
                WriteInt32(value);
            }
        }
        else if (IsNativeByteOrder)
        {
            _stream.Write(MemoryMarshal.AsBytes(values.AsSpan()));
        }
        else
        {
            var swapped = new int[values.Length];
            BinaryPrimitives.ReverseEndianness(values, swapped);
            _stream.Write(MemoryMarshal.AsBytes(swapped.AsSpan()));
        }
    }

    private void WriteInt64Array(long[] values)
    {
        WriteInt32(values.Length);
        if (Format == NbtFormat.BedrockNetwork)
        {
            foreach (var value in values)
            {
                WriteInt64(value);
            }
        }
        else if (IsNativeByteOrder)
        {
            _stream.Write(MemoryMarshal.AsBytes(values.AsSpan()));
        }
        else
        {
            var swapped = new long[values.Length];
            BinaryPrimitives.ReverseEndianness(values, swapped);
            _stream.Write(MemoryMarshal.AsBytes(swapped.AsSpan()));
        }
    }

    private bool IsNativeByteOrder => BitConverter.IsLittleEndian == (Format != NbtFormat.JavaEdition);
}
