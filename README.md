# Ubiety.Nbt

A .NET library for reading and writing Minecraft NBT (Named Binary Tag) data.

- All 13 tag types, with implicit conversions from .NET primitives and arrays
- Java Edition (big-endian, Modified UTF-8), Bedrock Edition (little-endian) and Bedrock network (varint) formats
- GZip, ZLib and LZ4 compression, auto-detected on load
- SNBT (stringified NBT) formatting and parsing
- Object serialization, with a source generator for trimmed and Native AOT apps
- Depth and allocation limits so malformed or hostile input fails cleanly

## Usage

```csharp
using Ubiety.Nbt;

// Load a file (compression is detected automatically).
var level = NbtFile.Load("level.dat");
var data = level.Root.GetCompound("Data");
Console.WriteLine(data.GetString("LevelName"));

// Modify and save; the original format and compression are kept.
data["LevelName"] = "Renamed";
level.Save("level.dat");

// Build tags directly.
var item = new NbtCompound
{
    ["id"] = "minecraft:diamond_sword",
    ["count"] = (byte)1,
    ["Enchantments"] = new NbtList
    {
        new NbtCompound { ["id"] = "minecraft:sharpness", ["lvl"] = (short)5 },
    },
};

// SNBT
string snbt = item.ToSnbt();                 // {id:"minecraft:diamond_sword",count:1b,...}
string pretty = item.ToSnbt(indented: true);
NbtTag parsed = NbtTag.ParseSnbt("{Health:20.0f,Pos:[0.5d,64d,0.5d]}");
```

### Object serialization

Map compounds to your own classes, structs and records:

```csharp
using Ubiety.Nbt.Serialization;

record BlockState(string Name, Dictionary<string, string>? Properties);

class Section
{
    public sbyte Y { get; set; }

    [NbtProperty("block_states")]
    public PalettedContainer? BlockStates { get; set; }
}

class PalettedContainer
{
    [NbtProperty("palette")] public List<BlockState> Palette { get; set; } = [];
    [NbtProperty("data")] public long[]? Data { get; set; }
}

var section = NbtSerializer.Deserialize<Section>(chunk.GetList("sections")[0]);
NbtCompound compound = NbtSerializer.Serialize(section);
```

Public properties and fields are included; `[NbtProperty("key")]` renames or opts in members, `[NbtIgnore]`
excludes them, and `NbtSerializerOptions.PropertyNamingPolicy` (e.g. `NbtNamingPolicy.SnakeCase`) converts names.
Null values are omitted. `Guid` uses Minecraft's four-int UUID format, and numeric members accept any integer tag width.

### Trimming and Native AOT

The methods above use reflection. For trimmed or Native AOT apps, declare a partial context listing your root types,
and the source generator included in the package creates reflection-free metadata for them and every type they reference:

```csharp
[NbtSerializable(typeof(Section))]
internal partial class WorldContext : NbtSerializerContext;

var section = NbtSerializer.Deserialize(tag, WorldContext.Default.Section);
NbtCompound compound = NbtSerializer.Serialize(section, WorldContext.Default.Section);
```

The context has a property per type, named after it (`Section`, `BlockState`, `ListBlockState`, ...), and the
output and errors are identical to the reflection-based methods. Pass options through the constructor, and use the
`Type` overloads when the type is only known at runtime:

```csharp
var snake = new WorldContext(new NbtSerializerOptions { PropertyNamingPolicy = NbtNamingPolicy.SnakeCase });
NbtSerializer.Serialize(section, snake.Section);

object value = NbtSerializer.Deserialize(tag, typeof(Section), WorldContext.Default);
```

Problems are reported at build time instead of at runtime:

| ID | Problem |
|----|---------|
| NBTGEN001 | The context isn't a non-generic, non-nested, non-abstract `partial` class deriving from `NbtSerializerContext` |
| NBTGEN002 | A type has no NBT representation (e.g. `decimal`, `object`, interfaces, abstract classes, other collections) |
| NBTGEN003 | A type isn't accessible from the context |
| NBTGEN004 | A type has no usable constructor, or a constructor parameter doesn't match a member |
| NBTGEN005 | A non-public member of a generic type is serialized |

### Bedrock level.dat

Bedrock `level.dat` files start with an 8-byte header, which is detected on load and written back on save:

```csharp
var level = NbtFile.Load("level.dat", NbtFormat.BedrockEdition);
Console.WriteLine(level.BedrockStorageVersion);   // e.g. 10
level.Root["LevelName"] = "Renamed";
level.Save("level.dat");
```

### Region files

```csharp
using Ubiety.Nbt.Region;

using var region = RegionFile.Open("world/region/r.0.0.mca");
foreach (var (x, z) in region.GetChunkPositions())
{
    var chunk = region.ReadChunk(x, z)!;
    Console.WriteLine($"{x},{z}: {chunk.GetString("Status")}");
}

var spawn = region.ReadChunk(0, 0)!;
spawn["InhabitedTime"] = 0L;
region.WriteChunk(0, 0, spawn);   // zlib by default, like Minecraft
region.DeleteChunk(5, 8);
```

Chunks over ~1 MiB are stored in `c.<x>.<z>.mcc` files alongside the region, as Minecraft does.
LZ4-compressed chunks (`region-file-compression=lz4`, available since 1.20.5) are read automatically;
pass `NbtCompression.Lz4` to `WriteChunk` to write them.

### Embedded NBT

For network packets or other embedded NBT, use `NbtBinaryReader` / `NbtBinaryWriter` directly.
`ReadNamelessTag` / `WriteNamelessTag` handle the nameless root used by Java Edition since 1.20.2.

## Building

```
dotnet test ubiety.nbt.slnx
```

## License

Licensed under the [Apache License 2.0](LICENSE).
