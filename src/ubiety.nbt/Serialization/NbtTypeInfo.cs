using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ubiety.Nbt.Serialization;

internal enum NbtTypeKind
{
    Boolean,
    Byte,
    SByte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Single,
    Double,
    String,
    Enum,
    Guid,
    ByteArray,
    IntArray,
    LongArray,
    Tag,
    Nullable,
    Array,
    List,
    Dictionary,
    Object,
}

/// <summary>
/// How a .NET type maps to NBT. Built once per type and cached.
/// </summary>
[RequiresUnreferencedCode(NbtSerializer.ReflectionWarning)]
[RequiresDynamicCode(NbtSerializer.ReflectionWarning)]
internal sealed class NbtTypeInfo
{
    private static readonly ConcurrentDictionary<Type, NbtTypeInfo> Cache = new();

    private static readonly Type[] ListInterfaces =
    [
        typeof(IEnumerable<>), typeof(ICollection<>), typeof(IList<>), typeof(IReadOnlyCollection<>), typeof(IReadOnlyList<>),
    ];

    private static readonly Type[] DictionaryInterfaces = [typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>)];

    private NbtTypeInfo(Type type, NbtTypeKind kind, Type? elementType = null)
    {
        Type = type;
        Kind = kind;
        ElementType = elementType;
    }

    public Type Type { get; }

    public NbtTypeKind Kind { get; }

    /// <summary>
    /// Gets the element type of a collection, the value type of a dictionary, the underlying type of a nullable or enum.
    /// </summary>
    public Type? ElementType { get; }

    /// <summary>Gets a factory for the <c>List&lt;T&gt;</c> backing a list kind.</summary>
    public Func<IList>? CreateList { get; private init; }

    /// <summary>Gets a factory for the <c>Dictionary&lt;string, T&gt;</c> backing a dictionary kind.</summary>
    public Func<IDictionary>? CreateDictionary { get; private init; }

    /// <summary>Gets the <c>Key</c> and <c>Value</c> properties of a dictionary's entries.</summary>
    public (PropertyInfo Key, PropertyInfo Value)? EntryProperties { get; private init; }

    public IReadOnlyList<NbtMemberInfo> Members { get; private init; } = [];

    /// <summary>Gets the constructor to call, or <see langword="null"/> to create a default struct.</summary>
    public ConstructorInfo? Constructor { get; private init; }

    /// <summary>Gets, for each constructor parameter, the index of the member it initializes.</summary>
    public IReadOnlyList<(ParameterInfo Parameter, int MemberIndex)> ConstructorParameters { get; private init; } = [];

    public static NbtTypeInfo Get(Type type) => Cache.GetOrAdd(type, Create);

    private static NbtTypeInfo Create(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return new(type, NbtTypeKind.Nullable, underlying);
        }

        if (type.IsEnum)
        {
            return new(type, NbtTypeKind.Enum, Enum.GetUnderlyingType(type));
        }

        var simple = Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => NbtTypeKind.Boolean,
            TypeCode.Byte => NbtTypeKind.Byte,
            TypeCode.SByte => NbtTypeKind.SByte,
            TypeCode.Int16 => NbtTypeKind.Int16,
            TypeCode.UInt16 => NbtTypeKind.UInt16,
            TypeCode.Int32 => NbtTypeKind.Int32,
            TypeCode.UInt32 => NbtTypeKind.UInt32,
            TypeCode.Int64 => NbtTypeKind.Int64,
            TypeCode.UInt64 => NbtTypeKind.UInt64,
            TypeCode.Single => NbtTypeKind.Single,
            TypeCode.Double => NbtTypeKind.Double,
            TypeCode.String => NbtTypeKind.String,
            _ => (NbtTypeKind?)null,
        };

        if (simple is { } kind)
        {
            return new(type, kind);
        }

        if (type == typeof(Guid))
        {
            return new(type, NbtTypeKind.Guid);
        }

        if (type == typeof(byte[]))
        {
            return new(type, NbtTypeKind.ByteArray);
        }

        if (type == typeof(int[]))
        {
            return new(type, NbtTypeKind.IntArray);
        }

        if (type == typeof(long[]))
        {
            return new(type, NbtTypeKind.LongArray);
        }

        if (typeof(NbtTag).IsAssignableFrom(type))
        {
            return new(type, NbtTypeKind.Tag);
        }

        if (type.IsSZArray)
        {
            return new(type, NbtTypeKind.Array, type.GetElementType());
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();

            if (definition == typeof(List<>) || ListInterfaces.Contains(definition))
            {
                var listType = typeof(List<>).MakeGenericType(arguments);
                return new(type, NbtTypeKind.List, arguments[0])
                {
                    CreateList = () => (IList)Activator.CreateInstance(listType)!,
                };
            }

            if ((definition == typeof(Dictionary<,>) || DictionaryInterfaces.Contains(definition)) &&
                arguments[0] == typeof(string))
            {
                var dictionaryType = typeof(Dictionary<,>).MakeGenericType(arguments);
                var entryType = typeof(KeyValuePair<,>).MakeGenericType(arguments);
                return new(type, NbtTypeKind.Dictionary, arguments[1])
                {
                    CreateDictionary = () => (IDictionary)Activator.CreateInstance(dictionaryType, StringComparer.Ordinal)!,
                    EntryProperties = (entryType.GetProperty("Key")!, entryType.GetProperty("Value")!),
                };
            }
        }

        if (type == typeof(object) || type.IsAbstract || type.IsInterface || type.IsPointer || type.IsByRef ||
            typeof(IEnumerable).IsAssignableFrom(type) || typeof(Delegate).IsAssignableFrom(type))
        {
            throw new NotSupportedException(
                $"Type {type} cannot be serialized to NBT. Use a concrete class or struct, an array, List<T>, or Dictionary<string, T>.");
        }

        return CreateObject(type);
    }

    private static NbtTypeInfo CreateObject(Type type)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var members = new List<NbtMemberInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in type.GetProperties(Flags).OrderBy(p => p.MetadataToken))
        {
            if (property.GetIndexParameters().Length > 0 || property.IsDefined(typeof(NbtIgnoreAttribute)) ||
                !seen.Add(property.Name))
            {
                continue;
            }

            var attribute = property.GetCustomAttribute<NbtPropertyAttribute>();
            if (property.GetMethod is not { } getter || (!getter.IsPublic && attribute is null))
            {
                continue;
            }

            var settable = property.SetMethod is { } setter && (setter.IsPublic || attribute is not null);
            members.Add(new NbtMemberInfo(
                property.Name,
                attribute?.Name,
                property.PropertyType,
                property.GetValue,
                settable ? property.SetValue : null,
                property.IsDefined(typeof(RequiredMemberAttribute))));
        }

        foreach (var field in type.GetFields(Flags).OrderBy(f => f.MetadataToken))
        {
            var attribute = field.GetCustomAttribute<NbtPropertyAttribute>();
            if ((!field.IsPublic && attribute is null) || field.IsDefined(typeof(NbtIgnoreAttribute)) ||
                field.IsDefined(typeof(CompilerGeneratedAttribute)) || !seen.Add(field.Name))
            {
                continue;
            }

            members.Add(new NbtMemberInfo(
                field.Name,
                attribute?.Name,
                field.FieldType,
                field.GetValue,
                field.IsInitOnly ? null : field.SetValue,
                field.IsDefined(typeof(RequiredMemberAttribute))));
        }

        var constructor = SelectConstructor(type);
        var parameters = new List<(ParameterInfo, int)>();
        foreach (var parameter in constructor?.GetParameters() ?? [])
        {
            var index = members.FindIndex(m => string.Equals(m.ClrName, parameter.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new NotSupportedException(
                    $"Constructor parameter '{parameter.Name}' of {type} does not match any serialized property or field.");
            }

            parameters.Add((parameter, index));
        }

        return new(type, NbtTypeKind.Object)
        {
            Members = members,
            Constructor = constructor,
            ConstructorParameters = parameters,
        };
    }

    private static ConstructorInfo? SelectConstructor(Type type)
    {
        var marked = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(c => c.IsDefined(typeof(NbtConstructorAttribute)))
            .ToArray();
        if (marked.Length > 1)
        {
            throw new NotSupportedException($"{type} has more than one constructor marked [NbtConstructor].");
        }

        if (marked.Length == 1)
        {
            return marked[0];
        }

        var constructors = type.GetConstructors();
        if (constructors.FirstOrDefault(c => c.GetParameters().Length == 0) is { } parameterless)
        {
            return parameterless;
        }

        if (constructors.Length == 1)
        {
            return constructors[0];
        }

        // Structs always have an implicit default constructor.
        return type.IsValueType
            ? null
            : throw new NotSupportedException(
                $"{type} needs a public parameterless constructor, a single public constructor, or one marked [NbtConstructor].");
    }
}

/// <summary>
/// A serialized property or field.
/// </summary>
internal sealed record NbtMemberInfo(
    string ClrName,
    string? ExplicitName,
    Type Type,
    Func<object?, object?> Get,
    Action<object?, object?>? Set,
    bool IsRequired)
{
    public string GetKey(NbtSerializerOptions options) =>
        ExplicitName ?? options.PropertyNamingPolicy?.Invoke(ClrName) ?? ClrName;
}
