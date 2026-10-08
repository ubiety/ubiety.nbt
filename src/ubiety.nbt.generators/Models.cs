using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ubiety.Nbt.Generators;

// The pipeline caches these models between edits, so they must be equatable and hold no Roslyn symbols.

internal sealed record ContextModel(
    string? Namespace,
    string ClassName,
    EquatableArray<TypeModel> Types,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    public string FullName => Namespace is null ? ClassName : Namespace + "." + ClassName;
}

internal enum TypeModelKind
{
    Primitive,
    Tag,
    Enum,
    Nullable,
    Array,
    List,
    Dictionary,
    Object,
}

internal sealed record TypeModel(
    string FullName,
    string PropertyName,
    TypeModelKind Kind,
    bool IsPublic,
    string? Primitive,
    string? ElementFullName,
    string? ElementProperty,
    ObjectModel? Object);

internal enum AccessMode
{
    None,
    Direct,
    Accessor,
    Initializer,
}

internal sealed record ObjectModel(
    bool IsValueType,
    EquatableArray<MemberModel> Members,
    ConstructorModel? Constructor);

internal sealed record MemberModel(
    string Name,
    string? ExplicitName,
    bool IsField,
    string TypeFullName,
    string DeclaringTypeFullName,
    bool DeclaringTypeIsValueType,
    string InfoTypeFullName,
    string InfoProperty,
    bool IsNullableValueType,
    bool IsReferenceType,
    bool IsRequired,
    AccessMode Get,
    AccessMode Set,
    string? GetterName,
    string? SetterName);

internal sealed record ConstructorModel(
    bool IsAccessible,
    bool SetsRequiredMembers,
    EquatableArray<ParameterModel> Parameters);

internal sealed record ParameterModel(
    int MemberIndex,
    string TypeFullName,
    string InfoTypeFullName,
    string InfoProperty,
    string? DefaultLiteral);

internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo? Location, EquatableArray<string> Arguments)
{
    public Diagnostic ToDiagnostic() => Diagnostic.Create(
        Descriptor,
        Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
        [.. Arguments]);
}

internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationInfo? From(Location? location) =>
        location is { IsInSource: true } && location.SourceTree is { } tree
            ? new LocationInfo(tree.FilePath, location.SourceSpan, location.GetLineSpan().Span)
            : null;

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}

/// <summary>An immutable array with value equality, for use in cached pipeline models.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(T[] items)
    {
        _items = items;
    }

    public int Length => _items?.Length ?? 0;

    public T this[int index] => _items![index];

    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        for (var i = 0; i < Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(_items![i], other._items![i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in this)
        {
            hash = unchecked((hash * 31) + (item?.GetHashCode() ?? 0));
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
