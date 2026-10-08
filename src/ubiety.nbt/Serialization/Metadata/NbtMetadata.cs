using System.Runtime.CompilerServices;

namespace Ubiety.Nbt.Serialization.Metadata;

/// <summary>
/// Built-in <see cref="NbtTypeInfo{T}"/> instances and factories, used by source-generated contexts.
/// </summary>
public static class NbtMetadata
{
    /// <summary>Gets metadata for <see cref="bool"/>, stored as a byte.</summary>
    public static NbtTypeInfo<bool> Boolean { get; } = new PrimitiveInfo<bool>(v => new NbtByte(v), NbtPrimitives.ReadBoolean);

    /// <summary>Gets metadata for <see cref="byte"/>.</summary>
    public static NbtTypeInfo<byte> Byte { get; } = new PrimitiveInfo<byte>(v => new NbtByte(v), NbtPrimitives.ReadByte);

    /// <summary>Gets metadata for <see cref="sbyte"/>.</summary>
    public static NbtTypeInfo<sbyte> SByte { get; } =
        new PrimitiveInfo<sbyte>(v => new NbtByte(unchecked((byte)v)), NbtPrimitives.ReadSByte);

    /// <summary>Gets metadata for <see cref="short"/>.</summary>
    public static NbtTypeInfo<short> Int16 { get; } = new PrimitiveInfo<short>(v => new NbtShort(v), NbtPrimitives.ReadInt16);

    /// <summary>Gets metadata for <see cref="ushort"/>, stored bit-for-bit as a short.</summary>
    public static NbtTypeInfo<ushort> UInt16 { get; } =
        new PrimitiveInfo<ushort>(v => new NbtShort(unchecked((short)v)), NbtPrimitives.ReadUInt16);

    /// <summary>Gets metadata for <see cref="int"/>.</summary>
    public static NbtTypeInfo<int> Int32 { get; } = new PrimitiveInfo<int>(v => new NbtInt(v), NbtPrimitives.ReadInt32);

    /// <summary>Gets metadata for <see cref="uint"/>, stored bit-for-bit as an int.</summary>
    public static NbtTypeInfo<uint> UInt32 { get; } =
        new PrimitiveInfo<uint>(v => new NbtInt(unchecked((int)v)), NbtPrimitives.ReadUInt32);

    /// <summary>Gets metadata for <see cref="long"/>.</summary>
    public static NbtTypeInfo<long> Int64 { get; } = new PrimitiveInfo<long>(v => new NbtLong(v), NbtPrimitives.ReadInt64);

    /// <summary>Gets metadata for <see cref="ulong"/>, stored bit-for-bit as a long.</summary>
    public static NbtTypeInfo<ulong> UInt64 { get; } =
        new PrimitiveInfo<ulong>(v => new NbtLong(unchecked((long)v)), NbtPrimitives.ReadUInt64);

    /// <summary>Gets metadata for <see cref="float"/>.</summary>
    public static NbtTypeInfo<float> Single { get; } = new PrimitiveInfo<float>(v => new NbtFloat(v), NbtPrimitives.ReadSingle);

    /// <summary>Gets metadata for <see cref="double"/>.</summary>
    public static NbtTypeInfo<double> Double { get; } = new PrimitiveInfo<double>(v => new NbtDouble(v), NbtPrimitives.ReadDouble);

    /// <summary>Gets metadata for <see cref="string"/>.</summary>
    public static NbtTypeInfo<string> String { get; } = new PrimitiveInfo<string>(v => new NbtString(v), NbtPrimitives.ReadString);

    /// <summary>Gets metadata for <see cref="System.Guid"/>, stored in Minecraft's four-int UUID format.</summary>
    public static NbtTypeInfo<Guid> Guid { get; } = new PrimitiveInfo<Guid>(NbtPrimitives.WriteGuid, NbtPrimitives.ReadGuid);

    /// <summary>Gets metadata for byte arrays, stored as a byte array tag.</summary>
    public static NbtTypeInfo<byte[]> ByteArray { get; } =
        new PrimitiveInfo<byte[]>(v => new NbtByteArray((byte[])v.Clone()), NbtPrimitives.ReadByteArray);

    /// <summary>Gets metadata for int arrays, stored as an int array tag.</summary>
    public static NbtTypeInfo<int[]> IntArray { get; } =
        new PrimitiveInfo<int[]>(v => new NbtIntArray((int[])v.Clone()), NbtPrimitives.ReadIntArray);

    /// <summary>Gets metadata for long arrays, stored as a long array tag.</summary>
    public static NbtTypeInfo<long[]> LongArray { get; } =
        new PrimitiveInfo<long[]>(v => new NbtLongArray((long[])v.Clone()), NbtPrimitives.ReadLongArray);

    /// <summary>Creates metadata for a tag type, which is copied in both directions.</summary>
    /// <typeparam name="TTag">The tag type.</typeparam>
    /// <returns>The metadata.</returns>
    public static NbtTypeInfo<TTag> CreateTagInfo<TTag>()
        where TTag : NbtTag => TagInfo<TTag>.Instance;

    /// <summary>Creates metadata for an enum, stored as its underlying integer or, optionally, its name.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="options">The options used when this metadata is the root of an operation.</param>
    /// <returns>The metadata.</returns>
    public static NbtTypeInfo<TEnum> CreateEnumInfo<TEnum>(NbtSerializerOptions? options)
        where TEnum : struct, Enum => new EnumInfo<TEnum>(options);

    /// <summary>Creates metadata for a nullable value type. Null values are omitted by the containing object.</summary>
    /// <typeparam name="T">The underlying type.</typeparam>
    /// <param name="options">The options used when this metadata is the root of an operation.</param>
    /// <param name="underlying">The metadata for <typeparamref name="T"/>.</param>
    /// <returns>The metadata.</returns>
    public static NbtTypeInfo<T?> CreateNullableInfo<T>(NbtSerializerOptions? options, NbtTypeInfo<T> underlying)
        where T : struct => new NullableInfo<T>(options, underlying);

    /// <summary>Creates metadata for an array, stored as a list.</summary>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <param name="options">The options used when this metadata is the root of an operation.</param>
    /// <param name="element">The metadata for <typeparamref name="TElement"/>.</param>
    /// <returns>The metadata.</returns>
    public static NbtTypeInfo<TElement[]> CreateArrayInfo<TElement>(NbtSerializerOptions? options, NbtTypeInfo<TElement> element) =>
        new CollectionInfo<TElement[], TElement>(options, element, static list => [.. list]);

    /// <summary>
    /// Creates metadata for <see cref="List{T}"/> or an interface it implements, such as <see cref="IReadOnlyList{T}"/>,
    /// stored as a list.
    /// </summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <param name="options">The options used when this metadata is the root of an operation.</param>
    /// <param name="element">The metadata for <typeparamref name="TElement"/>.</param>
    /// <returns>The metadata.</returns>
    public static NbtTypeInfo<TCollection> CreateListInfo<TCollection, TElement>(NbtSerializerOptions? options, NbtTypeInfo<TElement> element)
        where TCollection : IEnumerable<TElement> =>
        new CollectionInfo<TCollection, TElement>(options, element, static list => (TCollection)(object)list);

    /// <summary>
    /// Creates metadata for <see cref="Dictionary{TKey, TValue}"/> with string keys, or an interface it implements,
    /// stored as a compound.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="options">The options used when this metadata is the root of an operation.</param>
    /// <param name="value">The metadata for <typeparamref name="TValue"/>.</param>
    /// <returns>The metadata.</returns>
    public static NbtTypeInfo<TDictionary> CreateDictionaryInfo<TDictionary, TValue>(NbtSerializerOptions? options, NbtTypeInfo<TValue> value)
        where TDictionary : IEnumerable<KeyValuePair<string, TValue>> =>
        new DictionaryInfo<TDictionary, TValue>(options, value);

    private sealed class PrimitiveInfo<T>(Func<T, NbtTag> write, Func<NbtTag, NbtSerializationState, T> read) : NbtTypeInfo<T>(null)
    {
        public override NbtTag Serialize(T value, NbtSerializationState state) => write(value);

        public override T Deserialize(NbtTag tag, NbtSerializationState state) => read(tag, state);
    }

    private sealed class TagInfo<TTag>() : NbtTypeInfo<TTag>(null)
        where TTag : NbtTag
    {
        public static readonly TagInfo<TTag> Instance = new();

        public override NbtTag Serialize(TTag value, NbtSerializationState state) => value.Clone();

        public override TTag Deserialize(NbtTag tag, NbtSerializationState state) =>
            tag is TTag typed ? (TTag)typed.Clone() : throw state.Mismatch(typeof(TTag).Name, tag);
    }

    private sealed class EnumInfo<TEnum>(NbtSerializerOptions? options) : NbtTypeInfo<TEnum>(options)
        where TEnum : struct, Enum
    {
        private static readonly TypeCode UnderlyingType = Type.GetTypeCode(typeof(TEnum));

        public override NbtTag Serialize(TEnum value, NbtSerializationState state)
        {
            if (state.Options.EnumsAsStrings)
            {
                return new NbtString(value.ToString());
            }

            return UnderlyingType switch
            {
                TypeCode.Byte => new NbtByte(Unsafe.As<TEnum, byte>(ref value)),
                TypeCode.SByte => new NbtByte(Unsafe.As<TEnum, byte>(ref value)),
                TypeCode.Int16 or TypeCode.UInt16 => new NbtShort(Unsafe.As<TEnum, short>(ref value)),
                TypeCode.Int32 or TypeCode.UInt32 => new NbtInt(Unsafe.As<TEnum, int>(ref value)),
                _ => new NbtLong(Unsafe.As<TEnum, long>(ref value)),
            };
        }

        public override TEnum Deserialize(NbtTag tag, NbtSerializationState state)
        {
            if (tag is NbtString name)
            {
                return Enum.TryParse<TEnum>(name.Value, ignoreCase: false, out var result)
                    ? result
                    : throw state.Error($"'{name.Value}' is not a valid {typeof(TEnum).Name}.");
            }

            switch (UnderlyingType)
            {
                case TypeCode.Byte:
                    var b = NbtPrimitives.ReadByte(tag, state);
                    return Unsafe.As<byte, TEnum>(ref b);
                case TypeCode.SByte:
                    var sb = NbtPrimitives.ReadSByte(tag, state);
                    return Unsafe.As<sbyte, TEnum>(ref sb);
                case TypeCode.Int16:
                    var s = NbtPrimitives.ReadInt16(tag, state);
                    return Unsafe.As<short, TEnum>(ref s);
                case TypeCode.UInt16:
                    var us = NbtPrimitives.ReadUInt16(tag, state);
                    return Unsafe.As<ushort, TEnum>(ref us);
                case TypeCode.Int32:
                    var i = NbtPrimitives.ReadInt32(tag, state);
                    return Unsafe.As<int, TEnum>(ref i);
                case TypeCode.UInt32:
                    var ui = NbtPrimitives.ReadUInt32(tag, state);
                    return Unsafe.As<uint, TEnum>(ref ui);
                case TypeCode.Int64:
                    var l = NbtPrimitives.ReadInt64(tag, state);
                    return Unsafe.As<long, TEnum>(ref l);
                default:
                    var ul = NbtPrimitives.ReadUInt64(tag, state);
                    return Unsafe.As<ulong, TEnum>(ref ul);
            }
        }
    }

    private sealed class NullableInfo<T>(NbtSerializerOptions? options, NbtTypeInfo<T> underlying) : NbtTypeInfo<T?>(options)
        where T : struct
    {
        public override NbtTag Serialize(T? value, NbtSerializationState state) =>
            value is { } v ? underlying.Serialize(v, state) : throw NbtPrimitives.NullElement(state);

        public override T? Deserialize(NbtTag tag, NbtSerializationState state) => underlying.Deserialize(tag, state);
    }

    private sealed class CollectionInfo<TCollection, TElement>(
        NbtSerializerOptions? options,
        NbtTypeInfo<TElement> element,
        Func<List<TElement>, TCollection> create) : NbtTypeInfo<TCollection>(options)
    {
        public override NbtTag Serialize(TCollection value, NbtSerializationState state)
        {
            state.EnterContainer();
            var list = new NbtList();
            var index = 0;
            foreach (var item in (IEnumerable<TElement>)value!)
            {
                state.Push(index++);
                if (item is null)
                {
                    throw NbtPrimitives.NullElement(state);
                }

                NbtPrimitives.AddElement(list, element.Serialize(item, state), state);
                state.Pop();
            }

            return list;
        }

        public override TCollection Deserialize(NbtTag tag, NbtSerializationState state)
        {
            state.EnterContainer();
            var elements = NbtPrimitives.ReadElements(tag, state);
            var result = new List<TElement>(elements.Count);
            for (var i = 0; i < elements.Count; i++)
            {
                state.Push(i);
                result.Add(element.Deserialize(elements[i], state));
                state.Pop();
            }

            return create(result);
        }
    }

    private sealed class DictionaryInfo<TDictionary, TValue>(NbtSerializerOptions? options, NbtTypeInfo<TValue> valueInfo)
        : NbtTypeInfo<TDictionary>(options)
        where TDictionary : IEnumerable<KeyValuePair<string, TValue>>
    {
        public override NbtTag Serialize(TDictionary value, NbtSerializationState state)
        {
            state.EnterContainer();
            var compound = new NbtCompound();
            foreach (var (key, item) in value)
            {
                if (item is not null)
                {
                    state.WriteMember(compound, key, item, valueInfo);
                }
            }

            return compound;
        }

        public override TDictionary Deserialize(NbtTag tag, NbtSerializationState state)
        {
            var compound = state.Expect<NbtCompound>(tag, typeof(TDictionary));
            state.EnterContainer();
            var result = new Dictionary<string, TValue>(StringComparer.Ordinal);
            foreach (var (key, item) in compound)
            {
                state.Push(key);
                result[key] = valueInfo.Deserialize(item, state);
                state.Pop();
            }

            return (TDictionary)(object)result;
        }
    }
}
