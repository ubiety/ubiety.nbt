using Ubiety.Nbt.Serialization.Metadata;

namespace Ubiety.Nbt.Serialization;

/// <summary>
/// Base class for source-generated serialization contexts, which provide reflection-free <see cref="NbtTypeInfo{T}"/>
/// metadata that works with trimming and Native AOT.
/// </summary>
/// <remarks>
/// Declare a <see langword="partial"/> class deriving from this one and list the root types with
/// <see cref="NbtSerializableAttribute"/>; types they reference are included automatically. The generator adds a
/// static <c>Default</c> instance, constructors, and a property per type, named after it (e.g. <c>Player</c>,
/// <c>ListItem</c>). Do not declare constructors yourself.
/// <code>
/// [NbtSerializable(typeof(Player))]
/// internal partial class GameContext : NbtSerializerContext;
///
/// var compound = NbtSerializer.Serialize(player, GameContext.Default.Player);
/// </code>
/// </remarks>
public abstract class NbtSerializerContext
{
    /// <summary>Initializes a new instance of the <see cref="NbtSerializerContext"/> class.</summary>
    /// <param name="options">The options, or <see langword="null"/> for <see cref="NbtSerializerOptions.Default"/>.</param>
    protected NbtSerializerContext(NbtSerializerOptions? options)
    {
        Options = options ?? NbtSerializerOptions.Default;
    }

    /// <summary>Gets the options used by this context's metadata.</summary>
    public NbtSerializerOptions Options { get; }

    /// <summary>Gets the metadata for a type, if this context includes it.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The metadata, or <see langword="null"/> if the type is not included.</returns>
    public abstract NbtTypeInfo? GetTypeInfo(Type type);
}

/// <summary>
/// Includes a type in a source-generated <see cref="NbtSerializerContext"/>.
/// </summary>
/// <param name="type">The type.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class NbtSerializableAttribute(Type type) : Attribute
{
    /// <summary>Gets the type to include.</summary>
    public Type Type { get; } = type;
}
