using System.Buffers.Binary;

namespace Ubiety.Nbt.Serialization.Metadata;

/// <summary>
/// Conversions shared by the reflection-based and source-generated serializers, so both behave identically.
/// </summary>
internal static class NbtPrimitives
{
    public static long ReadInteger(NbtTag tag, Type type, NbtSerializationState state) => tag switch
    {
        NbtByte b => unchecked((sbyte)b.Value),
        NbtShort s => s.Value,
        NbtInt i => i.Value,
        NbtLong l => l.Value,
        _ => throw state.Mismatch($"an integer for {type.Name}", tag),
    };

    public static bool ReadBoolean(NbtTag tag, NbtSerializationState state) => ReadInteger(tag, typeof(bool), state) != 0;

    public static byte ReadByte(NbtTag tag, NbtSerializationState state) =>
        tag is NbtByte b ? b.Value : Narrow(tag, typeof(byte), state, static v => checked((byte)v));

    public static sbyte ReadSByte(NbtTag tag, NbtSerializationState state) =>
        tag is NbtByte b ? unchecked((sbyte)b.Value) : Narrow(tag, typeof(sbyte), state, static v => checked((sbyte)v));

    public static short ReadInt16(NbtTag tag, NbtSerializationState state) =>
        Narrow(tag, typeof(short), state, static v => checked((short)v));

    public static ushort ReadUInt16(NbtTag tag, NbtSerializationState state) =>
        tag is NbtShort s ? unchecked((ushort)s.Value) : Narrow(tag, typeof(ushort), state, static v => checked((ushort)v));

    public static int ReadInt32(NbtTag tag, NbtSerializationState state) =>
        Narrow(tag, typeof(int), state, static v => checked((int)v));

    public static uint ReadUInt32(NbtTag tag, NbtSerializationState state) =>
        tag is NbtInt i ? unchecked((uint)i.Value) : Narrow(tag, typeof(uint), state, static v => checked((uint)v));

    public static long ReadInt64(NbtTag tag, NbtSerializationState state) => ReadInteger(tag, typeof(long), state);

    public static ulong ReadUInt64(NbtTag tag, NbtSerializationState state) =>
        tag is NbtLong l ? unchecked((ulong)l.Value) : Narrow(tag, typeof(ulong), state, static v => checked((ulong)v));

    public static float ReadSingle(NbtTag tag, NbtSerializationState state) => tag switch
    {
        NbtFloat f => f.Value,
        NbtDouble d => (float)d.Value,
        _ => ReadInteger(tag, typeof(float), state),
    };

    public static double ReadDouble(NbtTag tag, NbtSerializationState state) => tag switch
    {
        NbtDouble d => d.Value,
        NbtFloat f => f.Value,
        _ => ReadInteger(tag, typeof(double), state),
    };

    public static string ReadString(NbtTag tag, NbtSerializationState state) => state.Expect<NbtString>(tag, typeof(string)).Value;

    public static byte[] ReadByteArray(NbtTag tag, NbtSerializationState state) =>
        (byte[])state.Expect<NbtByteArray>(tag, typeof(byte[])).Value.Clone();

    public static int[] ReadIntArray(NbtTag tag, NbtSerializationState state) =>
        (int[])state.Expect<NbtIntArray>(tag, typeof(int[])).Value.Clone();

    public static long[] ReadLongArray(NbtTag tag, NbtSerializationState state) =>
        (long[])state.Expect<NbtLongArray>(tag, typeof(long[])).Value.Clone();

    public static NbtIntArray WriteGuid(Guid value)
    {
        // Minecraft stores UUIDs as four big-endian ints, most significant first.
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        return new NbtIntArray(
        [
            BinaryPrimitives.ReadInt32BigEndian(bytes),
            BinaryPrimitives.ReadInt32BigEndian(bytes[4..]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[8..]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[12..]),
        ]);
    }

    public static Guid ReadGuid(NbtTag tag, NbtSerializationState state)
    {
        switch (tag)
        {
            case NbtIntArray { Value.Length: 4 } array:
                Span<byte> bytes = stackalloc byte[16];
                for (var i = 0; i < 4; i++)
                {
                    BinaryPrimitives.WriteInt32BigEndian(bytes[(i * 4)..], array.Value[i]);
                }

                return new Guid(bytes, bigEndian: true);
            case NbtString text when Guid.TryParse(text.Value, out var guid):
                // Older Minecraft versions stored some UUIDs as strings.
                return guid;
            default:
                throw state.Mismatch("an int array of length 4 or a UUID string", tag);
        }
    }

    /// <summary>Gets the elements of a list, also accepting the array tags.</summary>
    public static IReadOnlyList<NbtTag> ReadElements(NbtTag tag, NbtSerializationState state) => tag switch
    {
        NbtList list => list,
        NbtByteArray array => array.Value.Select(v => (NbtTag)new NbtByte(v)).ToList(),
        NbtIntArray array => array.Value.Select(v => (NbtTag)new NbtInt(v)).ToList(),
        NbtLongArray array => array.Value.Select(v => (NbtTag)new NbtLong(v)).ToList(),
        _ => throw state.Mismatch("a list", tag),
    };

    public static void AddElement(NbtList list, NbtTag tag, NbtSerializationState state)
    {
        try
        {
            list.Add(tag);
        }
        catch (ArgumentException e)
        {
            throw state.Error(e.Message, e);
        }
    }

    public static NbtSerializationException NullElement(NbtSerializationState state) =>
        state.Error("Collections serialized to NBT cannot contain null.");

    private static T Narrow<T>(NbtTag tag, Type type, NbtSerializationState state, Func<long, T> convert)
    {
        var value = ReadInteger(tag, type, state);
        try
        {
            return convert(value);
        }
        catch (OverflowException e)
        {
            throw state.Error($"The {tag.TagType} value {tag} is out of range for {type.Name}.", e);
        }
    }
}
