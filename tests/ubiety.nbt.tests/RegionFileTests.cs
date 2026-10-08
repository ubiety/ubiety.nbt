using System.Buffers.Binary;
using System.IO.Compression;
using Ubiety.Nbt.IO;
using Ubiety.Nbt.Region;

namespace Ubiety.Nbt.Tests;

public sealed class RegionFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ubiety-nbt-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void ReadsHandBuiltRegion()
    {
        // Chunk (1, 0) at sector 2, zlib compressed, laid out exactly as Minecraft writes it.
        var payload = Compress(new NbtCompound { ["DataVersion"] = 3953, ["xPos"] = 1 });
        var file = new byte[3 * RegionFile.SectorSize];
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(1 * 4), (2 << 8) | 1);
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(RegionFile.SectorSize + (1 * 4)), 1_700_000_000);
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(2 * RegionFile.SectorSize), payload.Length + 1);
        file[(2 * RegionFile.SectorSize) + 4] = 2;
        payload.CopyTo(file, (2 * RegionFile.SectorSize) + 5);
        var path = PathFor(0, 0);
        File.WriteAllBytes(path, file);

        using var region = RegionFile.Open(path, readOnly: true);

        Assert.Equal([(1, 0)], region.GetChunkPositions());
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), region.GetTimestamp(1, 0));
        Assert.Equal(3953, region.ReadChunk(1, 0)!.GetInt("DataVersion"));
        Assert.Null(region.ReadChunk(0, 0));
        Assert.Null(region.GetTimestamp(0, 0));
    }

    [Theory]
    [InlineData(NbtCompression.None)]
    [InlineData(NbtCompression.GZip)]
    [InlineData(NbtCompression.ZLib)]
    [InlineData(NbtCompression.Lz4)]
    public void RoundTripsChunksAcrossReopen(NbtCompression compression)
    {
        var path = PathFor(0, 0);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        using (var region = RegionFile.Open(path))
        {
            region.WriteChunk(0, 0, Chunk(0, 0), compression);
            region.WriteChunk(31, 31, Chunk(31, 31), compression);
        }

        using var reopened = RegionFile.Open(path);

        Assert.Equal([(0, 0), (31, 31)], reopened.GetChunkPositions());
        Assert.True(Chunk(31, 31).DeepEquals(reopened.ReadChunk(31, 31)));
        Assert.True(reopened.GetTimestamp(0, 0) >= before);
        Assert.Equal(0, new FileInfo(path).Length % RegionFile.SectorSize);
    }

    [Fact]
    public void AcceptsAbsoluteChunkCoordinates()
    {
        using var region = RegionFile.Open(PathFor(-1, 2));

        region.WriteChunk(-1, 65, Chunk(-1, 65));

        Assert.True(region.HasChunk(31, 1));
        Assert.Equal(-1, region.ReadChunk(31, 1)!.GetInt("xPos"));
    }

    [Fact]
    public void ReusesFreedSectors()
    {
        var path = PathFor(0, 0);
        using var region = RegionFile.Open(path);
        region.WriteChunk(0, 0, Chunk(0, 0));
        region.WriteChunk(1, 0, Chunk(1, 0));
        region.Flush();
        var length = new FileInfo(path).Length;

        Assert.True(region.DeleteChunk(0, 0));
        region.WriteChunk(2, 0, Chunk(2, 0));
        region.Flush();

        Assert.Equal(length, new FileInfo(path).Length);
        Assert.False(region.HasChunk(0, 0));
        Assert.False(region.DeleteChunk(0, 0));
        Assert.Equal(2, region.ReadChunk(2, 0)!.GetInt("xPos"));
    }

    [Fact]
    public void GrowsChunksInPlaceOfSmallerOnes()
    {
        using var region = RegionFile.Open(PathFor(0, 0));
        region.WriteChunk(0, 0, Chunk(0, 0));
        var big = Chunk(0, 0);
        big["blob"] = new byte[3 * RegionFile.SectorSize];

        region.WriteChunk(0, 0, big, NbtCompression.None);
        region.WriteChunk(1, 0, Chunk(1, 0));

        Assert.True(big.DeepEquals(region.ReadChunk(0, 0)));
        Assert.Equal(1, region.ReadChunk(1, 0)!.GetInt("xPos"));
    }

    [Fact]
    public void StoresOversizedChunksExternally()
    {
        var path = PathFor(1, -1);
        var externalPath = Path.Combine(_directory, "c.33.-31.mcc");
        var big = Chunk(33, -31);
        big["blob"] = new byte[1_100_000];

        using (var region = RegionFile.Open(path))
        {
            region.WriteChunk(1, 1, big, NbtCompression.None);
        }

        Assert.True(File.Exists(externalPath));
        Assert.True(new FileInfo(path).Length < 4 * RegionFile.SectorSize);

        using (var region = RegionFile.Open(path))
        {
            Assert.True(big.DeepEquals(region.ReadChunk(1, 1)));

            region.WriteChunk(1, 1, Chunk(33, -31));

            Assert.False(File.Exists(externalPath));
            Assert.Equal(33, region.ReadChunk(1, 1)!.GetInt("xPos"));
        }
    }

    [Fact]
    public void ExternalChunksNeedRegionCoordinates()
    {
        using var region = RegionFile.Open(Path.Combine(_directory, "custom.mca"));
        var big = new NbtCompound { ["blob"] = new byte[1_100_000] };

        Assert.Null(region.RegionX);
        Assert.Throws<InvalidOperationException>(() => region.WriteChunk(0, 0, big, NbtCompression.None));
    }

    [Fact]
    public void ReadOnlyRegionRejectsWrites()
    {
        var path = PathFor(0, 0);
        RegionFile.Open(path).Dispose();

        using var region = RegionFile.Open(path, readOnly: true);

        Assert.True(region.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => region.WriteChunk(0, 0, Chunk(0, 0)));
        Assert.Throws<InvalidOperationException>(() => region.DeleteChunk(0, 0));
    }

    [Fact]
    public void RejectsTruncatedFiles()
    {
        var path = PathFor(0, 0);
        File.WriteAllBytes(path, new byte[100]);

        Assert.Throws<NbtFormatException>(() => RegionFile.Open(path));
    }

    [Fact]
    public void ReportsUnsupportedCustomCompression()
    {
        var path = PathFor(0, 0);
        var file = new byte[3 * RegionFile.SectorSize];
        BinaryPrimitives.WriteInt32BigEndian(file, (2 << 8) | 1);
        BinaryPrimitives.WriteInt32BigEndian(file.AsSpan(2 * RegionFile.SectorSize), 2);
        file[(2 * RegionFile.SectorSize) + 4] = 127;
        File.WriteAllBytes(path, file);

        using var region = RegionFile.Open(path, readOnly: true);

        Assert.Throws<NotSupportedException>(() => region.ReadChunk(0, 0));
    }

    [Fact]
    public void StoresLz4ChunksWithTypeFour()
    {
        var path = PathFor(0, 0);
        using (var region = RegionFile.Open(path))
        {
            region.WriteChunk(0, 0, Chunk(0, 0), NbtCompression.Lz4);
        }

        var bytes = File.ReadAllBytes(path);

        Assert.Equal(4, bytes[(2 * RegionFile.SectorSize) + 4]);
        Assert.Equal("LZ4Block"u8.ToArray(), bytes.AsSpan((2 * RegionFile.SectorSize) + 5, 8).ToArray());
    }

    [Fact]
    public void ReportsChunksPastEndOfFile()
    {
        var path = PathFor(0, 0);
        var file = new byte[2 * RegionFile.SectorSize];
        BinaryPrimitives.WriteInt32BigEndian(file, (5 << 8) | 1);
        File.WriteAllBytes(path, file);

        using var region = RegionFile.Open(path, readOnly: true);

        Assert.Throws<NbtFormatException>(() => region.ReadChunk(0, 0));
    }

    [Fact]
    public void ComputesRegionNames()
    {
        Assert.Equal((-1, 2), RegionFile.GetRegionCoordinates(-1, 64));
        Assert.Equal("r.-1.2.mca", RegionFile.GetFileName(-1, 2));
    }

    private static NbtCompound Chunk(int x, int z) => new()
    {
        ["DataVersion"] = 3953,
        ["xPos"] = x,
        ["zPos"] = z,
        ["Status"] = "minecraft:full",
    };

    private static byte[] Compress(NbtCompound compound)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
        using (var writer = new NbtBinaryWriter(zlib))
        {
            writer.WriteTag(compound);
        }

        return output.ToArray();
    }

    private string PathFor(int regionX, int regionZ) => Path.Combine(_directory, RegionFile.GetFileName(regionX, regionZ));
}
