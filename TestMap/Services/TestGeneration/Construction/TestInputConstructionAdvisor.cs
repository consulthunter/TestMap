using Microsoft.CodeAnalysis;

namespace TestMap.Services.TestGeneration.Construction;

public static class TestInputConstructionAdvisor
{
    private static readonly HashSet<string> SimpleTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bool",
        "Boolean",
        "byte",
        "sbyte",
        "short",
        "ushort",
        "int",
        "Int32",
        "uint",
        "long",
        "Int64",
        "ulong",
        "float",
        "double",
        "decimal",
        "char",
        "string",
        "String",
        "object",
        "Object",
        "DateTime",
        "DateOnly",
        "TimeOnly",
        "TimeSpan",
        "Guid",
        "CancellationToken"
    };

    public static ConstructionAdvice ForTypeName(string typeName, string variableName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return new ConstructionAdvice("unknown", $"{variableName}: inspect the target type before constructing test inputs.");

        var normalized = NormalizeTypeName(typeName);
        if (IsSystemType(normalized))
        {
            return new ConstructionAdvice(
                "type-token",
                $"{variableName}: supply a Type value with null or typeof(...) based on the branch being tested; do not instantiate System.Type.");
        }

        if (SimpleTypeNames.Contains(normalized) || normalized.EndsWith("?", StringComparison.Ordinal))
            return new ConstructionAdvice("primitive-or-default", $"{variableName}: use a simple literal or default value for {typeName}.");

        if (IsCollectionType(normalized))
            return new ConstructionAdvice("collection", $"{variableName}: create a small {typeName} with representative element values.");

        if (RequiresMocking(normalized))
            return new ConstructionAdvice("mock", $"{variableName}: provide a mock or test double for {typeName}.");

        if (LooksLikeEnum(normalized))
            return new ConstructionAdvice("enum-or-named-value", $"{variableName}: choose the enum/named value that reaches the target branch.");

        return new ConstructionAdvice("construct", $"{variableName}: construct {typeName}, preferring the smallest usable constructor.");
    }

    public static ConstructionAdvice ForSymbol(ITypeSymbol type, string variableName)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var fullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (fullyQualifiedName == "global::System.Type")
        {
            return new ConstructionAdvice(
                "type-token",
                $"{variableName}: supply a Type value with null or typeof(...) based on the branch being tested; do not instantiate System.Type.");
        }

        if (type.NullableAnnotation == NullableAnnotation.Annotated &&
            type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
            return new ConstructionAdvice("primitive-or-default", $"{variableName}: use null or a simple {typeName} value based on the scenario.");

        if (type.SpecialType != SpecialType.None || IsKnownSimpleFrameworkType(type))
            return new ConstructionAdvice("primitive-or-default", $"{variableName}: use a simple literal or default value for {typeName}.");

        if (TryResolveElementType(type, out _))
            return new ConstructionAdvice("collection", $"{variableName}: create a small {typeName} with representative element values.");

        if (type.TypeKind is TypeKind.Interface || type.IsAbstract)
            return new ConstructionAdvice("mock", $"{variableName}: provide a mock or test double for {typeName}.");

        if (type.TypeKind == TypeKind.Enum)
            return new ConstructionAdvice("enum-or-named-value", $"{variableName}: choose the enum/named value that reaches the target branch.");

        return new ConstructionAdvice("construct", $"{variableName}: construct {typeName}, preferring the smallest usable constructor.");
    }

    public static string BuildParameterSnippet(string typeName, string variableName)
    {
        var normalized = NormalizeTypeName(typeName).TrimEnd('?');
        return normalized switch
        {
            "string" or "String" => $"var {variableName} = \"value\";",
            "int" or "Int32" => $"var {variableName} = 1;",
            "long" or "Int64" => $"var {variableName} = 1L;",
            "bool" or "Boolean" => $"var {variableName} = true;",
            "Type" => $"var {variableName} = typeof(object);",
            _ when RequiresMocking(normalized) => $"// Arrange a mock or fake for {typeName} {variableName}",
            _ => $"var {variableName} = new {typeName.Trim().TrimEnd('?')}();"
        };
    }

    public static bool RequiresMocking(string typeName)
    {
        var normalized = NormalizeTypeName(typeName).TrimEnd('?');
        return normalized.StartsWith('I') &&
               normalized.Length > 1 &&
               char.IsUpper(normalized[1]) &&
               normalized != "Int32";
    }

    public static bool TryResolveElementType(ITypeSymbol type, out ITypeSymbol elementType)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }

        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.TypeArguments.Length == 1 &&
                namedType.AllInterfaces.Any(x => x.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
            {
                elementType = namedType.TypeArguments[0];
                return true;
            }

            if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T &&
                namedType.TypeArguments.Length == 1)
            {
                elementType = namedType.TypeArguments[0];
                return true;
            }
        }

        elementType = type;
        return false;
    }

    public static string NormalizeTypeName(string typeName)
    {
        var trimmed = typeName.Trim();
        var lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 ? trimmed[(lastDot + 1)..] : trimmed;
    }

    private static bool IsSystemType(string normalizedTypeName)
    {
        return normalizedTypeName is "Type" or "System.Type";
    }

    private static bool IsCollectionType(string normalizedTypeName)
    {
        return normalizedTypeName.EndsWith("[]", StringComparison.Ordinal) ||
               normalizedTypeName.StartsWith("IEnumerable<", StringComparison.Ordinal) ||
               normalizedTypeName.StartsWith("IReadOnlyList<", StringComparison.Ordinal) ||
               normalizedTypeName.StartsWith("ICollection<", StringComparison.Ordinal) ||
               normalizedTypeName.StartsWith("List<", StringComparison.Ordinal);
    }

    private static bool LooksLikeEnum(string normalizedTypeName)
    {
        return normalizedTypeName.EndsWith("Status", StringComparison.Ordinal) ||
               normalizedTypeName.EndsWith("Kind", StringComparison.Ordinal) ||
               normalizedTypeName.EndsWith("Mode", StringComparison.Ordinal) ||
               normalizedTypeName.EndsWith("State", StringComparison.Ordinal);
    }

    private static bool IsKnownSimpleFrameworkType(ITypeSymbol type)
    {
        var fullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fullyQualifiedName is
            "global::System.DateTime" or
            "global::System.DateOnly" or
            "global::System.TimeOnly" or
            "global::System.TimeSpan" or
            "global::System.Guid" or
            "global::System.Threading.CancellationToken";
    }
}

public sealed record ConstructionAdvice(string Strategy, string Summary);
