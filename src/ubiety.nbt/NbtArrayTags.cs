using System.Diagnostics.CodeAnalysis;

namespace Ubiety.Nbt;

/// <summary>A tag holding an array of bytes.</summary>
public sealed class NbtByteArray : NbtTag
{
    private byte[] _value;

    /// <summary>Initializes a new instance of the <see cref="NbtByteArray"/> class.</summary>
    /// <param name="value">The array. It is stored by reference, not copied.</param>
    public NbtByteArray(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>Gets or sets the array.</summary>
    public byte[] Value
    {
        get => _value;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _value = value;
        }
    }

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.ByteArray;

    /// <inheritdoc/>
    public override NbtByteArray Clone() => new((byte[])Value.Clone());

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) =>
        other is NbtByteArray tag && tag.Value.AsSpan().SequenceEqual(Value);
}

/// <summary>A tag holding an array of 32-bit integers.</summary>
public sealed class NbtIntArray : NbtTag
{
    private int[] _value;

    /// <summary>Initializes a new instance of the <see cref="NbtIntArray"/> class.</summary>
    /// <param name="value">The array. It is stored by reference, not copied.</param>
    public NbtIntArray(int[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>Gets or sets the array.</summary>
    public int[] Value
    {
        get => _value;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _value = value;
        }
    }

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.IntArray;

    /// <inheritdoc/>
    public override NbtIntArray Clone() => new((int[])Value.Clone());

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) =>
        other is NbtIntArray tag && tag.Value.AsSpan().SequenceEqual(Value);
}

/// <summary>A tag holding an array of 64-bit integers.</summary>
public sealed class NbtLongArray : NbtTag
{
    private long[] _value;

    /// <summary>Initializes a new instance of the <see cref="NbtLongArray"/> class.</summary>
    /// <param name="value">The array. It is stored by reference, not copied.</param>
    public NbtLongArray(long[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>Gets or sets the array.</summary>
    public long[] Value
    {
        get => _value;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _value = value;
        }
    }

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.LongArray;

    /// <inheritdoc/>
    public override NbtLongArray Clone() => new((long[])Value.Clone());

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) =>
        other is NbtLongArray tag && tag.Value.AsSpan().SequenceEqual(Value);
}
