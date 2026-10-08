namespace Ubiety.Nbt.Serialization.Metadata;

/// <summary>
/// Describes how a type is converted to and from NBT, without reflection. Obtain instances from a source-generated
/// <see cref="NbtSerializerContext"/> or from <see cref="NbtMetadata"/>.
/// </summary>
public abstract class NbtTypeInfo
{
    private protected NbtTypeInfo(NbtSerializerOptions? options)
    {
        Options = options ?? NbtSerializerOptions.Default;
    }

    /// <summary>Gets the options used when this metadata is the root of an operation.</summary>
    public NbtSerializerOptions Options { get; }

    /// <summary>Gets the type described.</summary>
    public abstract Type Type { get; }

    internal abstract NbtTag SerializeAsObject(object value, NbtSerializationState state);

    internal abstract object? DeserializeAsObject(NbtTag tag, NbtSerializationState state);
}

/// <summary>
/// Describes how <typeparamref name="T"/> is converted to and from NBT, without reflection.
/// </summary>
/// <typeparam name="T">The type described.</typeparam>
public abstract class NbtTypeInfo<T> : NbtTypeInfo
{
    /// <summary>Initializes a new instance of the <see cref="NbtTypeInfo{T}"/> class.</summary>
    /// <param name="options">The options used when this metadata is the root of an operation.</param>
    protected NbtTypeInfo(NbtSerializerOptions? options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public sealed override Type Type => typeof(T);

    /// <summary>Converts a value to a tag.</summary>
    /// <param name="value">The value, which is not null.</param>
    /// <param name="state">The operation state.</param>
    /// <returns>The tag.</returns>
    public abstract NbtTag Serialize(T value, NbtSerializationState state);

    /// <summary>Converts a tag to a value.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="state">The operation state.</param>
    /// <returns>The value.</returns>
    public abstract T Deserialize(NbtTag tag, NbtSerializationState state);

    internal sealed override NbtTag SerializeAsObject(object value, NbtSerializationState state) => Serialize((T)value, state);

    internal sealed override object? DeserializeAsObject(NbtTag tag, NbtSerializationState state) => Deserialize(tag, state);
}
