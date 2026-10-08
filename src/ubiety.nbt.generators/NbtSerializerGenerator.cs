using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ubiety.Nbt.Generators;

/// <summary>
/// Generates reflection-free NBT serialization metadata for partial classes deriving from <c>NbtSerializerContext</c>
/// and marked with <c>[NbtSerializable(typeof(...))]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class NbtSerializerGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var contexts = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Ubiety.Nbt.Serialization.NbtSerializableAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, cancellationToken) => Parser.Parse(syntaxContext, cancellationToken))
            .Where(static model => model is not null)
            .Collect();

        context.RegisterSourceOutput(contexts, static (output, models) =>
        {
            // A partial class with attributes on several declarations is reported once per declaration.
            var seen = new HashSet<string>();
            foreach (var model in models)
            {
                if (!seen.Add(model!.FullName))
                {
                    continue;
                }

                foreach (var diagnostic in model.Diagnostics)
                {
                    output.ReportDiagnostic(diagnostic.ToDiagnostic());
                }

                if (model.Diagnostics.Any(d => d.Descriptor.DefaultSeverity == DiagnosticSeverity.Error))
                {
                    continue;
                }

                output.AddSource($"{model.FullName}.NbtSerializerContext.g.cs", Emitter.Emit(model));
            }
        });
    }
}
