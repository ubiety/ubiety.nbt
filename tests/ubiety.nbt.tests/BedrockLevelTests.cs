namespace Ubiety.Nbt.Tests;

public class BedrockLevelTests
{
    /// <summary>
    /// A Bedrock level.dat: storage version 10 and payload length 25 (both little-endian), then the
    /// little-endian NBT compound <c>{StorageVersion:10}</c>.
    /// </summary>
    private static readonly byte[] LevelDat =
    [
        0x0A, 0x00, 0x00, 0x00, 0x19, 0x00, 0x00, 0x00,
        0x0A, 0x00, 0x00,
        0x03, 0x0E, 0x00, .. "StorageVersion"u8, 0x0A, 0x00, 0x00, 0x00,
        0x00,
    ];

    [Fact]
    public void ReadsLevelDatHeader()
    {
        var file = NbtFile.Load(LevelDat, NbtFormat.BedrockEdition);

        Assert.Equal(10, file.BedrockStorageVersion);
        Assert.Equal(10, file.Root.GetInt("StorageVersion"));
        Assert.Equal(NbtCompression.None, file.Compression);
    }

    [Fact]
    public void WritesLevelDatHeaderExactly()
    {
        var file = new NbtFile(new NbtCompound { ["StorageVersion"] = 10 })
        {
            Format = NbtFormat.BedrockEdition,
            BedrockStorageVersion = 10,
        };

        Assert.Equal(LevelDat, file.ToArray());
    }

    [Fact]
    public void RoundTripsThroughDisk()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("ubiety-nbt-").FullName, "level.dat");
        try
        {
            var root = Samples.AllTypes();
            new NbtFile(root) { Format = NbtFormat.BedrockEdition, BedrockStorageVersion = 9 }.Save(path);

            var loaded = NbtFile.Load(path, NbtFormat.BedrockEdition);

            Assert.Equal(9, loaded.BedrockStorageVersion);
            Assert.True(root.DeepEquals(loaded.Root));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void LoadsHeaderlessBedrockNbt()
    {
        var data = new NbtFile(Samples.AllTypes()) { Format = NbtFormat.BedrockEdition }.ToArray();

        var file = NbtFile.Load(data, NbtFormat.BedrockEdition);

        Assert.Null(file.BedrockStorageVersion);
        Assert.True(Samples.AllTypes().DeepEquals(file.Root));
    }

    [Fact]
    public void EmptyHeaderlessCompoundIsNotMistakenForHeader()
    {
        // Starts with the same bytes as a version 10 header.
        var file = NbtFile.Load([0x0A, 0x00, 0x00, 0x00], NbtFormat.BedrockEdition);

        Assert.Null(file.BedrockStorageVersion);
        Assert.Empty(file.Root);
    }

    [Fact]
    public void IgnoresHeaderForOtherFormats()
    {
        var data = new NbtFile(new NbtCompound { ["a"] = 1 }) { Format = NbtFormat.BedrockNetwork }.ToArray();

        Assert.Null(NbtFile.Load(data, NbtFormat.BedrockNetwork).BedrockStorageVersion);
    }

    [Theory]
    [InlineData(NbtFormat.JavaEdition, NbtCompression.None)]
    [InlineData(NbtFormat.BedrockNetwork, NbtCompression.None)]
    [InlineData(NbtFormat.BedrockEdition, NbtCompression.GZip)]
    public void HeaderRequiresUncompressedBedrockFormat(NbtFormat format, NbtCompression compression)
    {
        var file = new NbtFile { Format = format, Compression = compression, BedrockStorageVersion = 10 };

        Assert.Throws<InvalidOperationException>(() => file.ToArray());
    }
}
