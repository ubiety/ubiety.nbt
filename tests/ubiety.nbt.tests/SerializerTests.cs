using Ubiety.Nbt.Serialization;

namespace Ubiety.Nbt.Tests;

public class SerializerTests
{
    public enum GameMode
    {
        Survival,
        Creative,
        Adventure,
        Spectator,
    }

    [Fact]
    public void RoundTripsPlayer()
    {
        var player = SamplePlayer();

        var compound = NbtSerializer.Serialize(player);
        var copy = NbtSerializer.Deserialize<Player>(compound);

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
    public void ProducesExpectedTags()
    {
        var compound = NbtSerializer.Serialize(SamplePlayer());

        Assert.Equal(
            "{Name:\"Steve\",Health:20f,Mode:1,Uuid:[I;-129209735,172901856,-2019832503,-338377502]," +
            "Position:{X:1,Y:64,Z:-3},Inventory:[{Id:\"minecraft:stone\",Count:64b},{Id:\"minecraft:torch\",Count:3b}]," +
            "Stats:{jumps:12,deaths:0},Flags:[B;1b,2b],Seen:[I;5,6],Extra:{custom:1b},xp_level:7}",
            compound.ToSnbt());
        Assert.False(compound.ContainsKey("Nickname"));
        Assert.False(compound.ContainsKey("Secret"));
    }

    [Fact]
    public void WritesMinecraftUuidFormat()
    {
        // Notch's UUID; Minecraft stores it as four big-endian ints, most significant first.
        var tag = NbtSerializer.SerializeToTag(Guid.Parse("f84c6a79-0a4e-45e0-879b-cd49ebd4c4e2"));

        Assert.Equal(
            [unchecked((int)0xF84C6A79), 0x0A4E45E0, unchecked((int)0x879BCD49), unchecked((int)0xEBD4C4E2)],
            Assert.IsType<NbtIntArray>(tag).Value);
    }

    [Fact]
    public void ReadsUuidStrings()
    {
        var guid = NbtSerializer.Deserialize<Guid>(new NbtString("f84c6a79-0a4e-45e0-879b-cd49ebd4c4e2"));

        Assert.Equal(Guid.Parse("f84c6a79-0a4e-45e0-879b-cd49ebd4c4e2"), guid);
    }

    [Fact]
    public void SurvivesBinaryRoundTrip()
    {
        var file = new NbtFile(NbtSerializer.Serialize(SamplePlayer())) { Compression = NbtCompression.GZip };

        var copy = NbtSerializer.Deserialize<Player>(NbtFile.Load(file.ToArray()).Root);

        Assert.Equal(SamplePlayer().Inventory, copy.Inventory);
    }

    [Fact]
    public void UsesConstructorForRecords()
    {
        var item = NbtSerializer.Deserialize<Item>(new NbtCompound { ["Id"] = "minecraft:apple", ["Count"] = (byte)5 });

        Assert.Equal(new Item("minecraft:apple", 5), item);
    }

    [Fact]
    public void UsesConstructorDefaultsForMissingKeys()
    {
        var block = NbtSerializer.Deserialize<Block>(new NbtCompound { ["Name"] = "minecraft:oak_log" });

        Assert.Equal("minecraft:oak_log", block.Name);
        Assert.Equal("y", block.Axis);
    }

    [Fact]
    public void UsesMarkedConstructor()
    {
        var value = NbtSerializer.Deserialize<WithMarkedConstructor>(new NbtCompound { ["Value"] = 4 });

        Assert.Equal(8, value.Doubled);
    }

    [Fact]
    public void DeserializesStructs()
    {
        var pos = NbtSerializer.Deserialize<Pos>(NbtTag.ParseSnbt("{X:1,Y:2,Z:3}"));

        Assert.Equal(new Pos { X = 1, Y = 2, Z = 3 }, pos);
    }

    [Fact]
    public void MissingRequiredMemberThrows()
    {
        var error = Assert.Throws<NbtSerializationException>(() => NbtSerializer.Deserialize<Required>(new NbtCompound()));

        Assert.Contains("'Id'", error.Message);
    }

    [Fact]
    public void WidensAndNarrowsIntegersWithOverflowChecks()
    {
        var value = NbtSerializer.Deserialize<Numbers>(NbtTag.ParseSnbt("{Int:5b,Long:7s,Short:300,Double:2,Float:1.5d,Flag:1}"));

        Assert.Equal(5, value.Int);
        Assert.Equal(7L, value.Long);
        Assert.Equal((short)300, value.Short);
        Assert.Equal(2d, value.Double);
        Assert.Equal(1.5f, value.Float);
        Assert.True(value.Flag);

        var error = Assert.Throws<NbtSerializationException>(() => NbtSerializer.Deserialize<Numbers>(NbtTag.ParseSnbt("{Short:70000}")));
        Assert.Equal("$.Short", error.Path);
    }

    [Fact]
    public void ReinterpretsUnsignedValuesBitForBit()
    {
        var unsigned = new Unsigned { Byte = 255, UShort = ushort.MaxValue, UInt = uint.MaxValue, ULong = ulong.MaxValue };

        var compound = NbtSerializer.Serialize(unsigned);

        Assert.Equal("{Byte:-1b,UShort:-1s,UInt:-1,ULong:-1L}", compound.ToSnbt());
        Assert.Equal(unsigned, NbtSerializer.Deserialize<Unsigned>(compound));
    }

    [Fact]
    public void ReportsPathOfMismatchedTag()
    {
        var compound = NbtSerializer.Serialize(SamplePlayer());
        compound.GetList("Inventory").Get<NbtCompound>(1)["Count"] = "lots";

        var error = Assert.Throws<NbtSerializationException>(() => NbtSerializer.Deserialize<Player>(compound));

        Assert.Equal("$.Inventory[1].Count", error.Path);
        Assert.Contains("integer", error.Message);
    }

    [Fact]
    public void AppliesNamingPolicies()
    {
        var options = new NbtSerializerOptions { PropertyNamingPolicy = NbtNamingPolicy.SnakeCase };

        var compound = NbtSerializer.Serialize(new Naming(), options);

        Assert.Equal(["block_entities", "x_pos", "uuid_most", "level2_data", "explicit"], compound.Keys);
        Assert.Equal(["blockEntities", "xPos", "uuidMost", "level2Data", "explicit"],
            NbtSerializer.Serialize(new Naming(), new NbtSerializerOptions { PropertyNamingPolicy = NbtNamingPolicy.CamelCase }).Keys);
    }

    [Fact]
    public void MatchesKeysCaseInsensitivelyWhenAsked()
    {
        var compound = new NbtCompound { ["x"] = 1, ["Y"] = 2, ["z"] = 3 };

        Assert.Equal(0, NbtSerializer.Deserialize<Pos>(compound).X);
        Assert.Equal(1, NbtSerializer.Deserialize<Pos>(compound, new NbtSerializerOptions { PropertyNameCaseInsensitive = true }).X);
    }

    [Fact]
    public void WritesEnumsAsStringsWhenAsked()
    {
        var options = new NbtSerializerOptions { EnumsAsStrings = true };

        var tag = NbtSerializer.SerializeToTag(GameMode.Adventure, options);

        Assert.Equal("Adventure", Assert.IsType<NbtString>(tag).Value);
        Assert.Equal(GameMode.Adventure, NbtSerializer.Deserialize<GameMode>(tag));
        Assert.Throws<NbtSerializationException>(() => NbtSerializer.Deserialize<GameMode>(new NbtString("Hardcore")));
    }

    [Fact]
    public void SerializesCollectionsToTags()
    {
        Assert.Equal("[1,2,3]", NbtSerializer.SerializeToTag(new List<int> { 1, 2, 3 }).ToSnbt());
        Assert.Equal("[I;1,2,3]", NbtSerializer.SerializeToTag(new[] { 1, 2, 3 }).ToSnbt());
        Assert.Equal([1, 2, 3], NbtSerializer.Deserialize<List<int>>(new NbtIntArray([1, 2, 3])));
        Assert.Equal(["a", "b"], NbtSerializer.Deserialize<IReadOnlyList<string>>(NbtTag.ParseSnbt("[a,b]")));
        Assert.Throws<InvalidOperationException>(() => NbtSerializer.Serialize(new List<int>()));
    }

    [Fact]
    public void CopiesTagMembers()
    {
        var player = SamplePlayer();

        var compound = NbtSerializer.Serialize(player);
        player.Extra["custom"] = false;

        Assert.True(compound.GetCompound("Extra").GetBool("custom"));
    }

    [Fact]
    public void RejectsNullCollectionElements()
    {
        var error = Assert.Throws<NbtSerializationException>(() =>
            NbtSerializer.SerializeToTag(new List<string?> { "a", null }));

        Assert.Equal("$[1]", error.Path);
    }

    [Fact]
    public void RejectsCycles()
    {
        var node = new Node();
        node.Children.Add(node);

        Assert.Throws<NbtSerializationException>(() => NbtSerializer.Serialize(node));
    }

    [Fact]
    public void RejectsUnsupportedTypes()
    {
        Assert.Throws<NotSupportedException>(() => NbtSerializer.Serialize(new Untyped()));
        Assert.Throws<NotSupportedException>(() => NbtSerializer.SerializeToTag(new Queue<int>()));
    }

    internal static Player SamplePlayer() => new()
    {
        Name = "Steve",
        Health = 20f,
        Mode = GameMode.Creative,
        Uuid = Guid.Parse("f84c6a79-0a4e-45e0-879b-cd49ebd4c4e2"),
        Position = new Pos { X = 1, Y = 64, Z = -3 },
        Inventory = [new Item("minecraft:stone", 64), new Item("minecraft:torch", 3)],
        Stats = new Dictionary<string, int> { ["jumps"] = 12, ["deaths"] = 0 },
        Flags = [1, 2],
        Seen = [5, 6],
        Extra = new NbtCompound { ["custom"] = true },
        Secret = "hidden",
        Level = 7,
    };

    public sealed class Player
    {
        public string Name { get; set; } = string.Empty;

        public float Health { get; set; }

        public GameMode Mode { get; set; }

        public Guid Uuid { get; set; }

        public Pos Position { get; set; }

        public List<Item> Inventory { get; set; } = [];

        public Dictionary<string, int> Stats { get; set; } = [];

        public byte[] Flags { get; set; } = [];

        public int[] Seen { get; set; } = [];

        public NbtCompound Extra { get; set; } = [];

        public string? Nickname { get; set; }

        [NbtIgnore]
        public string? Secret { get; set; }

        [NbtIgnore]
        public int Level
        {
            get => _level;
            set => _level = value;
        }

        [NbtProperty("xp_level")]
        private int _level;
    }

    public sealed record Item(string Id, byte Count);

    public sealed record Block(string Name, string Axis = "y");

    public struct Pos
    {
        public int X;
        public int Y;
        public int Z;
    }

    public sealed class Required
    {
        public required string Id { get; init; }
    }

    public sealed class Numbers
    {
        public int Int { get; set; }

        public long Long { get; set; }

        public short Short { get; set; }

        public double Double { get; set; }

        public float Float { get; set; }

        public bool Flag { get; set; }
    }

    public sealed record Unsigned
    {
        public byte Byte { get; init; }

        public ushort UShort { get; init; }

        public uint UInt { get; init; }

        public ulong ULong { get; init; }
    }

    public sealed class Naming
    {
        public int BlockEntities { get; set; }

        public int XPos { get; set; }

        public int UUIDMost { get; set; }

        public int Level2Data { get; set; }

        [NbtProperty("explicit")]
        public int Renamed { get; set; }
    }

    public sealed class WithMarkedConstructor
    {
        public WithMarkedConstructor()
        {
        }

        [NbtConstructor]
        public WithMarkedConstructor(int value)
        {
            Value = value;
            Doubled = value * 2;
        }

        public int Value { get; }

        [NbtIgnore]
        public int Doubled { get; }
    }

    public sealed class Node
    {
        public List<Node> Children { get; set; } = [];
    }

    public sealed class Untyped
    {
        public object? Value { get; set; } = 1;
    }
}
