using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Ubiety.Nbt.Serialization.Metadata;

namespace Ubiety.Nbt.Serialization;

/// <summary>
/// Converts between .NET values and tags using reflection. Primitive conversions are shared with
/// <see cref="NbtMetadata"/>, so this behaves the same as source-generated metadata.
/// </summary>
[RequiresUnreferencedCode(NbtSerializer.ReflectionWarning)]
[RequiresDynamicCode(NbtSerializer.ReflectionWarning)]
internal sealed class NbtConverter(NbtSerializerOptions options)
{
    private readonly NbtSerializationState _state = new(options);

    public NbtTag ToTag(object value, Type type)
    {
        var info = ReflectionTypeInfo.Get(type);
        return info.Kind switch
        {
            NbtTypeKind.Boolean => NbtMetadata.Boolean.Serialize((bool)value, _state),
            NbtTypeKind.Byte => NbtMetadata.Byte.Serialize((byte)value, _state),
            NbtTypeKind.SByte => NbtMetadata.SByte.Serialize((sbyte)value, _state),
            NbtTypeKind.Int16 => NbtMetadata.Int16.Serialize((short)value, _state),
            NbtTypeKind.UInt16 => NbtMetadata.UInt16.Serialize((ushort)value, _state),
            NbtTypeKind.Int32 => NbtMetadata.Int32.Serialize((int)value, _state),
            NbtTypeKind.UInt32 => NbtMetadata.UInt32.Serialize((uint)value, _state),
            NbtTypeKind.Int64 => NbtMetadata.Int64.Serialize((long)value, _state),
            NbtTypeKind.UInt64 => NbtMetadata.UInt64.Serialize((ulong)value, _state),
            NbtTypeKind.Single => NbtMetadata.Single.Serialize((float)value, _state),
            NbtTypeKind.Double => NbtMetadata.Double.Serialize((double)value, _state),
            NbtTypeKind.String => NbtMetadata.String.Serialize((string)value, _state),
            NbtTypeKind.Guid => NbtMetadata.Guid.Serialize((Guid)value, _state),
            NbtTypeKind.ByteArray => NbtMetadata.ByteArray.Serialize((byte[])value, _state),
            NbtTypeKind.IntArray => NbtMetadata.IntArray.Serialize((int[])value, _state),
            NbtTypeKind.LongArray => NbtMetadata.LongArray.Serialize((long[])value, _state),
            NbtTypeKind.Enum => options.EnumsAsStrings
                ? new NbtString(value.ToString()!)
                : ToTag(Convert.ChangeType(value, info.ElementType!, provider: null), info.ElementType!),
            NbtTypeKind.Tag => ((NbtTag)value).Clone(),

            // A boxed nullable with a value is boxed as its underlying type.
            NbtTypeKind.Nullable => ToTag(value, info.ElementType!),
            NbtTypeKind.Array or NbtTypeKind.List => SerializeList((IEnumerable)value, info.ElementType!),
            NbtTypeKind.Dictionary => SerializeDictionary((IEnumerable)value, info),
            _ => SerializeObject(value, info),
        };
    }

    public object? FromTag(NbtTag tag, Type type)
    {
        var info = ReflectionTypeInfo.Get(type);
        return info.Kind switch
        {
            NbtTypeKind.Boolean => NbtPrimitives.ReadBoolean(tag, _state),
            NbtTypeKind.Byte => NbtPrimitives.ReadByte(tag, _state),
            NbtTypeKind.SByte => NbtPrimitives.ReadSByte(tag, _state),
            NbtTypeKind.Int16 => NbtPrimitives.ReadInt16(tag, _state),
            NbtTypeKind.UInt16 => NbtPrimitives.ReadUInt16(tag, _state),
            NbtTypeKind.Int32 => NbtPrimitives.ReadInt32(tag, _state),
            NbtTypeKind.UInt32 => NbtPrimitives.ReadUInt32(tag, _state),
            NbtTypeKind.Int64 => NbtPrimitives.ReadInt64(tag, _state),
            NbtTypeKind.UInt64 => NbtPrimitives.ReadUInt64(tag, _state),
            NbtTypeKind.Single => NbtPrimitives.ReadSingle(tag, _state),
            NbtTypeKind.Double => NbtPrimitives.ReadDouble(tag, _state),
            NbtTypeKind.String => NbtPrimitives.ReadString(tag, _state),
            NbtTypeKind.Guid => NbtPrimitives.ReadGuid(tag, _state),
            NbtTypeKind.ByteArray => NbtPrimitives.ReadByteArray(tag, _state),
            NbtTypeKind.IntArray => NbtPrimitives.ReadIntArray(tag, _state),
            NbtTypeKind.LongArray => NbtPrimitives.ReadLongArray(tag, _state),
            NbtTypeKind.Enum => DeserializeEnum(tag, info),
            NbtTypeKind.Tag => type.IsInstanceOfType(tag) ? tag.Clone() : throw _state.Mismatch(type.Name, tag),
            NbtTypeKind.Nullable => FromTag(tag, info.ElementType!),
            NbtTypeKind.Array or NbtTypeKind.List => DeserializeList(tag, info),
            NbtTypeKind.Dictionary => DeserializeDictionary(_state.Expect<NbtCompound>(tag, type), info),
            _ => DeserializeObject(_state.Expect<NbtCompound>(tag, type), info),
        };
    }

    private NbtList SerializeList(IEnumerable values, Type elementType)
    {
        _state.EnterContainer();
        var list = new NbtList();
        var index = 0;
        foreach (var item in values)
        {
            _state.Push(index++);
            if (item is null)
            {
                throw NbtPrimitives.NullElement(_state);
            }

            NbtPrimitives.AddElement(list, ToTag(item, elementType), _state);
            _state.Pop();
        }

        return list;
    }

    private NbtCompound SerializeDictionary(IEnumerable entries, ReflectionTypeInfo info)
    {
        _state.EnterContainer();
        var (keyProperty, valueProperty) = info.EntryProperties!.Value;
        var compound = new NbtCompound();
        foreach (var entry in entries)
        {
            var key = (string)keyProperty.GetValue(entry)!;
            if (valueProperty.GetValue(entry) is { } value)
            {
                _state.Push(key);
                compound[key] = ToTag(value, info.ElementType!);
                _state.Pop();
            }
        }

        return compound;
    }

    private NbtCompound SerializeObject(object value, ReflectionTypeInfo info)
    {
        _state.EnterContainer();
        var compound = new NbtCompound();
        foreach (var member in info.Members)
        {
            // NBT has no null; null members are omitted.
            if (member.Get(value) is { } memberValue)
            {
                var key = member.GetKey(options);
                _state.Push(key);
                compound[key] = ToTag(memberValue, member.Type);
                _state.Pop();
            }
        }

        return compound;
    }

    private object DeserializeEnum(NbtTag tag, ReflectionTypeInfo info)
    {
        if (tag is NbtString name)
        {
            return Enum.TryParse(info.Type, name.Value, ignoreCase: false, out var result)
                ? result
                : throw _state.Error($"'{name.Value}' is not a valid {info.Type.Name}.");
        }

        return Enum.ToObject(info.Type, FromTag(tag, info.ElementType!)!);
    }

    private object DeserializeList(NbtTag tag, ReflectionTypeInfo info)
    {
        _state.EnterContainer();
        var elements = NbtPrimitives.ReadElements(tag, _state);
        var elementType = info.ElementType!;
        IList result = info.Kind == NbtTypeKind.Array
            ? Array.CreateInstance(elementType, elements.Count)
            : info.CreateList!();

        for (var i = 0; i < elements.Count; i++)
        {
            _state.Push(i);
            var value = FromTag(elements[i], elementType);
            if (info.Kind == NbtTypeKind.Array)
            {
                result[i] = value;
            }
            else
            {
                result.Add(value);
            }

            _state.Pop();
        }

        return result;
    }

    private IDictionary DeserializeDictionary(NbtCompound compound, ReflectionTypeInfo info)
    {
        _state.EnterContainer();
        var result = info.CreateDictionary!();
        foreach (var (key, tag) in compound)
        {
            _state.Push(key);
            result[key] = FromTag(tag, info.ElementType!);
            _state.Pop();
        }

        return result;
    }

    private object DeserializeObject(NbtCompound compound, ReflectionTypeInfo info)
    {
        _state.EnterContainer();
        var members = info.Members;
        var initialized = new bool[members.Count];

        var arguments = new object?[info.ConstructorParameters.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            var (parameter, memberIndex) = info.ConstructorParameters[i];
            initialized[memberIndex] = true;
            var member = members[memberIndex];
            var key = member.GetKey(options);
            if (_state.TryGetTag(compound, key, out var tag))
            {
                _state.Push(key);
                arguments[i] = FromTag(tag, parameter.ParameterType);
                _state.Pop();
            }
            else if (parameter.HasDefaultValue)
            {
                arguments[i] = parameter.DefaultValue;
            }
            else if (member.IsRequired)
            {
                throw _state.MissingRequired(key);
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
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw _state.Error($"The {info.Type.Name} constructor threw: {e.InnerException.Message}", e.InnerException);
        }

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (initialized[i] || member.Set is null)
            {
                continue;
            }

            var key = member.GetKey(options);
            if (_state.TryGetTag(compound, key, out var tag))
            {
                _state.Push(key);
                member.Set(instance, FromTag(tag, member.Type));
                _state.Pop();
            }
            else if (member.IsRequired)
            {
                throw _state.MissingRequired(key);
            }
        }

        return instance;
    }
}
