using System.ComponentModel;

namespace Ubiety.Nbt.Serialization.Metadata;

/// <summary>
/// Base class for source-generated metadata of classes, structs and records, which map to compounds.
/// </summary>
/// <typeparam name="T">The type described.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class NbtObjectInfo<T> : NbtTypeInfo<T>
{
    /// <summary>Initializes a new instance of the <see cref="NbtObjectInfo{T}"/> class.</summary>
    /// <param name="options">The options, which determine compound keys.</param>
    protected NbtObjectInfo(NbtSerializerOptions? options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public sealed override NbtTag Serialize(T value, NbtSerializationState state)
    {
        state.EnterContainer();
        var compound = new NbtCompound();
        WriteMembers(value, compound, state);
        return compound;
    }

    /// <inheritdoc/>
    public sealed override T Deserialize(NbtTag tag, NbtSerializationState state)
    {
        var compound = state.Expect<NbtCompound>(tag, typeof(T));
        state.EnterContainer();
        return ReadMembers(compound, state);
    }

    /// <summary>Gets the compound key for a member, applying the naming policy unless a name was given explicitly.</summary>
    /// <param name="memberName">The member name.</param>
    /// <param name="explicitName">The name from <see cref="NbtPropertyAttribute"/>, if any.</param>
    /// <returns>The compound key.</returns>
    protected string GetKey(string memberName, string? explicitName) =>
        explicitName ?? Options.PropertyNamingPolicy?.Invoke(memberName) ?? memberName;

    /// <summary>Writes the members of <paramref name="value"/> to <paramref name="compound"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="compound">The compound to fill.</param>
    /// <param name="state">The operation state.</param>
    protected abstract void WriteMembers(T value, NbtCompound compound, NbtSerializationState state);

    /// <summary>Creates a value from the entries of <paramref name="compound"/>.</summary>
    /// <param name="compound">The compound.</param>
    /// <param name="state">The operation state.</param>
    /// <returns>The value.</returns>
    protected abstract T ReadMembers(NbtCompound compound, NbtSerializationState state);
}
