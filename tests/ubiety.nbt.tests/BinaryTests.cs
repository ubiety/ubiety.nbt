using Ubiety.Nbt.IO;

namespace Ubiety.Nbt.Tests;

public class BinaryTests
{
    [Fact]
    public void ReadsHelloWorld()
    {
        var file = NbtFile.Load(Samples.HelloWorld);

        Assert.Equal("hello world", file.RootName);
        Assert.Equal("Bananrama", file.Root.GetString("name"));
        Assert.Equal(NbtCompression.None, file.Compression);
    }

    [Fact]
    public void WritesHelloWorldExactly()
    {
        var file = new NbtFile(new NbtCompound { ["name"] = "Bananrama" }, "hello world");

        Assert.Equal(Samples.HelloWorld, file.ToArray());
    }

    [Theory]
    [InlineData(NbtFormat.JavaEdition)]
    [InlineData(NbtFormat.BedrockEdition)]
    [InlineData(NbtFormat.BedrockNetwork)]
    public void RoundTripsAllTypes(NbtFormat format)
    {
        var original = new NbtFile(Samples.AllTypes(), "root") { Format = format };

        var loaded = NbtFile.Load(original.ToArray(), format);

        Assert.Equal("root", loaded.RootName);
        Assert.True(original.Root.DeepEquals(loaded.Root));
        Assert.Equal(NbtTagType.End, loaded.Root.GetList("emptyList").ElementType);
        Assert.Equal(NbtTagType.Int, loaded.Root.GetList("intList").ElementType);
    }

    [Theory]
    [InlineData(NbtCompression.None)]
    [InlineData(NbtCompression.GZip)]
    [InlineData(NbtCompression.ZLib)]
    public void DetectsCompression(NbtCompression compression)
    {
        var data = new NbtFile(Samples.AllTypes()) { Compression = compression }.ToArray();

        var loaded = NbtFile.Load(data);

        Assert.Equal(compression, loaded.Compression);
        Assert.True(Samples.AllTypes().DeepEquals(loaded.Root));
    }

    [Fact]
    public void LoadsFromNonSeekableStream()
    {
        var data = new NbtFile(Samples.AllTypes()) { Compression = NbtCompression.GZip }.ToArray();
        using var stream = new NonSeekableStream(data);

        var loaded = NbtFile.Load(stream);

        Assert.True(Samples.AllTypes().DeepEquals(loaded.Root));
    }

    [Fact]
    public async Task SavesAndLoadsFilesAsync()
    {
        var path = Path.GetTempFileName();
        try
        {
            await new NbtFile(Samples.AllTypes()) { Compression = NbtCompression.GZip }.SaveAsync(path);

            var loaded = await NbtFile.LoadAsync(path);

            Assert.Equal(NbtCompression.GZip, loaded.Compression);
            Assert.True(Samples.AllTypes().DeepEquals(loaded.Root));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void JavaStringsUseModifiedUtf8()
    {
        var bytes = WriteNameless(new NbtString("\0\U0001F600"), NbtFormat.JavaEdition);

        // Type, length (8), U+0000 as C0 80, then each surrogate of U+1F600 as three bytes.
        Assert.Equal([0x08, 0x00, 0x08, 0xC0, 0x80, 0xED, 0xA0, 0xBD, 0xED, 0xB8, 0x80], bytes);
    }

    [Fact]
    public void BedrockStringsUseUtf8()
    {
        var bytes = WriteNameless(new NbtString("\U0001F600"), NbtFormat.BedrockEdition);

        Assert.Equal([0x08, 0x04, 0x00, 0xF0, 0x9F, 0x98, 0x80], bytes);
    }

    [Fact]
    public void BedrockNetworkUsesZigZagVarInts()
    {
        var bytes = WriteNameless(new NbtCompound { ["a"] = -1, ["b"] = 300L }, NbtFormat.BedrockNetwork);

        Assert.Equal(
            [
                0x0A,
                0x03, 0x01, (byte)'a', 0x01,
                0x04, 0x01, (byte)'b', 0xD8, 0x04,
                0x00,
            ],
            bytes);
    }

    [Fact]
    public void ReadsNamelessTag()
    {
        using var reader = new NbtBinaryReader(new MemoryStream([0x08, 0x00, 0x02, (byte)'h', (byte)'i']));

        var tag = reader.ReadNamelessTag();

        Assert.Equal("hi", Assert.IsType<NbtString>(tag).Value);
    }

    [Fact]
    public void TruncatedDataThrowsFormatException()
    {
        var truncated = Samples.HelloWorld[..^5];

        Assert.Throws<NbtFormatException>(() => NbtFile.Load(truncated));
    }

    [Fact]
    public void HugeDeclaredLengthDoesNotAllocateBeforeFailing()
    {
        // A byte array claiming int.MaxValue bytes, with none following.
        byte[] data = [0x0A, 0x00, 0x00, 0x07, 0x00, 0x01, (byte)'x', 0x7F, 0xFF, 0xFF, 0xFF];

        Assert.Throws<NbtFormatException>(() => NbtFile.Load(data));
    }

    [Fact]
    public void UnknownTagTypeThrows()
    {
        Assert.Throws<NbtFormatException>(() => NbtFile.Load([0x0A, 0x00, 0x00, 0x0D, 0x00, 0x00, 0x00]));
    }

    [Fact]
    public void NonCompoundRootThrows()
    {
        Assert.Throws<NbtFormatException>(() => NbtFile.Load([0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01]));
    }

    [Fact]
    public void ExcessiveNestingThrowsOnRead()
    {
        // 1000 nested lists of lists.
        var data = new MemoryStream();
        data.Write([0x09, 0x00, 0x00]);
        for (var i = 0; i < 1000; i++)
        {
            data.Write([0x09, 0x00, 0x00, 0x00, 0x01]);
        }

        data.Position = 0;
        using var reader = new NbtBinaryReader(data);

        Assert.Throws<NbtFormatException>(() => reader.ReadTag(out _));
    }

    [Fact]
    public void CyclicTreeThrowsOnWrite()
    {
        var compound = new NbtCompound();
        compound["self"] = compound;

        Assert.Throws<NbtFormatException>(() => new NbtFile(compound).ToArray());
        Assert.Throws<NbtFormatException>(() => compound.ToSnbt());
    }

    [Fact]
    public void OverlongStringThrowsOnWrite()
    {
        var file = new NbtFile(new NbtCompound { ["s"] = new string('世', 30000) });

        Assert.Throws<NbtFormatException>(() => file.ToArray());
    }

    private static byte[] WriteNameless(NbtTag tag, NbtFormat format)
    {
        using var stream = new MemoryStream();
        using (var writer = new NbtBinaryWriter(stream, format, leaveOpen: true))
        {
            writer.WriteNamelessTag(tag);
        }

        using var reader = new NbtBinaryReader(new MemoryStream(stream.ToArray()), format);
        Assert.True(tag.DeepEquals(reader.ReadNamelessTag()));
        return stream.ToArray();
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
