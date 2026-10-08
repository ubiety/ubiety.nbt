using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Ubiety.Nbt.Serialization.Metadata;

/// <summary>
/// The state of a single serialization or deserialization operation: the options in effect and the current
/// path, which is reported in errors.
/// </summary>
/// <remarks>This type supports generated code and custom <see cref="NbtTypeInfo{T}"/> implementations.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NbtSerializationState
{
    private readonly List<(string? Name, int Index)> _path = [];

    internal NbtSerializationState(NbtSerializerOptions options)
    {
        Options = options;
    }

    /// <summary>Gets the options in effect.</summary>
    public NbtSerializerOptions Options { get; }

    /// <summary>Serializes a member value and stores it in <paramref name="compound"/>.</summary>
    /// <typeparam name="T">The member type.</typeparam>
    /// <param name="compound">The compound being written.</param>
    /// <param name="key">The compound key.</param>
    /// <param name="value">The value. Callers omit null values.</param>
    /// <param name="typeInfo">The metadata for <typeparamref name="T"/>.</param>
    public void WriteMember<T>(NbtCompound compound, string key, T value, NbtTypeInfo<T> typeInfo)
    {
        Push(key);
        compound[key] = typeInfo.Serialize(value, this);
        Pop();
    }

    /// <summary>Deserializes a member value from <paramref name="compound"/>, if its key is present.</summary>
    /// <typeparam name="T">The member type.</typeparam>
    /// <param name="compound">The compound being read.</param>
    /// <param name="key">The compound key.</param>
    /// <param name="typeInfo">The metadata for <typeparamref name="T"/>.</param>
    /// <param name="value">The value, if the key was present.</param>
    /// <returns><see langword="true"/> if the key was present.</returns>
    public bool TryReadMember<T>(NbtCompound compound, string key, NbtTypeInfo<T> typeInfo, [MaybeNullWhen(false)] out T value)
    {
        if (!TryGetTag(compound, key, out var tag))
        {
            value = default;
            return false;
        }

        Push(key);
        value = typeInfo.Deserialize(tag, this);
        Pop();
        return true;
    }

    /// <summary>Creates the exception for a missing required key.</summary>
    /// <param name="key">The compound key.</param>
    /// <returns>The exception to throw.</returns>
    public NbtSerializationException MissingRequired(string key) => Error($"Required key '{key}' is missing.");

    /// <summary>Creates an exception that reports the current path.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one, if any.</param>
    /// <returns>The exception to throw.</returns>
    public NbtSerializationException Error(string message, Exception? innerException = null)
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

        return new NbtSerializationException(message, path.ToString(), innerException);
    }

    internal void Push(string key) => _path.Add((key, 0));

    internal void Push(int index) => _path.Add((null, index));

    internal void Pop() => _path.RemoveAt(_path.Count - 1);

    internal void EnterContainer()
    {
        if (_path.Count >= Options.MaxDepth)
        {
            throw Error($"The maximum depth of {Options.MaxDepth} was exceeded. Does the object graph contain a cycle?");
        }
    }

    internal bool TryGetTag(NbtCompound compound, string key, [NotNullWhen(true)] out NbtTag? tag)
    {
        if (compound.TryGetValue(key, out tag))
        {
            return true;
        }

        if (Options.PropertyNameCaseInsensitive)
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

    internal NbtSerializationException Mismatch(string expected, NbtTag actual) =>
        Error($"Expected {expected} but found {actual.TagType}.");

    internal T Expect<T>(NbtTag tag, Type type)
        where T : NbtTag =>
        tag as T ?? throw Mismatch($"{typeof(T).Name[3..]} for {type.Name}", tag);
}
