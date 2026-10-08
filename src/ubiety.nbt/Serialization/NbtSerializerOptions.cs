namespace Ubiety.Nbt.Serialization;

/// <summary>
/// Options for <see cref="NbtSerializer"/>.
/// </summary>
public sealed class NbtSerializerOptions
{
    /// <summary>Gets the default options.</summary>
    public static NbtSerializerOptions Default { get; } = new();

    /// <summary>
    /// Gets a function that converts member names to compound keys, such as <see cref="NbtNamingPolicy.SnakeCase"/>.
    /// <see langword="null"/> uses member names unchanged. <see cref="NbtPropertyAttribute.Name"/> takes precedence.
    /// </summary>
    public Func<string, string>? PropertyNamingPolicy { get; init; }

    /// <summary>Gets a value indicating whether compound keys are matched to members ignoring case when deserializing.</summary>
    public bool PropertyNameCaseInsensitive { get; init; }

    /// <summary>
    /// Gets a value indicating whether enums are written as their names instead of their numeric values.
    /// Either form is accepted when deserializing.
    /// </summary>
    public bool EnumsAsStrings { get; init; }

    /// <summary>Gets the maximum nesting depth. Deeper (or cyclic) object graphs are rejected.</summary>
    public int MaxDepth { get; init; } = 512;
}
