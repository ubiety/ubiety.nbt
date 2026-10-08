using System.Text;
using Ubiety.Nbt.IO;

namespace Ubiety.Nbt.Tests;

public class Lz4Tests
{
    /// <summary>
    /// A Java NBT compound <c>{text:"the quick brown fox jumps over the lazy dog. " x6}</c>, compressed by
    /// lz4-java 1.8.0's <c>LZ4BlockOutputStream</c> (the library and class Minecraft uses).
    /// </summary>
    private const string LzJavaFixture =
        "TFo0QmxvY2smRQAAABsBAACxlooG8BwKAAAIAAR0ZXh0AQ50aGUgcXVpY2sgYnJvd24gZm94IGp1bXBzIG92ZXIgHwCRbGF6eSBkb2cuDgAPLQDGUG9nLiAATFo0QmxvY2sWAAAAAAAAAAAAAAAA";

    private static readonly string FixtureText = string.Concat(Enumerable.Repeat("the quick brown fox jumps over the lazy dog. ", 6));

    [Fact]
    public void DecodesLz4JavaOutput()
    {
        var file = NbtFile.Load(Convert.FromBase64String(LzJavaFixture));

        Assert.Equal(NbtCompression.Lz4, file.Compression);
        Assert.Equal(FixtureText, file.Root.GetString("text"));
    }

    [Fact]
    public void ReEncodesLz4JavaOutput()
    {
        var decoded = Lz4BlockStream.Decode(Convert.FromBase64String(LzJavaFixture));

        var encoded = Lz4BlockStream.Encode(decoded);

        Assert.Equal("LZ4Block"u8.ToArray(), encoded[..8]);
        Assert.Equal(0x26, encoded[8]);
        Assert.True(encoded.Length < decoded.Length);
        Assert.Equal(decoded, Lz4BlockStream.Decode(encoded));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(1000)]
    [InlineData(65536)]
    [InlineData(200_000)]
    public void RoundTripsCompressibleData(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = (byte)("minecraft:stone"[i % 15] + (i / 4096));
        }

        Assert.Equal(data, Lz4BlockStream.Decode(Lz4BlockStream.Encode(data)));
    }

    [Fact]
    public void RoundTripsIncompressibleDataAsRawBlocks()
    {
        var data = new byte[150_000];
        new Random(1234).NextBytes(data);

        var encoded = Lz4BlockStream.Encode(data);

        Assert.Equal(0x16, encoded[8]);
        Assert.Equal(data, Lz4BlockStream.Decode(encoded));
    }

    [Fact]
    public void RoundTripsOverlappingMatches()
    {
        var data = Encoding.ASCII.GetBytes(new string('a', 5000) + "ab" + string.Concat(Enumerable.Repeat("xyz", 2000)));

        var block = new byte[Lz4Block.MaxCompressedLength(data.Length)];
        var length = Lz4Block.Compress(data, block);
        var decoded = new byte[data.Length];

        Assert.True(length < 100);
        Assert.Equal(data.Length, Lz4Block.Decompress(block.AsSpan(0, length), decoded));
        Assert.Equal(data, decoded);
    }

    [Fact]
    public void DetectsChecksumMismatch()
    {
        var data = Convert.FromBase64String(LzJavaFixture);
        data[17] ^= 1;

        var error = Assert.Throws<NbtFormatException>(() => Lz4BlockStream.Decode(data));
        Assert.Contains("checksum", error.Message);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void RejectsTruncatedData(int length)
    {
        var data = Convert.FromBase64String(LzJavaFixture)[..length];

        Assert.Throws<NbtFormatException>(() => Lz4BlockStream.Decode(data));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x10 })]
    [InlineData(new byte[] { 0x10, 0x41, 0x01 })]
    [InlineData(new byte[] { 0x10, 0x41, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x10, 0x41, 0x02, 0x00 })]
    [InlineData(new byte[] { 0xF0 })]
    public void RejectsMalformedBlocks(byte[] block)
    {
        Assert.Throws<NbtFormatException>(() => Lz4Block.Decompress(block, new byte[100]));
    }

    [Fact]
    public void XxHash32MatchesReference()
    {
        var data = new byte[100];
        new Random(42).NextBytes(data);

        for (var length = 0; length <= data.Length; length++)
        {
            foreach (var seed in new uint[] { 0, 0x9747B28C })
            {
                var expected = System.IO.Hashing.XxHash32.HashToUInt32(data.AsSpan(0, length), unchecked((int)seed));
                Assert.Equal(expected, XxHash32.Hash(data.AsSpan(0, length), seed));
            }
        }
    }
}
