using System.Diagnostics.CodeAnalysis;

namespace Ubiety.Nbt.Serialization;

/// <summary>
/// Converts .NET objects to and from NBT compounds.
/// </summary>
/// <remarks>
/// <para>
/// Public properties and fields are serialized; <see cref="NbtPropertyAttribute"/> renames members or opts in
/// non-public ones, and <see cref="NbtIgnoreAttribute"/> excludes them. Null values are omitted, since NBT has no
/// null, and missing keys leave members at their defaults (or fail for C# <c>required</c> members).
/// </para>
/// <para>
/// Objects are created with a public parameterless constructor, a single public constructor, or the one marked
/// <see cref="NbtConstructorAttribute"/>; constructor parameters match members by name, so records work.
/// Members are serialized according to their declared type; polymorphism is not supported.
/// </para>
/// <para>
/// Numeric members accept any integer tag when deserializing, with overflow checking, since Minecraft has changed
/// the stored width of some values between versions. <see cref="Guid"/> maps to Minecraft's four-int UUID format.
/// </para>
/// </remarks>
public static class NbtSerializer
{
    internal const string ReflectionWarning =
        "NBT serialization uses reflection over the serialized types, which may be trimmed or need runtime code generation.";

    /// <summary>Serializes an object to a compound.</summary>
    /// <typeparam name="T">The type to serialize as.</typeparam>
    /// <param name="value">The object.</param>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="NbtSerializerOptions.Default"/>.</param>
    /// <returns>The compound.</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> does not serialize to a compound.</exception>
    /// <exception cref="NbtSerializationException">The object cannot be represented, e.g. a collection contains null.</exception>
    /// <exception cref="NotSupportedException">A type in the object graph is not supported.</exception>
    [RequiresUnreferencedCode(ReflectionWarning)]
    [RequiresDynamicCode(ReflectionWarning)]
    public static NbtCompound Serialize<T>(T value, NbtSerializerOptions? options = null) =>
        SerializeToTag(value, options) as NbtCompound ??
        throw new InvalidOperationException($"{typeof(T)} does not serialize to a compound. Use SerializeToTag instead.");

    /// <summary>Serializes a value to whichever tag type represents it, e.g. an <see cref="NbtList"/> for a list.</summary>
    /// <typeparam name="T">The type to serialize as.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="NbtSerializerOptions.Default"/>.</param>
    /// <returns>The tag.</returns>
    /// <exception cref="NbtSerializationException">The value cannot be represented, e.g. a collection contains null.</exception>
    /// <exception cref="NotSupportedException">A type in the object graph is not supported.</exception>
    [RequiresUnreferencedCode(ReflectionWarning)]
    [RequiresDynamicCode(ReflectionWarning)]
    public static NbtTag SerializeToTag<T>(T value, NbtSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var type = typeof(T) == typeof(object) ? value.GetType() : typeof(T);
        return new NbtConverter(options ?? NbtSerializerOptions.Default).ToTag(value, type);
    }

    /// <summary>Deserializes a value from a tag, usually a compound.</summary>
    /// <typeparam name="T">The type to create.</typeparam>
    /// <param name="tag">The tag.</param>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="NbtSerializerOptions.Default"/>.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NbtSerializationException">The tag does not match <typeparamref name="T"/>.</exception>
    /// <exception cref="NotSupportedException">A type in the object graph is not supported.</exception>
    [RequiresUnreferencedCode(ReflectionWarning)]
    [RequiresDynamicCode(ReflectionWarning)]
    public static T Deserialize<T>(NbtTag tag, NbtSerializerOptions? options = null) =>
        (T)Deserialize(tag, typeof(T), options);

    /// <summary>Deserializes a value from a tag, usually a compound.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="type">The type to create.</param>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="NbtSerializerOptions.Default"/>.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NbtSerializationException">The tag does not match <paramref name="type"/>.</exception>
    /// <exception cref="NotSupportedException">A type in the object graph is not supported.</exception>
    [RequiresUnreferencedCode(ReflectionWarning)]
    [RequiresDynamicCode(ReflectionWarning)]
    public static object Deserialize(NbtTag tag, Type type, NbtSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(type);
        return new NbtConverter(options ?? NbtSerializerOptions.Default).FromTag(tag, type)!;
    }
}
