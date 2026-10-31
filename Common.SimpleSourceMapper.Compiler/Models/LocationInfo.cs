using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Common.SimpleSourceMapper.Compiler.Models;

/// <summary>Equatable, symbol-free snapshot of a source location.</summary>
internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationInfo? From(Location? location)
    {
        if (location is null || location.SourceTree is null)
        {
            return null;
        }

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}

/// <summary>Equatable diagnostic description produced by symbol-free pipeline stages.</summary>
internal sealed record DiagnosticInfo(string Id, LocationInfo? Location, EquatableArray<string> Arguments)
{
    public Diagnostic ToDiagnostic()
    {
        var descriptor = Diagnostics.DiagnosticDescriptors.ById(Id);
        var args = new object?[Arguments.Count];
        for (var i = 0; i < args.Length; i++)
        {
            args[i] = Arguments[i];
        }

        return Diagnostic.Create(descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, args);
    }
}
