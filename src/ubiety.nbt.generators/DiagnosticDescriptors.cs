using Microsoft.CodeAnalysis;

namespace Ubiety.Nbt.Generators;

internal static class DiagnosticDescriptors
{
    private const string Category = "Ubiety.Nbt.SourceGeneration";

    public static readonly DiagnosticDescriptor InvalidContext = new(
        "NBTGEN001",
        "Invalid NBT serializer context",
        "NBT serializer context '{0}' must be a non-generic, non-nested, non-abstract partial class deriving from NbtSerializerContext",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedType = new(
        "NBTGEN002",
        "Type not supported by NBT serialization",
        "Type '{0}' (at {1}) is not supported by NBT serialization: {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InaccessibleType = new(
        "NBTGEN003",
        "Type not accessible",
        "Type '{0}' (at {1}) is not accessible from NBT serializer context '{2}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoConstructor = new(
        "NBTGEN004",
        "No usable constructor",
        "Cannot deserialize '{0}': {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InaccessibleGenericMember = new(
        "NBTGEN005",
        "Non-public member of generic type",
        "Member '{0}' of generic type '{1}' is not accessible from the serializer context; non-public members of generic types are not supported",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
