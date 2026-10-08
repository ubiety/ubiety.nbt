namespace Ubiety.Nbt.Tests;

public class SnbtTests
{
    [Fact]
    public void FormatsCompactSnbt()
    {
        var compound = new NbtCompound
        {
            ["b"] = (byte)255,
            ["s"] = (short)-2,
            ["i"] = 3,
            ["l"] = 4L,
            ["f"] = 0.5f,
            ["d"] = 1d,
            ["str"] = "hi",
            ["needs quotes"] = "it's",
            ["ba"] = new byte[] { 1, 2 },
            ["ia"] = new[] { 3 },
            ["la"] = new[] { 4L },
            ["list"] = new NbtList { 1, 2 },
        };

        Assert.Equal(
            "{b:-1b,s:-2s,i:3,l:4L,f:0.5f,d:1d,str:\"hi\",\"needs quotes\":\"it's\",ba:[B;1b,2b],ia:[I;3],la:[L;4L],list:[1,2]}",
            compound.ToSnbt());
    }

    [Fact]
    public void FormatsIndentedSnbt()
    {
        var compound = new NbtCompound
        {
            ["pos"] = new NbtList { 1.0, 2.0 },
            ["items"] = new NbtList { new NbtCompound { ["id"] = "stone" } },
            ["empty"] = new NbtCompound(),
        };

        Assert.Equal(
            """
            {
                pos: [1d, 2d],
                items: [
                    {
                        id: "stone"
                    }
                ],
                empty: {}
            }
            """.ReplaceLineEndings("\n"),
            compound.ToSnbt(indented: true));
    }

    [Fact]
    public void QuotesStringsSafely()
    {
        Assert.Equal("'say \"hi\"'", new NbtString("say \"hi\"").ToSnbt());
        Assert.Equal("\"both \\\" and '\"", new NbtString("both \" and '").ToSnbt());
        Assert.Equal("\"back\\\\slash\"", new NbtString("back\\slash").ToSnbt());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundTripsAllTypes(bool indented)
    {
        var original = Samples.AllTypes();

        var parsed = NbtTag.ParseSnbt(original.ToSnbt(indented));

        Assert.True(original.DeepEquals(parsed), parsed.ToSnbt(indented: true));
    }

    [Theory]
    [InlineData("1b", NbtTagType.Byte)]
    [InlineData("-128B", NbtTagType.Byte)]
    [InlineData("true", NbtTagType.Byte)]
    [InlineData("1s", NbtTagType.Short)]
    [InlineData("1", NbtTagType.Int)]
    [InlineData("-2147483648", NbtTagType.Int)]
    [InlineData("1L", NbtTagType.Long)]
    [InlineData("1f", NbtTagType.Float)]
    [InlineData("1.5e3F", NbtTagType.Float)]
    [InlineData("1d", NbtTagType.Double)]
    [InlineData("1.5", NbtTagType.Double)]
    [InlineData(".5", NbtTagType.Double)]
    [InlineData("3000000000", NbtTagType.String)]
    [InlineData("128b", NbtTagType.String)]
    [InlineData("minecraft:stone", NbtTagType.String)]
    [InlineData("hello", NbtTagType.String)]
    [InlineData("1e5", NbtTagType.String)]
    public void InfersLiteralTypes(string snbt, NbtTagType expected)
    {
        // ':' isn't an unquoted character, so wrap anything that needs it in quotes first.
        var text = snbt.Contains(':') ? $"\"{snbt}\"" : snbt;

        Assert.Equal(expected, NbtTag.ParseSnbt(text).TagType);
    }

    [Fact]
    public void ParsesTypicalCommandSnbt()
    {
        var tag = NbtTag.ParseSnbt("""
            { CustomName: '{"text":"Bob"}', Health: 20.0f, NoAI: 1b,
              Pos: [0.5d, 64d, -3.25d], UUID: [I; 1, -2, 3, 4], "odd key!": "é\n" }
            """);

        var compound = Assert.IsType<NbtCompound>(tag);
        Assert.Equal("{\"text\":\"Bob\"}", compound.GetString("CustomName"));
        Assert.Equal(20f, compound.GetFloat("Health"));
        Assert.True(compound.GetBool("NoAI"));
        Assert.Equal(-3.25, compound.GetList("Pos").Get<NbtDouble>(2).Value);
        Assert.Equal([1, -2, 3, 4], compound.GetIntArray("UUID"));
        Assert.Equal("é\n", compound.GetString("odd key!"));
    }

    [Fact]
    public void ArraysAcceptAnyIntegerInRange()
    {
        var tag = NbtTag.ParseSnbt("[B; -1b, 2, 3s]");

        Assert.Equal([255, 2, 3], Assert.IsType<NbtByteArray>(tag).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{a:1")]
    [InlineData("{a 1}")]
    [InlineData("{a:1,}")]
    [InlineData("\"unterminated")]
    [InlineData("[1, \"two\"]")]
    [InlineData("[B; 300]")]
    [InlineData("[I; 1.5]")]
    [InlineData("1 2")]
    [InlineData("\"bad \\q escape\"")]
    [InlineData("{a:@}")]
    public void RejectsInvalidSnbt(string snbt)
    {
        Assert.Throws<NbtFormatException>(() => NbtTag.ParseSnbt(snbt));
    }

    [Fact]
    public void RejectsExcessiveNesting()
    {
        var snbt = new string('[', 1000) + new string(']', 1000);

        Assert.Throws<NbtFormatException>(() => NbtTag.ParseSnbt(snbt));
    }
}
