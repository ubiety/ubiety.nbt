using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ubiety.Nbt.Generators;

/// <summary>
/// Turns a serializer context's symbols into an equatable <see cref="ContextModel"/>. The rules mirror the
/// reflection-based serializer (<c>ReflectionTypeInfo</c>) so both produce the same NBT.
/// </summary>
internal sealed class Parser
{
    private static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat;

    private static readonly string[] ReservedNames =
    [
        "Default", "Options", "GetTypeInfo", "Equals", "GetHashCode", "GetType", "ToString", "MemberwiseClone",
        "ReferenceEquals", "Finalize",
    ];

    private readonly Compilation _compilation;
    private readonly INamedTypeSymbol _context;
    private readonly KnownSymbols _known;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<Entry> _ordered = [];
    private readonly Queue<(Entry Entry, Location? Location, string Path)> _pendingObjects = new();
    private readonly HashSet<string> _usedNames = new(StringComparer.Ordinal);
    private readonly List<DiagnosticInfo> _diagnostics = [];

    private Parser(Compilation compilation, INamedTypeSymbol context, KnownSymbols known, CancellationToken cancellationToken)
    {
        _compilation = compilation;
        _context = context;
        _known = known;
        _cancellationToken = cancellationToken;
        _usedNames.UnionWith(ReservedNames);
        _usedNames.Add(context.Name);
    }

    public static ContextModel? Parse(GeneratorAttributeSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        if (syntaxContext.TargetSymbol is not INamedTypeSymbol context ||
            KnownSymbols.Create(syntaxContext.SemanticModel.Compilation) is not { } known)
        {
            return null;
        }

        var parser = new Parser(syntaxContext.SemanticModel.Compilation, context, known, cancellationToken);
        return parser.Parse();
    }

    private ContextModel Parse()
    {
        var ns = _context.ContainingNamespace.IsGlobalNamespace ? null : _context.ContainingNamespace.ToDisplayString();
        if (!IsValidContext())
        {
            Report(DiagnosticDescriptors.InvalidContext, _context.Locations.FirstOrDefault(), _context.Name);
            return Build(ns);
        }

        foreach (var attribute in _context.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _known.SerializableAttribute) ||
                attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value is not ITypeSymbol type)
            {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(_cancellationToken).GetLocation();
            Resolve(type, location, type.Name);
        }

        while (_pendingObjects.Count > 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var (entry, location, path) = _pendingObjects.Dequeue();
            entry.Object = AnalyzeObject((INamedTypeSymbol)entry.Symbol, location, path);
        }

        return Build(ns);
    }

    private ContextModel Build(string? ns) => new(
        ns,
        _context.Name,
        new EquatableArray<TypeModel>(_ordered.Select(e => e.ToModel()).ToArray()),
        new EquatableArray<DiagnosticInfo>(_diagnostics.ToArray()));

    private bool IsValidContext()
    {
        if (_context.ContainingType is not null || _context.IsGenericType || _context.IsAbstract || _context.IsStatic)
        {
            return false;
        }

        foreach (var reference in _context.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(_cancellationToken) is not ClassDeclarationSyntax declaration ||
                !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }
        }

        for (var type = _context.BaseType; type is not null; type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(type, _known.SerializerContext))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Registers a type and the types it depends on, returning its context property name.</summary>
    private string? Resolve(ITypeSymbol type, Location? location, string path)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var fullName = type.ToDisplayString(FullyQualified);
        if (_entries.TryGetValue(fullName, out var existing))
        {
            return existing.Failed ? null : existing.PropertyName;
        }

        var entry = new Entry(type, fullName, UniqueName(Mangle(type)), IsPublic(type));
        _entries.Add(fullName, entry);

        if (!Classify(entry, location, path))
        {
            entry.Failed = true;
            _usedNames.Remove(entry.PropertyName);
            return null;
        }

        _ordered.Add(entry);
        return entry.PropertyName;
    }

    private bool Classify(Entry entry, Location? location, string path)
    {
        var type = entry.Symbol;

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            return SetElement(entry, TypeModelKind.Nullable, nullable.TypeArguments[0], location, path);
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            entry.Kind = TypeModelKind.Enum;
            return CheckAccessible(type, location, path);
        }

        var primitive = type.SpecialType switch
        {
            SpecialType.System_Boolean => "Boolean",
            SpecialType.System_Byte => "Byte",
            SpecialType.System_SByte => "SByte",
            SpecialType.System_Int16 => "Int16",
            SpecialType.System_UInt16 => "UInt16",
            SpecialType.System_Int32 => "Int32",
            SpecialType.System_UInt32 => "UInt32",
            SpecialType.System_Int64 => "Int64",
            SpecialType.System_UInt64 => "UInt64",
            SpecialType.System_Single => "Single",
            SpecialType.System_Double => "Double",
            SpecialType.System_String => "String",
            _ => SymbolEqualityComparer.Default.Equals(type, _known.Guid) ? "Guid" : null,
        };

        if (type is IArrayTypeSymbol { IsSZArray: true } array)
        {
            primitive = array.ElementType.SpecialType switch
            {
                SpecialType.System_Byte => "ByteArray",
                SpecialType.System_Int32 => "IntArray",
                SpecialType.System_Int64 => "LongArray",
                _ => null,
            };

            if (primitive is null)
            {
                return SetElement(entry, TypeModelKind.Array, array.ElementType, location, path + "[]");
            }
        }

        if (primitive is not null)
        {
            entry.Kind = TypeModelKind.Primitive;
            entry.Primitive = primitive;
            return true;
        }

        if (InheritsFrom(type, _known.Tag))
        {
            entry.Kind = TypeModelKind.Tag;
            return true;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } generic)
        {
            var definition = generic.OriginalDefinition;
            if (_known.ListTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, definition)))
            {
                return SetElement(entry, TypeModelKind.List, generic.TypeArguments[0], location, path + "[]");
            }

            if (_known.DictionaryTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, definition)) &&
                generic.TypeArguments[0].SpecialType == SpecialType.System_String)
            {
                return SetElement(entry, TypeModelKind.Dictionary, generic.TypeArguments[1], location, path + "[]");
            }
        }

        if (UnsupportedReason(type) is { } reason)
        {
            Report(DiagnosticDescriptors.UnsupportedType, location, entry.FullName, path, reason);
            return false;
        }

        if (!CheckAccessible(type, location, path))
        {
            return false;
        }

        entry.Kind = TypeModelKind.Object;
        _pendingObjects.Enqueue((entry, location, path));
        return true;
    }

    private bool SetElement(Entry entry, TypeModelKind kind, ITypeSymbol element, Location? location, string path)
    {
        entry.Kind = kind;
        entry.ElementFullName = element.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(FullyQualified);
        entry.ElementProperty = Resolve(element, location, path);
        return entry.ElementProperty is not null;
    }

    private string? UnsupportedReason(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Object or SpecialType.System_Char or SpecialType.System_Decimal or
            SpecialType.System_DateTime or SpecialType.System_IntPtr or SpecialType.System_UIntPtr ||
            _known.UnsupportedValueTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, type)))
        {
            return "it has no NBT representation";
        }

        if (type.TypeKind is TypeKind.Interface or TypeKind.Delegate or TypeKind.Pointer or TypeKind.FunctionPointer or
            TypeKind.TypeParameter or TypeKind.Dynamic or TypeKind.Error || type is IArrayTypeSymbol)
        {
            return "use a concrete class or struct, a single-dimensional array, List<T>, or Dictionary<string, T>";
        }

        if (type.IsAbstract)
        {
            return "abstract types cannot be created; polymorphism is not supported";
        }

        if (type.AllInterfaces.Any(i => i.SpecialType == SpecialType.System_Collections_IEnumerable))
        {
            return "only arrays, List<T>, Dictionary<string, T> and their interfaces are supported as collections";
        }

        return null;
    }

    private ObjectModel? AnalyzeObject(INamedTypeSymbol type, Location? location, string path)
    {
        var members = CollectMembers(type, location, path);
        if (members is null)
        {
            return null;
        }

        var constructor = SelectConstructor(type, out var failure);
        if (failure is not null)
        {
            Report(DiagnosticDescriptors.NoConstructor, location, type.ToDisplayString(), failure);
            return null;
        }

        ConstructorModel? constructorModel = null;
        if (constructor is not null)
        {
            var parameters = new List<ParameterModel>();
            foreach (var parameter in constructor.Parameters)
            {
                var index = members.FindIndex(m => string.Equals(m.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    Report(
                        DiagnosticDescriptors.NoConstructor,
                        parameter.Locations.FirstOrDefault() ?? location,
                        type.ToDisplayString(),
                        $"constructor parameter '{parameter.Name}' does not match any serialized property or field");
                    return null;
                }

                var infoType = Unwrap(parameter.Type);
                var infoProperty = Resolve(infoType, parameter.Locations.FirstOrDefault() ?? location, path + "." + parameter.Name);
                if (infoProperty is null)
                {
                    return null;
                }

                var typeName = parameter.Type.ToDisplayString(FullyQualified);
                parameters.Add(new ParameterModel(index, typeName, infoType.ToDisplayString(FullyQualified), infoProperty, DefaultLiteral(parameter, typeName)));
            }

            var accessible = _compilation.IsSymbolAccessibleWithin(constructor, _context);
            if (!accessible && type.IsGenericType)
            {
                Report(DiagnosticDescriptors.InaccessibleGenericMember, location, constructor.ToDisplayString(), type.ToDisplayString());
                return null;
            }

            var setsRequired = constructor.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _known.SetsRequiredMembersAttribute));
            constructorModel = new ConstructorModel(accessible, setsRequired, new EquatableArray<ParameterModel>(parameters.ToArray()));
        }

        // Members set through an object initializer need a constructor the generated code can call directly.
        if (constructorModel is { IsAccessible: false })
        {
            members = members.Select(m => m.Set == AccessMode.Initializer ? m with { Set = AccessMode.Accessor } : m).ToList();
        }

        return new ObjectModel(type.IsValueType, new EquatableArray<MemberModel>(members.ToArray()), constructorModel);
    }

    private List<MemberModel>? CollectMembers(INamedTypeSymbol type, Location? location, string path)
    {
        // Collect derived-first so overriding and hiding members win, then emit base-first, matching reflection.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var properties = new List<List<IPropertySymbol>>();
        var fields = new List<List<IFieldSymbol>>();

        for (var current = type; current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); current = current.BaseType)
        {
            var isBase = !SymbolEqualityComparer.Default.Equals(current, type);
            var declared = new List<IPropertySymbol>();
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || !property.ExplicitInterfaceImplementations.IsEmpty ||
                    (isBase && property.DeclaredAccessibility == Accessibility.Private) ||
                    HasAttribute(property, _known.IgnoreAttribute) || !seen.Add(property.Name))
                {
                    continue;
                }

                declared.Add(property);
            }

            properties.Add(declared);
        }

        for (var current = type; current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); current = current.BaseType)
        {
            var isBase = !SymbolEqualityComparer.Default.Equals(current, type);
            var declared = new List<IFieldSymbol>();
            foreach (var field in current.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic || field.IsConst || field.IsImplicitlyDeclared ||
                    (isBase && field.DeclaredAccessibility == Accessibility.Private) ||
                    (field.DeclaredAccessibility != Accessibility.Public && GetExplicitName(field, out _) is false) ||
                    HasAttribute(field, _known.IgnoreAttribute) || !seen.Add(field.Name))
                {
                    continue;
                }

                declared.Add(field);
            }

            fields.Add(declared);
        }

        var members = new List<MemberModel>();
        var ok = true;
        foreach (var property in Enumerable.Reverse(properties).SelectMany(p => p))
        {
            var hasAttribute = GetExplicitName(property, out var explicitName);
            if (property.GetMethod is not { } getter || (getter.DeclaredAccessibility != Accessibility.Public && !hasAttribute))
            {
                continue;
            }

            var setter = property.SetMethod;
            var set = AccessMode.None;
            if (setter is not null && (setter.DeclaredAccessibility == Accessibility.Public || hasAttribute))
            {
                var accessible = _compilation.IsSymbolAccessibleWithin(setter, _context);
                set = property.IsRequired && accessible ? AccessMode.Initializer
                    : setter.IsInitOnly || !accessible ? AccessMode.Accessor
                    : AccessMode.Direct;
            }

            var get = _compilation.IsSymbolAccessibleWithin(getter, _context) ? AccessMode.Direct : AccessMode.Accessor;
            ok &= AddMember(members, property, property.Type, explicitName, isField: false, property.IsRequired, get, set, getter.Name, setter?.Name, location, path);
        }

        foreach (var field in Enumerable.Reverse(fields).SelectMany(f => f))
        {
            GetExplicitName(field, out var explicitName);
            var accessible = _compilation.IsSymbolAccessibleWithin(field, _context);
            var get = accessible ? AccessMode.Direct : AccessMode.Accessor;
            var set = field.IsReadOnly ? AccessMode.None
                : field.IsRequired && accessible ? AccessMode.Initializer
                : get;
            ok &= AddMember(members, field, field.Type, explicitName, isField: true, field.IsRequired, get, set, null, null, location, path);
        }

        return ok ? members : null;
    }

    private bool AddMember(
        List<MemberModel> members,
        ISymbol member,
        ITypeSymbol memberType,
        string? explicitName,
        bool isField,
        bool isRequired,
        AccessMode get,
        AccessMode set,
        string? getterName,
        string? setterName,
        Location? location,
        string path)
    {
        var memberLocation = member.Locations.FirstOrDefault(l => l.IsInSource) ?? location;
        var declaringType = member.ContainingType;
        if ((get == AccessMode.Accessor || set == AccessMode.Accessor) && declaringType.IsGenericType)
        {
            Report(DiagnosticDescriptors.InaccessibleGenericMember, memberLocation, member.Name, declaringType.ToDisplayString());
            return false;
        }

        var infoType = Unwrap(memberType);
        var infoProperty = Resolve(infoType, memberLocation, path + "." + member.Name);
        if (infoProperty is null)
        {
            return false;
        }

        members.Add(new MemberModel(
            member.Name,
            explicitName,
            isField,
            memberType.ToDisplayString(FullyQualified),
            declaringType.ToDisplayString(FullyQualified),
            declaringType.IsValueType,
            infoType.ToDisplayString(FullyQualified),
            infoProperty,
            IsNullableValueType(memberType),
            memberType.IsReferenceType,
            isRequired,
            get,
            set,
            getterName,
            setterName));
        return true;
    }

    private IMethodSymbol? SelectConstructor(INamedTypeSymbol type, out string? failure)
    {
        failure = null;

        // Reflection does not see a struct's implicit parameterless constructor.
        var constructors = type.InstanceConstructors
            .Where(c => !(type.IsValueType && c.IsImplicitlyDeclared && c.Parameters.Length == 0))
            .ToList();

        var marked = constructors.Where(c => HasAttribute(c, _known.ConstructorAttribute)).ToList();
        if (marked.Count > 1)
        {
            failure = "more than one constructor is marked [NbtConstructor]";
            return null;
        }

        if (marked.Count == 1)
        {
            return marked[0];
        }

        var publicConstructors = constructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();
        if (publicConstructors.FirstOrDefault(c => c.Parameters.Length == 0) is { } parameterless)
        {
            return parameterless;
        }

        if (publicConstructors.Count == 1)
        {
            return publicConstructors[0];
        }

        if (!type.IsValueType)
        {
            failure = "it needs a public parameterless constructor, a single public constructor, or one marked [NbtConstructor]";
        }

        return null;
    }

    private bool CheckAccessible(ITypeSymbol type, Location? location, string path)
    {
        if (_compilation.IsSymbolAccessibleWithin(type, _context))
        {
            return true;
        }

        Report(DiagnosticDescriptors.InaccessibleType, location, type.ToDisplayString(), path, _context.Name);
        return false;
    }

    private bool GetExplicitName(ISymbol member, out string? name)
    {
        foreach (var attribute in member.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _known.PropertyAttribute))
            {
                name = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
                return true;
            }
        }

        name = null;
        return false;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attribute) =>
        symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

    private static bool InheritsFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol Unwrap(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

    private static bool IsNullableValueType(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    private static bool IsPublic(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsPublic(array.ElementType),
        INamedTypeSymbol named => IsPublicChain(named) && named.TypeArguments.All(IsPublic),
        _ => true,
    };

    private static bool IsPublicChain(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    private static string Mangle(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => Mangle(array.ElementType) + "Array",
        INamedTypeSymbol { IsGenericType: true } generic =>
            (generic.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ? "Nullable" : generic.Name) +
            string.Concat(generic.TypeArguments.Select(Mangle)),
        _ => type.Name,
    };

    private string UniqueName(string name)
    {
        var candidate = name;
        for (var i = 2; !_usedNames.Add(candidate); i++)
        {
            candidate = name + i.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private static string? DefaultLiteral(IParameterSymbol parameter, string typeName)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return null;
        }

        var value = parameter.ExplicitDefaultValue;
        var underlying = Unwrap(parameter.Type);
        if (value is null)
        {
            return $"default({typeName})";
        }

        if (underlying.TypeKind == TypeKind.Enum)
        {
            return $"({typeName})({Convert.ToString(value, CultureInfo.InvariantCulture)})";
        }

        return value switch
        {
            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),
            bool b => b ? "true" : "false",
            float f when float.IsNaN(f) => "float.NaN",
            float f when float.IsPositiveInfinity(f) => "float.PositiveInfinity",
            float f when float.IsNegativeInfinity(f) => "float.NegativeInfinity",
            float f => f.ToString("R", CultureInfo.InvariantCulture) + "F",
            double d when double.IsNaN(d) => "double.NaN",
            double d when double.IsPositiveInfinity(d) => "double.PositiveInfinity",
            double d when double.IsNegativeInfinity(d) => "double.NegativeInfinity",
            double d => d.ToString("R", CultureInfo.InvariantCulture) + "D",
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "M",
            _ => $"({typeName})({Convert.ToString(value, CultureInfo.InvariantCulture)})",
        };
    }

    private void Report(DiagnosticDescriptor descriptor, Location? location, params string[] arguments) =>
        _diagnostics.Add(new DiagnosticInfo(descriptor, LocationInfo.From(location), new EquatableArray<string>(arguments)));

    private sealed class Entry(ITypeSymbol symbol, string fullName, string propertyName, bool isPublic)
    {
        public ITypeSymbol Symbol { get; } = symbol;

        public string FullName { get; } = fullName;

        public string PropertyName { get; } = propertyName;

        public bool IsPublic { get; } = isPublic;

        public bool Failed { get; set; }

        public TypeModelKind Kind { get; set; }

        public string? Primitive { get; set; }

        public string? ElementFullName { get; set; }

        public string? ElementProperty { get; set; }

        public ObjectModel? Object { get; set; }

        public TypeModel ToModel() => new(FullName, PropertyName, Kind, IsPublic, Primitive, ElementFullName, ElementProperty, Object);
    }

    private sealed record KnownSymbols(
        INamedTypeSymbol SerializerContext,
        INamedTypeSymbol SerializableAttribute,
        INamedTypeSymbol PropertyAttribute,
        INamedTypeSymbol IgnoreAttribute,
        INamedTypeSymbol ConstructorAttribute,
        INamedTypeSymbol Tag,
        INamedTypeSymbol? Guid,
        INamedTypeSymbol? SetsRequiredMembersAttribute,
        INamedTypeSymbol[] ListTypes,
        INamedTypeSymbol[] DictionaryTypes,
        INamedTypeSymbol[] UnsupportedValueTypes)
    {
        public static KnownSymbols? Create(Compilation compilation)
        {
            INamedTypeSymbol? Get(string name) => compilation.GetTypeByMetadataName(name);

            INamedTypeSymbol[] GetAll(params string[] names) => names.Select(Get).Where(t => t is not null).ToArray()!;

            if (Get("Ubiety.Nbt.Serialization.NbtSerializerContext") is not { } context ||
                Get("Ubiety.Nbt.Serialization.NbtSerializableAttribute") is not { } serializable ||
                Get("Ubiety.Nbt.Serialization.NbtPropertyAttribute") is not { } property ||
                Get("Ubiety.Nbt.Serialization.NbtIgnoreAttribute") is not { } ignore ||
                Get("Ubiety.Nbt.Serialization.NbtConstructorAttribute") is not { } constructor ||
                Get("Ubiety.Nbt.NbtTag") is not { } tag)
            {
                return null;
            }

            return new KnownSymbols(
                context,
                serializable,
                property,
                ignore,
                constructor,
                tag,
                Get("System.Guid"),
                Get("System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"),
                GetAll(
                    "System.Collections.Generic.List`1",
                    "System.Collections.Generic.IEnumerable`1",
                    "System.Collections.Generic.ICollection`1",
                    "System.Collections.Generic.IList`1",
                    "System.Collections.Generic.IReadOnlyCollection`1",
                    "System.Collections.Generic.IReadOnlyList`1"),
                GetAll(
                    "System.Collections.Generic.Dictionary`2",
                    "System.Collections.Generic.IDictionary`2",
                    "System.Collections.Generic.IReadOnlyDictionary`2"),
                GetAll("System.TimeSpan", "System.DateTimeOffset"));
        }
    }
}
