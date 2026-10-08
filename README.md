# Ubiety.Nbt

A .NET library for reading and writing Minecraft NBT (Named Binary Tag) data.

- All 13 tag types, with implicit conversions from .NET primitives and arrays
- Java Edition (big-endian, Modified UTF-8), Bedrock Edition (little-endian) and Bedrock network (varint) formats
- GZip / ZLib compression, auto-detected on load
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

For network packets or other embedded NBT, use `NbtBinaryReader` / `NbtBinaryWriter` directly.
`ReadNamelessTag` / `WriteNamelessTag` handle the nameless root used by Java Edition since 1.20.2.

## Building

```
dotnet test ubiety.nbt.slnx
```
