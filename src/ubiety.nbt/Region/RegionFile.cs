using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Ubiety.Nbt.IO;

namespace Ubiety.Nbt.Region;

/// <summary>
/// A Minecraft Java Edition region file (Anvil <c>.mca</c>, or legacy McRegion <c>.mcr</c>), holding the
/// NBT data of up to 32×32 chunks.
/// </summary>
/// <remarks>
/// <para>
/// Chunk coordinates may be given either as absolute chunk coordinates or as local coordinates (0-31);
/// only the low five bits are used.
/// </para>
/// <para>
/// Chunks too large for the region file are stored in separate <c>c.&lt;x&gt;.&lt;z&gt;.mcc</c> files next to it.
/// Locating those requires the region file to be named <c>r.&lt;x&gt;.&lt;z&gt;.mca</c>.
/// </para>
/// <para>Instances are not thread-safe.</para>
/// </remarks>
public sealed partial class RegionFile : IDisposable
{
    /// <summary>The number of chunks along each side of a region.</summary>
    public const int ChunksPerSide = 32;

    /// <summary>The size of a sector, the unit in which region file space is allocated.</summary>
    public const int SectorSize = 4096;

    private const int ChunkCount = ChunksPerSide * ChunksPerSide;
    private const int HeaderSectors = 2;
    private const int ChunkHeaderSize = 5;
    private const int MaxSectorsPerChunk = byte.MaxValue;
    private const int MaxSectorOffset = (1 << 24) - 1;
    private const byte ExternalFlag = 0x80;

    private readonly FileStream _stream;
    private readonly int[] _locations = new int[ChunkCount];
    private readonly int[] _timestamps = new int[ChunkCount];
    private readonly List<bool> _usedSectors = [];

    private RegionFile(FileStream stream, bool readOnly)
    {
        _stream = stream;
        IsReadOnly = readOnly;
        FilePath = stream.Name;

        var match = RegionFileNameRegex().Match(Path.GetFileName(FilePath));
        if (match.Success &&
            int.TryParse(match.Groups[1].ValueSpan, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var regionX) &&
            int.TryParse(match.Groups[2].ValueSpan, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var regionZ))
        {
            RegionX = regionX;
            RegionZ = regionZ;
        }
    }

    /// <summary>Gets the full path of the region file.</summary>
    public string FilePath { get; }

    /// <summary>Gets a value indicating whether the file was opened read-only.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Gets the region's X coordinate, parsed from a file name of the form <c>r.&lt;x&gt;.&lt;z&gt;.mca</c>.</summary>
    public int? RegionX { get; }

    /// <summary>Gets the region's Z coordinate, parsed from a file name of the form <c>r.&lt;x&gt;.&lt;z&gt;.mca</c>.</summary>
    public int? RegionZ { get; }

    /// <summary>Opens a region file, creating it if it does not exist and <paramref name="readOnly"/> is false.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="readOnly">Whether to open the file for reading only. Read-only access lets the game keep it open.</param>
    /// <returns>The region file.</returns>
    /// <exception cref="NbtFormatException">The file is too small to be a region file.</exception>
    public static RegionFile Open(string path, bool readOnly = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        var stream = readOnly
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            : new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        try
        {
            var region = new RegionFile(stream, readOnly);
            region.LoadHeader();
            return region;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Gets the region file name for the given region coordinates, e.g. <c>r.-1.2.mca</c>.</summary>
    /// <param name="regionX">The region X coordinate.</param>
    /// <param name="regionZ">The region Z coordinate.</param>
    /// <returns>The file name.</returns>
    public static string GetFileName(int regionX, int regionZ) =>
        string.Create(CultureInfo.InvariantCulture, $"r.{regionX}.{regionZ}.mca");

    /// <summary>Gets the coordinates of the region containing the given chunk.</summary>
    /// <param name="chunkX">The absolute chunk X coordinate.</param>
    /// <param name="chunkZ">The absolute chunk Z coordinate.</param>
    /// <returns>The region coordinates.</returns>
    public static (int RegionX, int RegionZ) GetRegionCoordinates(int chunkX, int chunkZ) => (chunkX >> 5, chunkZ >> 5);

    /// <summary>Determines whether the region contains data for a chunk.</summary>
    /// <param name="chunkX">The chunk X coordinate.</param>
    /// <param name="chunkZ">The chunk Z coordinate.</param>
    /// <returns><see langword="true"/> if the chunk exists.</returns>
    public bool HasChunk(int chunkX, int chunkZ) => _locations[GetIndex(chunkX, chunkZ)] != 0;

    /// <summary>Gets the local coordinates (0-31) of every chunk in the region.</summary>
    /// <returns>The chunk coordinates, ordered by Z then X.</returns>
    public IEnumerable<(int X, int Z)> GetChunkPositions()
    {
        for (var i = 0; i < ChunkCount; i++)
        {
            if (_locations[i] != 0)
            {
                yield return (i % ChunksPerSide, i / ChunksPerSide);
            }
        }
    }

    /// <summary>Gets the time a chunk was last saved.</summary>
    /// <param name="chunkX">The chunk X coordinate.</param>
    /// <param name="chunkZ">The chunk Z coordinate.</param>
    /// <returns>The timestamp, or <see langword="null"/> if the chunk does not exist.</returns>
    public DateTimeOffset? GetTimestamp(int chunkX, int chunkZ)
    {
        var index = GetIndex(chunkX, chunkZ);
        return _locations[index] == 0 ? null : DateTimeOffset.FromUnixTimeSeconds((uint)_timestamps[index]);
    }

    /// <summary>Reads a chunk's NBT data.</summary>
    /// <param name="chunkX">The chunk X coordinate.</param>
    /// <param name="chunkZ">The chunk Z coordinate.</param>
    /// <returns>The chunk's root compound, or <see langword="null"/> if the chunk does not exist.</returns>
    /// <exception cref="NbtFormatException">The chunk data is corrupt.</exception>
    /// <exception cref="NotSupportedException">The chunk uses custom compression.</exception>
    public NbtCompound? ReadChunk(int chunkX, int chunkZ)
    {
        ObjectDisposedException.ThrowIf(!_stream.CanRead, this);
        var index = GetIndex(chunkX, chunkZ);
        var location = _locations[index];
        if (location == 0)
        {
            return null;
        }

        var (offset, sectorCount) = (location >>> 8, location & 0xFF);
        if (offset < HeaderSectors || sectorCount == 0)
        {
            throw new NbtFormatException($"Chunk ({chunkX & 31}, {chunkZ & 31}) has an invalid location (sector {offset}, count {sectorCount}).");
        }

        try
        {
            Span<byte> header = stackalloc byte[ChunkHeaderSize];
            _stream.Position = (long)offset * SectorSize;
            _stream.ReadExactly(header);

            var length = BinaryPrimitives.ReadInt32BigEndian(header);
            var compressionType = header[4];
            if (length <= 0 || length > (sectorCount * SectorSize) - 4)
            {
                throw new NbtFormatException($"Chunk ({chunkX & 31}, {chunkZ & 31}) has an invalid length {length}.");
            }

            byte[] data;
            if ((compressionType & ExternalFlag) != 0)
            {
                data = File.ReadAllBytes(GetExternalPath(chunkX, chunkZ));
                compressionType &= unchecked((byte)~ExternalFlag);
            }
            else
            {
                data = new byte[length - 1];
                _stream.ReadExactly(data);
            }

            return Decode(data, compressionType);
        }
        catch (EndOfStreamException e)
        {
            throw new NbtFormatException($"Chunk ({chunkX & 31}, {chunkZ & 31}) extends past the end of the region file.", e);
        }
    }

    /// <summary>Writes a chunk's NBT data, replacing any existing data, and sets its timestamp to now.</summary>
    /// <param name="chunkX">The chunk X coordinate.</param>
    /// <param name="chunkZ">The chunk Z coordinate.</param>
    /// <param name="chunk">The chunk's root compound.</param>
    /// <param name="compression">
    /// The compression to use. Minecraft uses <see cref="NbtCompression.ZLib"/> by default, and
    /// <see cref="NbtCompression.Lz4"/> when the server sets <c>region-file-compression=lz4</c>.
    /// </param>
    public void WriteChunk(int chunkX, int chunkZ, NbtCompound chunk, NbtCompression compression = NbtCompression.ZLib)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ThrowIfReadOnly();

        var compressionType = compression switch
        {
            NbtCompression.GZip => (byte)1,
            NbtCompression.ZLib => (byte)2,
            NbtCompression.None => (byte)3,
            NbtCompression.Lz4 => (byte)4,
            _ => throw new ArgumentOutOfRangeException(nameof(compression), compression, "Unknown compression."),
        };

        var payload = Encode(chunk, compression);
        var external = ChunkHeaderSize + payload.Length > MaxSectorsPerChunk * SectorSize;

        byte[] record;
        if (external)
        {
            // Too big for the region file: store the payload separately and leave a stub pointing at it.
            var externalPath = GetExternalPath(chunkX, chunkZ);
            var temporaryPath = externalPath + ".tmp";
            File.WriteAllBytes(temporaryPath, payload);
            File.Move(temporaryPath, externalPath, overwrite: true);

            record = new byte[ChunkHeaderSize];
            BinaryPrimitives.WriteInt32BigEndian(record, 1);
            record[4] = (byte)(compressionType | ExternalFlag);
        }
        else
        {
            record = new byte[ChunkHeaderSize + payload.Length];
            BinaryPrimitives.WriteInt32BigEndian(record, payload.Length + 1);
            record[4] = compressionType;
            payload.CopyTo(record, ChunkHeaderSize);
        }

        // Write the new data to free space before updating the header, so a failure leaves the old chunk intact.
        var sectorCount = (record.Length + SectorSize - 1) / SectorSize;
        var offset = Allocate(sectorCount);
        _stream.Position = (long)offset * SectorSize;
        _stream.Write(record);
        _stream.Write(new byte[(sectorCount * SectorSize) - record.Length]);

        var index = GetIndex(chunkX, chunkZ);
        var oldLocation = _locations[index];
        _locations[index] = (offset << 8) | sectorCount;
        _timestamps[index] = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        WriteHeaderEntry(index);
        Free(oldLocation);

        if (!external)
        {
            DeleteExternalFile(chunkX, chunkZ);
        }
    }

    /// <summary>Removes a chunk from the region.</summary>
    /// <param name="chunkX">The chunk X coordinate.</param>
    /// <param name="chunkZ">The chunk Z coordinate.</param>
    /// <returns><see langword="true"/> if the chunk existed.</returns>
    public bool DeleteChunk(int chunkX, int chunkZ)
    {
        ThrowIfReadOnly();
        var index = GetIndex(chunkX, chunkZ);
        var oldLocation = _locations[index];
        if (oldLocation == 0)
        {
            return false;
        }

        _locations[index] = 0;
        _timestamps[index] = 0;
        WriteHeaderEntry(index);
        Free(oldLocation);
        DeleteExternalFile(chunkX, chunkZ);
        return true;
    }

    /// <summary>Flushes pending writes to disk.</summary>
    public void Flush() => _stream.Flush(flushToDisk: true);

    /// <inheritdoc/>
    public void Dispose() => _stream.Dispose();

    private static int GetIndex(int chunkX, int chunkZ) => (chunkX & 31) + ((chunkZ & 31) * ChunksPerSide);

    private static NbtCompound Decode(byte[] data, byte compressionType)
    {
        var raw = new MemoryStream(data, writable: false);
        using var input = compressionType switch
        {
            1 => new GZipStream(raw, CompressionMode.Decompress),
            2 => new ZLibStream(raw, CompressionMode.Decompress),
            3 => (Stream)raw,
            4 => new MemoryStream(Lz4BlockStream.Decode(data), writable: false),
            127 => throw new NotSupportedException("Chunks with custom compression are not supported."),
            _ => throw new NbtFormatException($"Unknown chunk compression type {compressionType}."),
        };

        try
        {
            using var reader = new NbtBinaryReader(input, NbtFormat.JavaEdition, leaveOpen: true);
            var root = reader.ReadTag(out _);
            return root as NbtCompound ?? throw new NbtFormatException($"Expected a compound chunk root but found {root.TagType}.");
        }
        catch (InvalidDataException e)
        {
            throw new NbtFormatException("The compressed chunk data is corrupt.", e);
        }
    }

    private static byte[] Encode(NbtCompound chunk, NbtCompression compression)
    {
        using var output = new MemoryStream();
        Stream? compressor = compression switch
        {
            NbtCompression.GZip => new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true),
            NbtCompression.ZLib => new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true),
            _ => null,
        };

        using (var writer = new NbtBinaryWriter(compressor ?? output, NbtFormat.JavaEdition, leaveOpen: true))
        {
            writer.WriteTag(chunk);
        }

        compressor?.Dispose();
        return compression == NbtCompression.Lz4
            ? Lz4BlockStream.Encode(output.GetBuffer().AsSpan(0, (int)output.Length))
            : output.ToArray();
    }

    [GeneratedRegex(@"^r\.(-?\d+)\.(-?\d+)\.mc[ar]$", RegexOptions.CultureInvariant)]
    private static partial Regex RegionFileNameRegex();

    private void LoadHeader()
    {
        if (_stream.Length == 0)
        {
            if (IsReadOnly)
            {
                throw new NbtFormatException("The region file is empty.");
            }

            _stream.Write(new byte[HeaderSectors * SectorSize]);
        }
        else if (_stream.Length < HeaderSectors * SectorSize)
        {
            throw new NbtFormatException($"The file is too small to be a region file ({_stream.Length} bytes).");
        }

        var header = new byte[HeaderSectors * SectorSize];
        _stream.Position = 0;
        _stream.ReadExactly(header);

        for (var i = 0; i < ChunkCount; i++)
        {
            _locations[i] = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(i * 4));
            _timestamps[i] = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(SectorSize + (i * 4)));
        }

        MarkSectors(0, HeaderSectors, used: true);
        foreach (var location in _locations)
        {
            var (offset, count) = (location >>> 8, location & 0xFF);
            if (offset >= HeaderSectors && count > 0)
            {
                MarkSectors(offset, count, used: true);
            }
        }
    }

    private int Allocate(int count)
    {
        // First fit, as Minecraft does. Sectors past the end of the bitmap are free, so this always terminates.
        var run = 0;
        for (var sector = HeaderSectors; ; sector++)
        {
            if (sector < _usedSectors.Count && _usedSectors[sector])
            {
                run = 0;
                continue;
            }

            if (++run == count)
            {
                var start = sector - count + 1;
                if (start > MaxSectorOffset)
                {
                    throw new IOException("The region file is full.");
                }

                MarkSectors(start, count, used: true);
                return start;
            }
        }
    }

    private void Free(int location)
    {
        var (offset, count) = (location >>> 8, location & 0xFF);
        if (offset >= HeaderSectors && count > 0)
        {
            MarkSectors(offset, count, used: false);
        }
    }

    private void MarkSectors(int start, int count, bool used)
    {
        var end = start + count;
        while (used && _usedSectors.Count < end)
        {
            _usedSectors.Add(false);
        }

        for (var sector = start; sector < Math.Min(end, _usedSectors.Count); sector++)
        {
            _usedSectors[sector] = used;
        }
    }

    private void WriteHeaderEntry(int index)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, _locations[index]);
        _stream.Position = index * 4;
        _stream.Write(buffer);

        BinaryPrimitives.WriteInt32BigEndian(buffer, _timestamps[index]);
        _stream.Position = SectorSize + (index * 4);
        _stream.Write(buffer);
        _stream.Flush();
    }

    private string GetExternalPath(int chunkX, int chunkZ)
    {
        if (RegionX is not { } regionX || RegionZ is not { } regionZ)
        {
            throw new InvalidOperationException(
                "Locating an external chunk file requires the region file to be named r.<x>.<z>.mca.");
        }

        var directory = Path.GetDirectoryName(FilePath) ?? string.Empty;
        var x = (regionX * ChunksPerSide) + (chunkX & 31);
        var z = (regionZ * ChunksPerSide) + (chunkZ & 31);
        return Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"c.{x}.{z}.mcc"));
    }

    private void DeleteExternalFile(int chunkX, int chunkZ)
    {
        if (RegionX is not null && RegionZ is not null)
        {
            File.Delete(GetExternalPath(chunkX, chunkZ));
        }
    }

    private void ThrowIfReadOnly()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException("The region file was opened read-only.");
        }
    }
}
