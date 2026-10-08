using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Ubiety.Nbt.Serialization;

/// <summary>
/// Converts between .NET values and tags, tracking the current path for error messages.
/// </summary>
[RequiresUnreferencedCode(NbtSerializer.ReflectionWarning)]
[RequiresDynamicCode(NbtSerializer.ReflectionWarning)]
internal sealed class NbtConverter(NbtSerializerOptions options)
{
    private readonly List<(string? Name, int Index)> _path = [];

    public NbtTag ToTag(object value, Type type)
    {
        var info = NbtTypeInfo.Get(type);
        switch (info.Kind)
        {
            case NbtTypeKind.Boolean:
                return new NbtByte((bool)value);
            case NbtTypeKind.Byte:
                return new NbtByte((byte)value);
            case NbtTypeKind.SByte:
                return new NbtByte(unchecked((byte)(sbyte)value));
            case NbtTypeKind.Int16:
                return new NbtShort((short)value);
            case NbtTypeKind.UInt16:
                return new NbtShort(unchecked((short)(ushort)value));
            case NbtTypeKind.Int32:
                return new NbtInt((int)value);
            case NbtTypeKind.UInt32:
                return new NbtInt(unchecked((int)(uint)value));
            case NbtTypeKind.Int64:
                return new NbtLong((long)value);
            case NbtTypeKind.UInt64:
                return new NbtLong(unchecked((long)(ulong)value));
            case NbtTypeKind.Single:
                return new NbtFloat((float)value);
            case NbtTypeKind.Double:
                return new NbtDouble((double)value);
            case NbtTypeKind.String:
                return new NbtString((string)value);
            case NbtTypeKind.Enum:
                return options.EnumsAsStrings
                    ? new NbtString(value.ToString()!)
                    : ToTag(Convert.ChangeType(value, info.ElementType!, provider: null), info.ElementType!);
            case NbtTypeKind.Guid:
                return new NbtIntArray(GuidToInts((Guid)value));
            case NbtTypeKind.ByteArray:
                return new NbtByteArray((byte[])((byte[])value).Clone());
            case NbtTypeKind.IntArray:
                return new NbtIntArray((int[])((int[])value).Clone());
            case NbtTypeKind.LongArray:
                return new NbtLongArray((long[])((long[])value).Clone());
            case NbtTypeKind.Tag:
                return ((NbtTag)value).Clone();
            case NbtTypeKind.Nullable:
                // A boxed nullable with a value is boxed as its underlying type.
                return ToTag(value, info.ElementType!);
            case NbtTypeKind.Array or NbtTypeKind.List:
                return SerializeList((IEnumerable)value, info.ElementType!);
            case NbtTypeKind.Dictionary:
                return SerializeDictionary((IEnumerable)value, info);
            default:
                return SerializeObject(value, info);
        }
    }

    public object? FromTag(NbtTag tag, Type type)
    {
        var info = NbtTypeInfo.Get(type);
        try
        {
            return info.Kind switch
            {
                NbtTypeKind.Boolean => Integer(tag, type) != 0,
                NbtTypeKind.Byte => tag is NbtByte b ? b.Value : checked((byte)Integer(tag, type)),
                NbtTypeKind.SByte => tag is NbtByte b ? unchecked((sbyte)b.Value) : checked((sbyte)Integer(tag, type)),
                NbtTypeKind.Int16 => checked((short)Integer(tag, type)),
                NbtTypeKind.UInt16 => tag is NbtShort s ? unchecked((ushort)s.Value) : checked((ushort)Integer(tag, type)),
                NbtTypeKind.Int32 => checked((int)Integer(tag, type)),
                NbtTypeKind.UInt32 => tag is NbtInt i ? unchecked((uint)i.Value) : checked((uint)Integer(tag, type)),
                NbtTypeKind.Int64 => Integer(tag, type),
                NbtTypeKind.UInt64 => tag is NbtLong l ? unchecked((ulong)l.Value) : checked((ulong)Integer(tag, type)),
                NbtTypeKind.Single => tag switch
                {
                    NbtFloat f => f.Value,
                    NbtDouble d => (float)d.Value,
                    _ => (float)Integer(tag, type),
                },
                NbtTypeKind.Double => tag switch
                {
                    NbtDouble d => d.Value,
                    NbtFloat f => f.Value,
                    _ => (double)Integer(tag, type),
                },
                NbtTypeKind.String => Expect<NbtString>(tag, type).Value,
                NbtTypeKind.Enum => DeserializeEnum(tag, info),
                NbtTypeKind.Guid => DeserializeGuid(tag),
                NbtTypeKind.ByteArray => Expect<NbtByteArray>(tag, type).Value.Clone(),
                NbtTypeKind.IntArray => Expect<NbtIntArray>(tag, type).Value.Clone(),
                NbtTypeKind.LongArray => Expect<NbtLongArray>(tag, type).Value.Clone(),
                NbtTypeKind.Tag => type.IsInstanceOfType(tag) ? tag.Clone() : throw Mismatch(type.Name, tag),
                NbtTypeKind.Nullable => FromTag(tag, info.ElementType!),
                NbtTypeKind.Array or NbtTypeKind.List => DeserializeList(tag, info),
                NbtTypeKind.Dictionary => DeserializeDictionary(Expect<NbtCompound>(tag, type), info),
                _ => DeserializeObject(Expect<NbtCompound>(tag, type), info),
            };
        }
        catch (OverflowException e)
        {
            throw Error($"The {tag.TagType} value {tag} is out of range for {type.Name}.", e);
        }
    }

    private static int[] GuidToInts(Guid value)
    {
        // Minecraft stores UUIDs as four big-endian ints, most significant first.
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        return
        [
            BinaryPrimitives.ReadInt32BigEndian(bytes),
            BinaryPrimitives.ReadInt32BigEndian(bytes[4..]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[8..]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[12..]),
        ];
    }

    private NbtList SerializeList(IEnumerable values, Type elementType)
    {
        EnterContainer();
        var list = new NbtList();
        var index = 0;
        foreach (var item in values)
        {
            _path.Add((null, index++));
            if (item is null)
            {
                throw Error("Collections serialized to NBT cannot contain null.");
            }

            var tag = ToTag(item, elementType);
            try
            {
                list.Add(tag);
            }
            catch (ArgumentException e)
            {
                throw Error(e.Message, e);
            }

            _path.RemoveAt(_path.Count - 1);
        }

        return list;
    }

    private NbtCompound SerializeDictionary(IEnumerable entries, NbtTypeInfo info)
    {
        EnterContainer();
        var (keyProperty, valueProperty) = info.EntryProperties!.Value;
        var compound = new NbtCompound();
        foreach (var entry in entries)
        {
            var key = (string)keyProperty.GetValue(entry)!;
            if (valueProperty.GetValue(entry) is { } value)
            {
                _path.Add((key, 0));
                compound[key] = ToTag(value, info.ElementType!);
                _path.RemoveAt(_path.Count - 1);
            }
        }

        return compound;
    }

    private NbtCompound SerializeObject(object value, NbtTypeInfo info)
    {
        EnterContainer();
        var compound = new NbtCompound();
        foreach (var member in info.Members)
        {
            // NBT has no null; null members are omitted.
            if (member.Get(value) is { } memberValue)
            {
                var key = member.GetKey(options);
                _path.Add((key, 0));
                compound[key] = ToTag(memberValue, member.Type);
                _path.RemoveAt(_path.Count - 1);
            }
        }

        return compound;
    }

    private object DeserializeEnum(NbtTag tag, NbtTypeInfo info)
    {
        if (tag is NbtString name)
        {
            return Enum.TryParse(info.Type, name.Value, ignoreCase: false, out var result)
                ? result
                : throw Error($"'{name.Value}' is not a valid {info.Type.Name}.");
        }

        return Enum.ToObject(info.Type, FromTag(tag, info.ElementType!)!);
    }

    private Guid DeserializeGuid(NbtTag tag)
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
                throw Mismatch("an int array of length 4 or a UUID string", tag);
        }
    }

    private object DeserializeList(NbtTag tag, NbtTypeInfo info)
    {
        EnterContainer();
        IReadOnlyList<NbtTag> elements = tag switch
        {
            NbtList list => list,
            NbtByteArray array => array.Value.Select(v => (NbtTag)new NbtByte(v)).ToList(),
            NbtIntArray array => array.Value.Select(v => (NbtTag)new NbtInt(v)).ToList(),
            NbtLongArray array => array.Value.Select(v => (NbtTag)new NbtLong(v)).ToList(),
            _ => throw Mismatch("a list", tag),
        };

        var elementType = info.ElementType!;
        IList result = info.Kind == NbtTypeKind.Array
            ? Array.CreateInstance(elementType, elements.Count)
            : info.CreateList!();

        for (var i = 0; i < elements.Count; i++)
        {
            _path.Add((null, i));
            var value = FromTag(elements[i], elementType);
            if (info.Kind == NbtTypeKind.Array)
            {
                result[i] = value;
            }
            else
            {
                result.Add(value);
            }

            _path.RemoveAt(_path.Count - 1);
        }

        return result;
    }

    private IDictionary DeserializeDictionary(NbtCompound compound, NbtTypeInfo info)
    {
        EnterContainer();
        var result = info.CreateDictionary!();
        foreach (var (key, tag) in compound)
        {
            _path.Add((key, 0));
            result[key] = FromTag(tag, info.ElementType!);
            _path.RemoveAt(_path.Count - 1);
        }

        return result;
    }

    private object DeserializeObject(NbtCompound compound, NbtTypeInfo info)
    {
        EnterContainer();
        var members = info.Members;
        var initialized = new bool[members.Count];

        var arguments = new object?[info.ConstructorParameters.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            var (parameter, memberIndex) = info.ConstructorParameters[i];
            initialized[memberIndex] = true;
            var member = members[memberIndex];
            if (TryGetMemberTag(compound, member, out var key, out var tag))
            {
                _path.Add((key, 0));
                arguments[i] = FromTag(tag, parameter.ParameterType);
                _path.RemoveAt(_path.Count - 1);
            }
            else if (parameter.HasDefaultValue)
            {
                arguments[i] = parameter.DefaultValue;
            }
            else if (member.IsRequired)
            {
                throw Error($"Required key '{key}' is missing.");
            }

            // Otherwise null, which reflection passes as default(T) for value types.
        }

        object instance;
        try
        {
            instance = info.Constructor is null
                ? Activator.CreateInstance(info.Type)!
                : info.Constructor.Invoke(arguments);
        }
        catch (System.Reflection.TargetInvocationException e) when (e.InnerException is not null)
        {
            throw Error($"The {info.Type.Name} constructor threw: {e.InnerException.Message}", e.InnerException);
        }

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (initialized[i] || member.Set is null)
            {
                continue;
            }

            if (TryGetMemberTag(compound, member, out var key, out var tag))
            {
                _path.Add((key, 0));
                member.Set(instance, FromTag(tag, member.Type));
                _path.RemoveAt(_path.Count - 1);
            }
            else if (member.IsRequired)
            {
                throw Error($"Required key '{key}' is missing.");
            }
        }

        return instance;
    }

    private bool TryGetMemberTag(NbtCompound compound, NbtMemberInfo member, out string key, [NotNullWhen(true)] out NbtTag? tag)
    {
        key = member.GetKey(options);
        if (compound.TryGetValue(key, out tag))
        {
            return true;
        }

        if (options.PropertyNameCaseInsensitive)
        {
            foreach (var (name, value) in compound)
            {
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    tag = value;
                    return true;
                }
            }
        }

        return false;
    }

    private long Integer(NbtTag tag, Type type) => tag switch
    {
        NbtByte b => unchecked((sbyte)b.Value),
        NbtShort s => s.Value,
        NbtInt i => i.Value,
        NbtLong l => l.Value,
        _ => throw Mismatch($"an integer for {type.Name}", tag),
    };

    private T Expect<T>(NbtTag tag, Type type)
        where T : NbtTag =>
        tag as T ?? throw Mismatch($"{typeof(T).Name[3..]} for {type.Name}", tag);

    private void EnterContainer()
    {
        if (_path.Count >= options.MaxDepth)
        {
            throw Error($"The maximum depth of {options.MaxDepth} was exceeded. Does the object graph contain a cycle?");
        }
    }

    private NbtSerializationException Mismatch(string expected, NbtTag actual) =>
        Error($"Expected {expected} but found {actual.TagType}.");

    private NbtSerializationException Error(string message, Exception? inner = null)
    {
        var path = new StringBuilder("$");
        foreach (var (name, index) in _path)
        {
            if (name is null)
            {
                path.Append('[').Append(index).Append(']');
            }
            else
            {
                path.Append('.').Append(name);
            }
        }

        return new NbtSerializationException(message, path.ToString(), inner);
    }
}
