using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Common.SimpleSourceMapper.Compiler.Generation;

internal static class Identifier
{
    public static string Escape(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    /// <summary>Namespace as written in a declaration, or empty for the global namespace.</summary>
    public static string FormatNamespace(INamespaceSymbol? ns)
    {
        var parts = new List<string>();
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
        {
            parts.Insert(0, Escape(current.Name));
        }

        return string.Join(".", parts);
    }
}
