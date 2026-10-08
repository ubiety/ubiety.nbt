using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Ubiety.Nbt;

/// <summary>
/// A tag holding a collection of named tags. Insertion order is preserved.
/// </summary>
public sealed class NbtCompound : NbtTag, IDictionary<string, NbtTag>, IReadOnlyDictionary<string, NbtTag>
{
    private readonly OrderedDictionary<string, NbtTag> _tags = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.Compound;

    /// <inheritdoc cref="ICollection{T}.Count"/>
    public int Count => _tags.Count;

    /// <inheritdoc/>
    public ICollection<string> Keys => _tags.Keys;

    /// <inheritdoc/>
    public ICollection<NbtTag> Values => _tags.Values;

    /// <inheritdoc/>
    IEnumerable<string> IReadOnlyDictionary<string, NbtTag>.Keys => _tags.Keys;

    /// <inheritdoc/>
    IEnumerable<NbtTag> IReadOnlyDictionary<string, NbtTag>.Values => _tags.Values;

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<string, NbtTag>>.IsReadOnly => false;

    /// <summary>Gets or sets the tag with the given name. Setting replaces any existing tag with that name.</summary>
    /// <param name="name">The tag name.</param>
    /// <exception cref="KeyNotFoundException">Getting a name that is not present.</exception>
    public NbtTag this[string name]
    {
        get => _tags[name];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _tags[name] = value;
        }
    }

    /// <summary>Adds a tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="tag">The tag.</param>
    /// <exception cref="ArgumentException">A tag with the same name already exists.</exception>
    public void Add(string name, NbtTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        _tags.Add(name, tag);
    }

    /// <inheritdoc/>
    public bool ContainsKey(string name) => _tags.ContainsKey(name);

    /// <inheritdoc/>
    public bool Remove(string name) => _tags.Remove(name);

    /// <inheritdoc/>
    public bool TryGetValue(string name, [MaybeNullWhen(false)] out NbtTag value) => _tags.TryGetValue(name, out value);

    /// <inheritdoc/>
    public void Clear() => _tags.Clear();

    /// <summary>Gets the tag with the given name as a <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The expected tag type.</typeparam>
    /// <param name="name">The tag name.</param>
    /// <returns>The tag.</returns>
    /// <exception cref="KeyNotFoundException">No tag has that name.</exception>
    /// <exception cref="InvalidCastException">The tag is not a <typeparamref name="T"/>.</exception>
    public T Get<T>(string name)
        where T : NbtTag
    {
        var tag = _tags[name];
        return tag as T ?? throw new InvalidCastException($"Tag '{name}' is {tag.TagType}, not {typeof(T).Name}.");
    }

    /// <summary>Gets the tag with the given name if it exists and is a <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The expected tag type.</typeparam>
    /// <param name="name">The tag name.</param>
    /// <param name="tag">The tag, if found.</param>
    /// <returns><see langword="true"/> if a tag of the right type was found.</returns>
    public bool TryGet<T>(string name, [NotNullWhen(true)] out T? tag)
        where T : NbtTag
    {
        tag = _tags.TryGetValue(name, out var value) ? value as T : null;
        return tag is not null;
    }

    /// <summary>Gets the value of a byte tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public byte GetByte(string name) => Get<NbtByte>(name).Value;

    /// <summary>Gets the value of a byte tag as a boolean.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public bool GetBool(string name) => Get<NbtByte>(name).BoolValue;

    /// <summary>Gets the value of a short tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public short GetShort(string name) => Get<NbtShort>(name).Value;

    /// <summary>Gets the value of an int tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public int GetInt(string name) => Get<NbtInt>(name).Value;

    /// <summary>Gets the value of a long tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public long GetLong(string name) => Get<NbtLong>(name).Value;

    /// <summary>Gets the value of a float tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public float GetFloat(string name) => Get<NbtFloat>(name).Value;

    /// <summary>Gets the value of a double tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public double GetDouble(string name) => Get<NbtDouble>(name).Value;

    /// <summary>Gets the value of a string tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public string GetString(string name) => Get<NbtString>(name).Value;

    /// <summary>Gets the value of a byte array tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public byte[] GetByteArray(string name) => Get<NbtByteArray>(name).Value;

    /// <summary>Gets the value of an int array tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public int[] GetIntArray(string name) => Get<NbtIntArray>(name).Value;

    /// <summary>Gets the value of a long array tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The value.</returns>
    public long[] GetLongArray(string name) => Get<NbtLongArray>(name).Value;

    /// <summary>Gets a list tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The tag.</returns>
    public NbtList GetList(string name) => Get<NbtList>(name);

    /// <summary>Gets a compound tag.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The tag.</returns>
    public NbtCompound GetCompound(string name) => Get<NbtCompound>(name);

    /// <summary>Returns an enumerator over the entries in insertion order.</summary>
    /// <returns>The enumerator.</returns>
    public OrderedDictionary<string, NbtTag>.Enumerator GetEnumerator() => _tags.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<string, NbtTag>> IEnumerable<KeyValuePair<string, NbtTag>>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    void ICollection<KeyValuePair<string, NbtTag>>.Add(KeyValuePair<string, NbtTag> item) => Add(item.Key, item.Value);

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<string, NbtTag>>.Contains(KeyValuePair<string, NbtTag> item) =>
        ((ICollection<KeyValuePair<string, NbtTag>>)_tags).Contains(item);

    /// <inheritdoc/>
    void ICollection<KeyValuePair<string, NbtTag>>.CopyTo(KeyValuePair<string, NbtTag>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<string, NbtTag>>)_tags).CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<string, NbtTag>>.Remove(KeyValuePair<string, NbtTag> item) =>
        ((ICollection<KeyValuePair<string, NbtTag>>)_tags).Remove(item);

    /// <inheritdoc/>
    public override NbtCompound Clone()
    {
        var clone = new NbtCompound();
        foreach (var (name, tag) in _tags)
        {
            clone._tags.Add(name, tag.Clone());
        }

        return clone;
    }

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other)
    {
        if (other is not NbtCompound compound || compound.Count != Count)
        {
            return false;
        }

        foreach (var (name, tag) in _tags)
        {
            if (!compound._tags.TryGetValue(name, out var otherTag) || !tag.DeepEquals(otherTag))
            {
                return false;
            }
        }

        return true;
    }
}
