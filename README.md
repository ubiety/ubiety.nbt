# Ubiety.Nbt

A .NET library for reading and writing Minecraft NBT (Named Binary Tag) data.

- All 13 tag types, with implicit conversions from .NET primitives and arrays
- Java Edition (big-endian, Modified UTF-8), Bedrock Edition (little-endian) and Bedrock network (varint) formats
- GZip, ZLib and LZ4 compression, auto-detected on load
- SNBT (stringified NBT) formatting and parsing
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
