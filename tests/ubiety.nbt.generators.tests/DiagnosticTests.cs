using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ubiety.Nbt.Generators;
using Ubiety.Nbt.Serialization;

namespace Ubiety.Nbt.Generators.Tests;

/// <summary>
/// Runs the generator over small compilations and checks the diagnostics it reports.
/// </summary>
public class DiagnosticTests
{
    private static readonly ImmutableArray<MetadataReference> References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
        MetadataReference.CreateFromFile(typeof(NbtSerializerContext).Assembly.Location),
    ];

    private const string SourcePath = "Source.cs";

    [Fact]
    public void ValidContextGeneratesCompilingCode()
    {
        var (generator, output) = Run("""
            public enum Mode { A, B }
            public record Item(string Id, byte Count);
            public class Player
            {
                public required string Name { get; init; }
                public Mode Mode { get; set; }
                public List<Item> Items { get; set; } = [];
                public Dictionary<string, int?> Stats { get; set; } = [];
                [NbtProperty("secret")] private int _secret;
            }

            [NbtSerializable(typeof(Player))]
            internal partial class Context : NbtSerializerContext;
            """);

        Assert.Empty(generator);
        Assert.Empty(output.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData("internal class Context : NbtSerializerContext { public Context() : base(null) { } public override Ubiety.Nbt.Serialization.Metadata.NbtTypeInfo? GetTypeInfo(Type type) => null; }")]
    [InlineData("internal partial class Context;")]
    [InlineData("internal abstract partial class Context : NbtSerializerContext;")]
    [InlineData("internal partial class Context<T> : NbtSerializerContext;")]
    [InlineData("internal static partial class Outer { [NbtSerializable(typeof(int))] internal partial class Context : NbtSerializerContext; }")]
    public void ReportsInvalidContext(string declaration)
    {
        var source = declaration.Contains("Outer", StringComparison.Ordinal)
            ? declaration
            : "[NbtSerializable(typeof(int))]\n" + declaration;

        AssertSingle("NBTGEN001", Run(source).Generator);
    }

    [Theory]
    [InlineData("decimal", "no NBT representation")]
    [InlineData("object", "no NBT representation")]
    [InlineData("IComparable", "use a concrete class")]
    [InlineData("int[,]", "use a concrete class")]
    [InlineData("Shape", "abstract types")]
    [InlineData("Queue<int>", "only arrays")]
    public void ReportsUnsupportedMemberType(string type, string reason)
    {
        var diagnostic = AssertSingle("NBTGEN002", Run($$"""
            public abstract class Shape;
            public class Holder { public {{type}} Value { get; set; } = default!; }

            [NbtSerializable(typeof(Holder))]
            internal partial class Context : NbtSerializerContext;
            """).Generator);

        Assert.Contains("Holder.Value", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(reason, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsUnsupportedRootType()
    {
        AssertSingle("NBTGEN002", Run("""
            [NbtSerializable(typeof(DateTime))]
            internal partial class Context : NbtSerializerContext;
            """).Generator);
    }

    [Fact]
    public void ReportsInaccessibleType()
    {
        var diagnostic = AssertSingle("NBTGEN003", Run("""
            public class Holder
            {
                [NbtProperty] private Secret Value { get; set; } = new();

                private class Secret { public int X { get; set; } }
            }

            [NbtSerializable(typeof(Holder))]
            internal partial class Context : NbtSerializerContext;
            """).Generator);

        Assert.Contains("Holder.Secret", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public Thing(int a) { } public Thing(string b) { }", "public parameterless constructor")]
    [InlineData("[NbtConstructor] public Thing(int a) { } [NbtConstructor] public Thing(string b) { }", "more than one constructor")]
    [InlineData("public Thing(int missing) { }", "'missing' does not match")]
    public void ReportsNoConstructor(string constructors, string reason)
    {
        var diagnostic = AssertSingle("NBTGEN004", Run($$"""
            public class Thing
            {
                {{constructors}}
                public int A { get; set; }
                public string B { get; set; } = "";
            }

            [NbtSerializable(typeof(Thing))]
            internal partial class Context : NbtSerializerContext;
            """).Generator);

        Assert.Contains(reason, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[NbtProperty] private int Value { get; set; }")]
    [InlineData("[NbtProperty] private int _value;")]
    [InlineData("public int Value { get; set; } [NbtConstructor] private Box(int value) { Value = value; }")]
    public void ReportsInaccessibleGenericMember(string members)
    {
        AssertSingle("NBTGEN005", Run($$"""
            public class Box<T> { public Box() { } {{members}} }

            [NbtSerializable(typeof(Box<int>))]
            internal partial class Context : NbtSerializerContext;
            """).Generator);
    }

    [Fact]
    public void SkipsGenerationWhenErrorsAreReported()
    {
        var (diagnostics, _) = Run("""
            [NbtSerializable(typeof(decimal))]
            internal partial class Context : NbtSerializerContext;
            """, out var generated);

        AssertSingle("NBTGEN002", diagnostics);
        Assert.Empty(generated);
    }

    private static Diagnostic AssertSingle(string id, ImmutableArray<Diagnostic> diagnostics)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

        // The generator caches locations as path and span, so they come back as external-file locations.
        Assert.Equal(SourcePath, diagnostic.Location.GetLineSpan().Path);
        return diagnostic;
    }

    private static (ImmutableArray<Diagnostic> Generator, ImmutableArray<Diagnostic> Output) Run(string source) =>
        Run(source, out _);

    /// <returns>The generator's diagnostics, and the diagnostics of the compilation including the generated code.</returns>
    private static (ImmutableArray<Diagnostic> Generator, ImmutableArray<Diagnostic> Output) Run(string source, out ImmutableArray<SyntaxTree> generated)
    {
        const string Usings = """
            global using System;
            global using System.Collections.Generic;
            global using Ubiety.Nbt;
            global using Ubiety.Nbt.Serialization;
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(Usings, parseOptions), CSharpSyntaxTree.ParseText(source, parseOptions, SourcePath)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([new NbtSerializerGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        generated = driver.GetRunResult().GeneratedTrees;
        return (generatorDiagnostics, output.GetDiagnostics());
    }
}
