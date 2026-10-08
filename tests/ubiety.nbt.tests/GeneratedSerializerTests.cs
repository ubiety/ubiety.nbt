using Ubiety.Nbt.Serialization;
using Ubiety.Nbt.Serialization.Metadata;
using static Ubiety.Nbt.Tests.SerializerTests;

namespace Ubiety.Nbt.Tests;

[NbtSerializable(typeof(Player))]
[NbtSerializable(typeof(Required))]
[NbtSerializable(typeof(Numbers))]
[NbtSerializable(typeof(Unsigned))]
[NbtSerializable(typeof(Naming))]
[NbtSerializable(typeof(WithMarkedConstructor))]
[NbtSerializable(typeof(Node))]
[NbtSerializable(typeof(Block))]
[NbtSerializable(typeof(GameMode))]
[NbtSerializable(typeof(Guid))]
[NbtSerializable(typeof(List<int>))]
[NbtSerializable(typeof(int[]))]
[NbtSerializable(typeof(IReadOnlyList<string>))]
[NbtSerializable(typeof(List<string>))]
[NbtSerializable(typeof(GeneratedSerializerTests.Hidden))]
internal partial class TestContext : NbtSerializerContext
{
}

/// <summary>
/// Runs the serializer scenarios through source-generated metadata and checks the output matches reflection.
/// </summary>
public class GeneratedSerializerTests
{
    private static readonly TestContext Context = TestContext.Default;

    [Fact]
    public void MatchesReflectionForPlayer()
    {
        var player = SamplePlayer();

        var generated = NbtSerializer.Serialize(player, Context.Player);

        Assert.Equal(NbtSerializer.Serialize(player).ToSnbt(), generated.ToSnbt());
        Assert.False(generated.ContainsKey("Secret"));
        Assert.Equal(7, generated.GetInt("xp_level"));
    }

    [Fact]
    public void RoundTripsPlayer()
    {
        var player = SamplePlayer();

        var copy = NbtSerializer.Deserialize(NbtSerializer.Serialize(player, Context.Player), Context.Player);

        Assert.Equal(player.Name, copy.Name);
        Assert.Equal(player.Health, copy.Health);
        Assert.Equal(player.Mode, copy.Mode);
        Assert.Equal(player.Uuid, copy.Uuid);
        Assert.Equal(player.Position, copy.Position);
        Assert.Equal(player.Inventory, copy.Inventory);
        Assert.Equal(player.Stats, copy.Stats);
        Assert.Equal(player.Flags, copy.Flags);
        Assert.Equal(player.Seen, copy.Seen);
        Assert.Null(copy.Nickname);
        Assert.Equal(7, copy.Level);
        Assert.True(player.Extra.DeepEquals(copy.Extra));
    }

    [Fact]
    public void UsesConstructorsAndDefaults()
    {
        var block = NbtSerializer.Deserialize(new NbtCompound { ["Name"] = "minecraft:oak_log" }, Context.Block);
        var marked = NbtSerializer.Deserialize(new NbtCompound { ["Value"] = 4 }, Context.WithMarkedConstructor);

        Assert.Equal(new Block("minecraft:oak_log", "y"), block);
        Assert.Equal(8, marked.Doubled);
    }

    [Fact]
    public void SetsInitOnlyMembers()
    {
        var unsigned = new Unsigned { Byte = 255, UShort = ushort.MaxValue, UInt = uint.MaxValue, ULong = ulong.MaxValue };

        var compound = NbtSerializer.Serialize(unsigned, Context.Unsigned);

        Assert.Equal("{Byte:-1b,UShort:-1s,UInt:-1,ULong:-1L}", compound.ToSnbt());
        Assert.Equal(unsigned, NbtSerializer.Deserialize(compound, Context.Unsigned));
    }

    [Fact]
    public void ReadsAndWritesNonPublicMembers()
    {
        var hidden = Hidden.Create(5, "secret");

        var compound = NbtSerializer.Serialize(hidden, Context.Hidden);
        var copy = NbtSerializer.Deserialize(compound, Context.Hidden);

        Assert.Equal("{Label:\"secret\",count:5}", compound.ToSnbt());
        Assert.Equal(NbtSerializer.Serialize(hidden).ToSnbt(), compound.ToSnbt());
        Assert.Equal((5, "secret"), (copy.Count, copy.Label));
    }

    [Fact]
    public void ReportsSameErrorsAsReflection()
    {
        var compound = NbtSerializer.Serialize(SamplePlayer());
        compound.GetList("Inventory").Get<NbtCompound>(1)["Count"] = "lots";

        var generated = Assert.Throws<NbtSerializationException>(() => NbtSerializer.Deserialize(compound, Context.Player));
        var reflection = Assert.Throws<NbtSerializationException>(() => NbtSerializer.Deserialize<Player>(compound));

        Assert.Equal("$.Inventory[1].Count", generated.Path);
        Assert.Equal(reflection.Message, generated.Message);
    }

    [Fact]
    public void EnforcesRequiredMembers()
    {
        var error = Assert.Throws<NbtSerializationException>(() => NbtSerializer.Deserialize(new NbtCompound(), Context.Required));

        Assert.Contains("'Id'", error.Message);
        Assert.Equal("x", NbtSerializer.Deserialize(new NbtCompound { ["Id"] = "x" }, Context.Required).Id);
    }

    [Fact]
    public void ConvertsNumbersLikeReflection()
    {
        var tag = NbtTag.ParseSnbt("{Int:5b,Long:7s,Short:300,Double:2,Float:1.5d,Flag:1}");

        var value = NbtSerializer.Deserialize(tag, Context.Numbers);

        Assert.Equal((5, 7L, (short)300, 2d, 1.5f, true), (value.Int, value.Long, value.Short, value.Double, value.Float, value.Flag));
        var error = Assert.Throws<NbtSerializationException>(() => NbtSerializer.Deserialize(NbtTag.ParseSnbt("{Short:70000}"), Context.Numbers));
        Assert.Equal("$.Short", error.Path);
    }

    [Fact]
    public void AppliesContextOptions()
    {
        var snake = new TestContext(new NbtSerializerOptions { PropertyNamingPolicy = NbtNamingPolicy.SnakeCase, EnumsAsStrings = true });

        Assert.Equal(["block_entities", "x_pos", "uuid_most", "level2_data", "explicit"], NbtSerializer.Serialize(new Naming(), snake.Naming).Keys);
        Assert.Equal("Creative", Assert.IsType<NbtString>(NbtSerializer.SerializeToTag(GameMode.Creative, snake.GameMode)).Value);
        Assert.Equal("Creative", NbtSerializer.Serialize(SamplePlayer(), snake.Player).GetString("mode"));
    }

    [Fact]
    public void SerializesCollectionsAndPrimitives()
    {
        Assert.Equal("[1,2,3]", NbtSerializer.SerializeToTag(new List<int> { 1, 2, 3 }, Context.ListInt32).ToSnbt());
        Assert.Equal("[I;1,2,3]", NbtSerializer.SerializeToTag(new[] { 1, 2, 3 }, Context.Int32Array).ToSnbt());
        Assert.Equal(["a", "b"], NbtSerializer.Deserialize(NbtTag.ParseSnbt("[a,b]"), Context.IReadOnlyListString));
        Assert.Equal(
            Guid.Parse("f84c6a79-0a4e-45e0-879b-cd49ebd4c4e2"),
            NbtSerializer.Deserialize(NbtSerializer.SerializeToTag(Guid.Parse("f84c6a79-0a4e-45e0-879b-cd49ebd4c4e2"), Context.Guid), Context.Guid));
    }

    [Fact]
    public void RejectsNullElementsAndCycles()
    {
        var nulls = Assert.Throws<NbtSerializationException>(() =>
            NbtSerializer.SerializeToTag(new List<string> { "a", null! }, Context.ListString));
        var node = new Node();
        node.Children.Add(node);

        Assert.Equal("$[1]", nulls.Path);
        Assert.Throws<NbtSerializationException>(() => NbtSerializer.Serialize(node, Context.Node));
    }

    [Fact]
    public void SupportsNonGenericApi()
    {
        var tag = NbtSerializer.SerializeToTag(SamplePlayer(), typeof(Player), Context);

        var player = Assert.IsType<Player>(NbtSerializer.Deserialize(tag, typeof(Player), Context));

        Assert.Equal("Steve", player.Name);
        Assert.Same(Context.Player, Context.GetTypeInfo(typeof(Player)));
        Assert.Null(Context.GetTypeInfo(typeof(Uri)));
        Assert.Throws<ArgumentException>(() => NbtSerializer.Deserialize(tag, typeof(Uri), Context));
    }

    [Fact]
    public void IncludesReferencedTypesTransitively()
    {
        NbtTypeInfo[] infos = [Context.Item, Context.Pos, Context.ListItem, Context.DictionaryStringInt32, Context.NbtCompound, Context.ByteArray];

        Assert.All(infos, Assert.NotNull);
    }

    public sealed class Hidden
    {
        [NbtProperty("count")]
        private int _count;

        [NbtIgnore]
        public int Count => _count;

        [NbtProperty]
        public string Label { get; private set; } = string.Empty;

        public static Hidden Create(int count, string label) => new() { _count = count, Label = label };
    }
}
