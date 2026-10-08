using System.Diagnostics.CodeAnalysis;

namespace Ubiety.Nbt;

/// <summary>A tag holding an 8-bit integer. Minecraft treats this as signed, and also uses it for booleans.</summary>
/// <param name="value">The value.</param>
public sealed class NbtByte(byte value) : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtByte"/> class from a boolean (1 or 0).</summary>
    /// <param name="value">The value.</param>
    public NbtByte(bool value)
        : this(value ? (byte)1 : (byte)0)
    {
    }

    /// <summary>Gets or sets the value.</summary>
    public byte Value { get; set; } = value;

    /// <summary>Gets the value as a boolean (any non-zero value is <see langword="true"/>).</summary>
    public bool BoolValue => Value != 0;

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.Byte;

    /// <inheritdoc/>
    public override NbtByte Clone() => new(Value);

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) => other is NbtByte tag && tag.Value == Value;
}

/// <summary>A tag holding a signed 16-bit integer.</summary>
/// <param name="value">The value.</param>
public sealed class NbtShort(short value) : NbtTag
{
    /// <summary>Gets or sets the value.</summary>
    public short Value { get; set; } = value;

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.Short;

    /// <inheritdoc/>
    public override NbtShort Clone() => new(Value);

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) => other is NbtShort tag && tag.Value == Value;
}

/// <summary>A tag holding a signed 32-bit integer.</summary>
/// <param name="value">The value.</param>
public sealed class NbtInt(int value) : NbtTag
{
    /// <summary>Gets or sets the value.</summary>
    public int Value { get; set; } = value;

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.Int;

    /// <inheritdoc/>
    public override NbtInt Clone() => new(Value);

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) => other is NbtInt tag && tag.Value == Value;
}

/// <summary>A tag holding a signed 64-bit integer.</summary>
/// <param name="value">The value.</param>
public sealed class NbtLong(long value) : NbtTag
{
    /// <summary>Gets or sets the value.</summary>
    public long Value { get; set; } = value;

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.Long;

    /// <inheritdoc/>
    public override NbtLong Clone() => new(Value);

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) => other is NbtLong tag && tag.Value == Value;
}

/// <summary>A tag holding a 32-bit floating point number.</summary>
/// <param name="value">The value.</param>
public sealed class NbtFloat(float value) : NbtTag
{
    /// <summary>Gets or sets the value.</summary>
    public float Value { get; set; } = value;

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.Float;

    /// <inheritdoc/>
    public override NbtFloat Clone() => new(Value);

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) => other is NbtFloat tag && tag.Value.Equals(Value);
}

/// <summary>A tag holding a 64-bit floating point number.</summary>
/// <param name="value">The value.</param>
public sealed class NbtDouble(double value) : NbtTag
{
    /// <summary>Gets or sets the value.</summary>
    public double Value { get; set; } = value;

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.Double;

    /// <inheritdoc/>
    public override NbtDouble Clone() => new(Value);

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) => other is NbtDouble tag && tag.Value.Equals(Value);
}

/// <summary>A tag holding a string.</summary>
public sealed class NbtString : NbtTag
{
    private string _value;

    /// <summary>Initializes a new instance of the <see cref="NbtString"/> class.</summary>
    /// <param name="value">The value.</param>
    public NbtString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>Gets or sets the value.</summary>
    public string Value
    {
        get => _value;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _value = value;
        }
    }

    /// <inheritdoc/>
    public override NbtTagType TagType => NbtTagType.String;

    /// <inheritdoc/>
    public override NbtString Clone() => new(Value);

    /// <inheritdoc/>
    public override bool DeepEquals([NotNullWhen(true)] NbtTag? other) =>
        other is NbtString tag && string.Equals(tag.Value, Value, StringComparison.Ordinal);
}
