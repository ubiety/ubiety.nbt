using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Ubiety.Nbt;

/// <summary>
/// A tag holding an ordered list of unnamed tags that all share the same <see cref="ElementType"/>.
/// </summary>
/// <remarks>
/// A list created without an element type adopts the type of the first tag added to it.
/// </remarks>
public sealed class NbtList : NbtTag, IList<NbtTag>, IReadOnlyList<NbtTag>
{
    private readonly List<NbtTag> _items = [];

    /// <summary>Initializes a new, empty instance of the <see cref="NbtList"/> class with no element type.</summary>
    public NbtList()
    {
    }

    /// <summary>Initializes a new, empty instance of the <see cref="NbtList"/> class.</summary>
    /// <param name="elementType">The type of tag the list holds.</param>
    public NbtList(NbtTagType elementType)
    {
        ElementType = elementType;
    }

    /// <summary>Initializes a new instance of the <see cref="NbtList"/> class containing <paramref name="items"/>.</summary>
    /// <param name="items">The tags to add. They must all be of the same type.</param>
    public NbtList(IEnumerable<NbtTag> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            Add(item);
        }
    }

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.List;

    /// <summary>
    /// Gets or sets the type of tag this list holds. <see cref="NbtTagType.End"/> means the list is empty and untyped.
    /// </summary>
    /// <exception cref="InvalidOperationException">The type is changed while the list is not empty.</exception>
    public NbtTagType ElementType
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            if (_items.Count > 0)
            {
                throw new InvalidOperationException("Cannot change the element type of a non-empty list.");
            }

            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown tag type.");
            }

            field = value;
        }
    }

    /// <inheritdoc cref="ICollection{T}.Count"/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    bool ICollection<NbtTag>.IsReadOnly => false;

    /// <inheritdoc cref="IList{T}.this"/>
    public NbtTag this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)_items.Count, nameof(index));
            Validate(value);
            _items[index] = value;
        }
    }

    /// <summary>Gets the tag at <paramref name="index"/> as a <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The expected tag type.</typeparam>
    /// <param name="index">The index.</param>
    /// <returns>The tag.</returns>
    /// <exception cref="InvalidCastException">The tag is not a <typeparamref name="T"/>.</exception>
    public T Get<T>(int index)
        where T : NbtTag
    {
        var tag = _items[index];
        return tag as T ?? throw new InvalidCastException($"List element {index} is {tag.TagType}, not {typeof(T).Name}.");
    }

    /// <inheritdoc/>
    public void Add(NbtTag item)
    {
        Validate(item);
        _items.Add(item);
    }

    /// <inheritdoc/>
    public void Insert(int index, NbtTag item)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)_items.Count, nameof(index));
        Validate(item);
        _items.Insert(index, item);
    }

    /// <inheritdoc/>
    public bool Remove(NbtTag item) => _items.Remove(item);

    /// <inheritdoc/>
    public void RemoveAt(int index) => _items.RemoveAt(index);

    /// <summary>Removes all tags. The element type is kept.</summary>
    public void Clear() => _items.Clear();

    /// <inheritdoc/>
    public bool Contains(NbtTag item) => _items.Contains(item);

    /// <inheritdoc/>
    public int IndexOf(NbtTag item) => _items.IndexOf(item);

    /// <inheritdoc/>
    public void CopyTo(NbtTag[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>Returns an enumerator over the tags in the list.</summary>
    /// <returns>The enumerator.</returns>
    public List<NbtTag>.Enumerator GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<NbtTag> IEnumerable<NbtTag>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public override NbtList Clone()
    {
        var clone = new NbtList(ElementType);
        foreach (var item in _items)
        {
            clone._items.Add(item.Clone());
        }

        return clone;
    }

    /// <inheritdoc/>
    /// <remarks>The element types of two empty lists are not compared.</remarks>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other)
    {
        if (other is not NbtList list || list.Count != Count)
        {
            return false;
        }

        if (Count > 0 && list.ElementType != ElementType)
        {
            return false;
        }

        for (var i = 0; i < _items.Count; i++)
        {
            if (!_items[i].DeepEquals(list._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void Validate(NbtTag item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (ElementType == NbtTagType.End)
        {
            ElementType = item.TagType;
        }
        else if (item.TagType != ElementType)
        {
            throw new ArgumentException($"Cannot add a {item.TagType} tag to a list of {ElementType}.", nameof(item));
        }
    }
}
