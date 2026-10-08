using System.Diagnostics.CodeAnalysis;
using Ubiety.Nbt.Snbt;

namespace Ubiety.Nbt;

/// <summary>
/// Base type for all NBT tags.
/// </summary>
/// <remarks>
/// Tags do not carry their own names; names belong to the entries of an <see cref="NbtCompound"/>
/// (or to the root of an <see cref="NbtFile"/>).
/// </remarks>
public abstract class NbtTag
{
    private protected NbtTag()
    {
    }

    /// <summary>Gets the type of this tag.</summary>
    public abstract NbtTagType TagType { get; }

    /// <summary>Converts a <see cref="bool"/> to an <see cref="NbtByte"/> (1 or 0).</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(bool value) => new NbtByte(value);

    /// <summary>Converts a <see cref="byte"/> to an <see cref="NbtByte"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(byte value) => new NbtByte(value);

    /// <summary>Converts a <see cref="short"/> to an <see cref="NbtShort"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(short value) => new NbtShort(value);

    /// <summary>Converts an <see cref="int"/> to an <see cref="NbtInt"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(int value) => new NbtInt(value);

    /// <summary>Converts a <see cref="long"/> to an <see cref="NbtLong"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(long value) => new NbtLong(value);

    /// <summary>Converts a <see cref="float"/> to an <see cref="NbtFloat"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(float value) => new NbtFloat(value);

    /// <summary>Converts a <see cref="double"/> to an <see cref="NbtDouble"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(double value) => new NbtDouble(value);

    /// <summary>Converts a <see cref="string"/> to an <see cref="NbtString"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(string value) => new NbtString(value);

    /// <summary>Converts a byte array to an <see cref="NbtByteArray"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(byte[] value) => new NbtByteArray(value);

    /// <summary>Converts an int array to an <see cref="NbtIntArray"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(int[] value) => new NbtIntArray(value);

    /// <summary>Converts a long array to an <see cref="NbtLongArray"/>.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator NbtTag(long[] value) => new NbtLongArray(value);

    /// <summary>
    /// Parses a tag from its SNBT (stringified NBT) representation, e.g. <c>{name:"Steve",Health:20.0f}</c>.
    /// </summary>
    /// <param name="snbt">The SNBT text.</param>
    /// <returns>The parsed tag.</returns>
    /// <exception cref="NbtFormatException">The text is not valid SNBT.</exception>
    public static NbtTag ParseSnbt(string snbt) => SnbtParser.Parse(snbt);

    /// <summary>Creates a deep copy of this tag.</summary>
    /// <returns>The copy.</returns>
    public abstract NbtTag Clone();

    /// <summary>
    /// Determines whether <paramref name="other"/> has the same type and contents as this tag.
    /// Compound entry order is ignored.
    /// </summary>
    /// <param name="other">The tag to compare with.</param>
    /// <returns><see langword="true"/> if the tags are equivalent.</returns>
    public abstract bool DeepEquals([NotNullWhen(true)] NbtTag? other);

    /// <summary>Formats this tag as SNBT.</summary>
    /// <param name="indented">Whether to spread compounds and nested lists over multiple indented lines.</param>
    /// <returns>The SNBT text.</returns>
    public string ToSnbt(bool indented = false) => SnbtWriter.Write(this, indented);

    /// <summary>Formats this tag as compact SNBT.</summary>
    /// <returns>The SNBT text.</returns>
    public override string ToString() => ToSnbt();
}
