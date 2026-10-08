namespace Ubiety.Nbt.Serialization;

/// <summary>
/// Sets the compound key used for a property or field, overriding any naming policy. Also opts in members that
/// are not public, or whose setter is not public.
/// </summary>
/// <param name="name">The compound key.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NbtPropertyAttribute(string? name = null) : Attribute
{
    /// <summary>Gets the compound key, or <see langword="null"/> to use the member name and naming policy.</summary>
    public string? Name { get; } = name;
}

/// <summary>Excludes a property or field from serialization.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NbtIgnoreAttribute : Attribute
{
}

/// <summary>
/// Marks the constructor to use when deserializing. Its parameters are matched to properties and fields by name,
/// ignoring case.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class NbtConstructorAttribute : Attribute
{
}
