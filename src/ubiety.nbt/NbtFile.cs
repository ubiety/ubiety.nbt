using System.IO.Compression;
using Ubiety.Nbt.IO;

namespace Ubiety.Nbt;

/// <summary>
/// An NBT file: a named root compound, plus the format and compression used to store it.
/// </summary>
public sealed class NbtFile
{
    /// <summary>Initializes a new instance of the <see cref="NbtFile"/> class with an empty root compound.</summary>
    public NbtFile()
        : this(new NbtCompound())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NbtFile"/> class.</summary>
    /// <param name="root">The root compound.</param>
    /// <param name="rootName">The root tag's name. Almost always empty.</param>
    public NbtFile(NbtCompound root, string rootName = "")
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(rootName);
        Root = root;
        RootName = rootName;
    }

    /// <summary>Gets or sets the root compound.</summary>
    public NbtCompound Root
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets or sets the root tag's name.</summary>
    public string RootName
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets or sets the binary format used when saving. Set by <c>Load</c> to the format that was read.</summary>
    public NbtFormat Format { get; set; } = NbtFormat.JavaEdition;

    /// <summary>Gets or sets the compression used when saving. Set by <c>Load</c> to the compression that was detected.</summary>
    public NbtCompression Compression { get; set; } = NbtCompression.None;

    /// <summary>Loads an NBT file, detecting gzip or zlib compression automatically.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="format">The binary format of the data.</param>
    /// <returns>The file.</returns>
    /// <exception cref="NbtFormatException">The data is malformed or the root is not a compound.</exception>
    public static NbtFile Load(string path, NbtFormat format = NbtFormat.JavaEdition)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Load(File.ReadAllBytes(path), format);
    }

    /// <summary>Loads NBT data from a byte array, detecting gzip or zlib compression automatically.</summary>
    /// <param name="data">The data.</param>
    /// <param name="format">The binary format of the data.</param>
    /// <returns>The file.</returns>
    /// <exception cref="NbtFormatException">The data is malformed or the root is not a compound.</exception>
    public static NbtFile Load(byte[] data, NbtFormat format = NbtFormat.JavaEdition)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Load(new MemoryStream(data, writable: false), format);
    }

    /// <summary>Loads NBT data from a stream, detecting gzip or zlib compression automatically.</summary>
    /// <param name="stream">The stream. It is read from its current position and left open.</param>
    /// <param name="format">The binary format of the data.</param>
    /// <returns>The file.</returns>
    /// <exception cref="NbtFormatException">The data is malformed or the root is not a compound.</exception>
    public static NbtFile Load(Stream stream, NbtFormat format = NbtFormat.JavaEdition)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            // Compression detection needs to peek at the first byte.
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            return Load(buffer, format);
        }

        var compression = DetectCompression(stream);
        var input = compression switch
        {
            NbtCompression.GZip => new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true),
            NbtCompression.ZLib => new ZLibStream(stream, CompressionMode.Decompress, leaveOpen: true),
            NbtCompression.Lz4 => new MemoryStream(Lz4BlockStream.Decode(ReadToEnd(stream)), writable: false),
            _ => stream,
        };

        try
        {
            using var reader = new NbtBinaryReader(input, format, leaveOpen: true);
            var root = reader.ReadTag(out var name);
            if (root is not NbtCompound compound)
            {
                throw new NbtFormatException($"Expected a compound root tag but found {root.TagType}.");
            }

            return new NbtFile(compound, name) { Format = format, Compression = compression };
        }
        catch (InvalidDataException e)
        {
            throw new NbtFormatException($"The {compression} compressed data is corrupt.", e);
        }
        finally
        {
            if (!ReferenceEquals(input, stream))
            {
                input.Dispose();
            }
        }
    }

    /// <summary>Loads an NBT file asynchronously, detecting gzip or zlib compression automatically.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="format">The binary format of the data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The file.</returns>
    /// <exception cref="NbtFormatException">The data is malformed or the root is not a compound.</exception>
    public static async Task<NbtFile> LoadAsync(string path, NbtFormat format = NbtFormat.JavaEdition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Load(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false), format);
    }

    /// <summary>Loads NBT data from a stream asynchronously, detecting gzip or zlib compression automatically.</summary>
    /// <param name="stream">The stream. It is read to the end and left open.</param>
    /// <param name="format">The binary format of the data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The file.</returns>
    /// <exception cref="NbtFormatException">The data is malformed or the root is not a compound.</exception>
    public static async Task<NbtFile> LoadAsync(Stream stream, NbtFormat format = NbtFormat.JavaEdition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return Load(buffer, format);
    }

    /// <summary>Saves the file using <see cref="Format"/> and <see cref="Compression"/>.</summary>
    /// <param name="path">The file path. An existing file is overwritten.</param>
    public void Save(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        File.WriteAllBytes(path, ToArray());
    }

    /// <summary>Saves the file to a stream using <see cref="Format"/> and <see cref="Compression"/>.</summary>
    /// <param name="stream">The stream. It is left open.</param>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var raw = Serialize();
        if (Compression is NbtCompression.None or NbtCompression.Lz4)
        {
            raw.CopyTo(stream);
            return;
        }

        using var compressor = CreateCompressor(stream);
        raw.CopyTo(compressor);
    }

    /// <summary>Saves the file asynchronously using <see cref="Format"/> and <see cref="Compression"/>.</summary>
    /// <param name="path">The file path. An existing file is overwritten.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        return File.WriteAllBytesAsync(path, ToArray(), cancellationToken);
    }

    /// <summary>Saves the file to a stream asynchronously using <see cref="Format"/> and <see cref="Compression"/>.</summary>
    /// <param name="stream">The stream. It is left open.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the data is written.</returns>
    public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var raw = Serialize();
        if (Compression is NbtCompression.None or NbtCompression.Lz4)
        {
            await raw.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var compressor = CreateCompressor(stream);
        await raw.CopyToAsync(compressor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serializes the file using <see cref="Format"/> and <see cref="Compression"/>.</summary>
    /// <returns>The encoded bytes.</returns>
    public byte[] ToArray()
    {
        using var output = new MemoryStream();
        Save(output);
        return output.ToArray();
    }

    private static NbtCompression DetectCompression(Stream stream)
    {
        // Uncompressed NBT starts with a tag type (0-12), which can't be confused with any of the magic bytes.
        var position = stream.Position;
        Span<byte> magic = stackalloc byte[8];
        var read = stream.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false);
        stream.Position = position;
        if (read == 0)
        {
            return NbtCompression.None;
        }

        return magic[0] switch
        {
            0x1F => NbtCompression.GZip,
            0x78 => NbtCompression.ZLib,
            _ when Lz4BlockStream.HasMagic(magic[..read]) => NbtCompression.Lz4,
            _ => NbtCompression.None,
        };
    }

    private static byte[] ReadToEnd(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    // Returns the encoded tag, LZ4 compressed if requested; GZip and ZLib are applied by the caller as it writes.
    private MemoryStream Serialize()
    {
        var buffer = new MemoryStream();
        using (var writer = new NbtBinaryWriter(buffer, Format, leaveOpen: true))
        {
            writer.WriteTag(Root, RootName);
        }

        if (Compression == NbtCompression.Lz4)
        {
            buffer = new MemoryStream(Lz4BlockStream.Encode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)), writable: false);
        }

        buffer.Position = 0;
        return buffer;
    }

    private Stream CreateCompressor(Stream stream) => Compression switch
    {
        NbtCompression.GZip => new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true),
        NbtCompression.ZLib => new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true),
        _ => throw new InvalidOperationException($"Unknown compression {Compression}."),
    };
}
