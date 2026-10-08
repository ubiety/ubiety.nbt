namespace Ubiety.Nbt.Tests;

public class TagTests
{
    [Fact]
    public void ImplicitConversionsCreateMatchingTags()
    {
        var compound = new NbtCompound
        {
            ["b"] = (byte)1,
            ["s"] = (short)2,
            ["i"] = 3,
            ["l"] = 4L,
            ["f"] = 5f,
            ["d"] = 6d,
            ["str"] = "seven",
        };

        Assert.Equal(1, compound.GetByte("b"));
        Assert.Equal(2, compound.GetShort("s"));
        Assert.Equal(3, compound.GetInt("i"));
        Assert.Equal(4L, compound.GetLong("l"));
        Assert.Equal(5f, compound.GetFloat("f"));
        Assert.Equal(6d, compound.GetDouble("d"));
        Assert.Equal("seven", compound.GetString("str"));
    }

    [Fact]
    public void CompoundPreservesInsertionOrder()
    {
        var compound = new NbtCompound { ["z"] = 1, ["a"] = 2, ["m"] = 3 };
        compound.Remove("a");
        compound["b"] = 4;

        Assert.Equal(["z", "m", "b"], compound.Keys);
    }

    [Fact]
    public void TypedGettersReportMismatches()
    {
        var compound = new NbtCompound { ["x"] = 1 };

        Assert.Throws<KeyNotFoundException>(() => compound.GetInt("missing"));
        var error = Assert.Throws<InvalidCastException>(() => compound.GetString("x"));
        Assert.Contains("Int", error.Message);
        Assert.False(compound.TryGet<NbtString>("x", out _));
        Assert.True(compound.TryGet<NbtInt>("x", out var tag));
        Assert.Equal(1, tag.Value);
    }

    [Fact]
    public void ListAdoptsFirstElementType()
    {
        var list = new NbtList();
        Assert.Equal(NbtTagType.End, list.ElementType);

        list.Add("first");

        Assert.Equal(NbtTagType.String, list.ElementType);
        Assert.Throws<ArgumentException>(() => list.Add(1));
        Assert.Throws<ArgumentException>(() => list[0] = 1);
    }

    [Fact]
    public void ListElementTypeIsFixedOnceNonEmpty()
    {
        var list = new NbtList(NbtTagType.Int);
        Assert.Throws<ArgumentException>(() => list.Add("nope"));

        list.Add(1);

        Assert.Throws<InvalidOperationException>(() => list.ElementType = NbtTagType.Long);
    }

    [Fact]
    public void CloneIsDeep()
    {
        var original = Samples.AllTypes();

        var clone = original.Clone();
        clone.GetCompound("nested").GetCompound("inner")["deep"] = 0L;
        clone.GetIntArray("intArray")[0] = 99;

        Assert.Equal(42L, original.GetCompound("nested").GetCompound("inner").GetLong("deep"));
        Assert.Equal(int.MinValue, original.GetIntArray("intArray")[0]);
        Assert.False(original.DeepEquals(clone));
    }

    [Fact]
    public void DeepEqualsIgnoresCompoundOrder()
    {
        var a = new NbtCompound { ["x"] = 1, ["y"] = "two" };
        var b = new NbtCompound { ["y"] = "two", ["x"] = 1 };

        Assert.True(a.DeepEquals(b));
        Assert.False(a.DeepEquals(new NbtCompound { ["x"] = 1L, ["y"] = "two" }));
    }

    [Fact]
    public void DeepEqualsDistinguishesListOrder()
    {
        Assert.False(new NbtList { 1, 2 }.DeepEquals(new NbtList { 2, 1 }));
    }
}
