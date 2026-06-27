using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommandCenter.Backend.Tests;

internal sealed class ContractConsumerVerifier(ConsumerContractVerifierSpec spec)
{
    public IEnumerable<ConsumerContractDrift> Compare(string path, JsonElement backend)
    {
        return Compare(path, backend, spec.RootShape);
    }

    private IEnumerable<ConsumerContractDrift> Compare(
        string path,
        JsonElement backend,
        ConsumerContractShape consumer)
    {
        if (backend.ValueKind == JsonValueKind.Null && consumer.IsNullable)
        {
            yield break;
        }

        ConsumerContractShape nonNullableConsumer = consumer.WithoutNullability();
        if (!MatchesValueKind(backend.ValueKind, nonNullableConsumer.Kind, nonNullableConsumer.PrimitiveKind))
        {
            yield return new ConsumerContractDrift(
                spec.Consumer,
                spec.ConsumerCategory,
                ConsumerContractDriftKind.ValueKindChanged,
                path,
                $"backend serialized value kind {backend.ValueKind} is not accepted by downstream mirror");
            yield break;
        }

        if (backend.ValueKind == JsonValueKind.Object && nonNullableConsumer.Kind == ConsumerContractShapeKind.Object)
        {
            Dictionary<string, JsonElement> backendProperties = backend.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
            IReadOnlyDictionary<string, ConsumerContractShape> consumerProperties = nonNullableConsumer.Properties;

            foreach (string missing in backendProperties.Keys.Except(consumerProperties.Keys, StringComparer.Ordinal))
            {
                yield return new ConsumerContractDrift(
                    spec.Consumer,
                    spec.ConsumerCategory,
                    ConsumerContractDriftKind.MissingDownstreamField,
                    $"{path}.{missing}",
                    "backend serialized field is omitted by the downstream mirror");
            }

            foreach (string extra in consumerProperties.Keys.Except(backendProperties.Keys, StringComparer.Ordinal))
            {
                yield return new ConsumerContractDrift(
                    spec.Consumer,
                    spec.ConsumerCategory,
                    ConsumerContractDriftKind.ExtraDownstreamField,
                    $"{path}.{extra}",
                    "downstream mirror declares a field not present in backend serialization");
            }

            foreach ((string name, JsonElement backendValue) in backendProperties)
            {
                if (consumerProperties.TryGetValue(name, out ConsumerContractShape? consumerValue))
                {
                    foreach (ConsumerContractDrift drift in Compare($"{path}.{name}", backendValue, consumerValue))
                    {
                        yield return drift;
                    }
                }
            }
        }

        if (backend.ValueKind == JsonValueKind.Array && nonNullableConsumer.Kind == ConsumerContractShapeKind.Array)
        {
            JsonElement[] backendItems = backend.EnumerateArray().ToArray();
            if (backendItems.Length == 0)
            {
                yield break;
            }

            ConsumerContractShape itemShape = nonNullableConsumer.ItemShape
                ?? throw new InvalidOperationException("Array consumer contract shape must declare an item shape.");

            foreach (ConsumerContractDrift drift in Compare($"{path}[]", backendItems[0], itemShape))
            {
                yield return drift;
            }
        }
    }

    private static bool MatchesValueKind(
        JsonValueKind backendKind,
        ConsumerContractShapeKind consumerKind,
        ConsumerContractPrimitiveKind? primitiveKind)
    {
        if (consumerKind == ConsumerContractShapeKind.Any)
        {
            return true;
        }

        return (backendKind, consumerKind, primitiveKind) switch
        {
            (JsonValueKind.Object, ConsumerContractShapeKind.Object, _) => true,
            (JsonValueKind.Array, ConsumerContractShapeKind.Array, _) => true,
            (JsonValueKind.String, ConsumerContractShapeKind.Primitive, ConsumerContractPrimitiveKind.String) => true,
            (JsonValueKind.Number, ConsumerContractShapeKind.Primitive, ConsumerContractPrimitiveKind.Number) => true,
            (JsonValueKind.True, ConsumerContractShapeKind.Primitive, ConsumerContractPrimitiveKind.Boolean) => true,
            (JsonValueKind.False, ConsumerContractShapeKind.Primitive, ConsumerContractPrimitiveKind.Boolean) => true,
            _ => false
        };
    }
}

internal sealed class RustContractShapeProvider(IReadOnlyDictionary<string, RustStructDefinition> structs)
{
    public static RustContractShapeProvider Parse(string source)
    {
        Dictionary<string, RustStructDefinition> structs = Regex.Matches(
                source,
                @"struct\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\n\}",
                RegexOptions.Singleline)
            .Cast<Match>()
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => new RustStructDefinition(match.Groups["name"].Value, ReadFields(match.Groups["body"].Value)),
                StringComparer.Ordinal);

        return new RustContractShapeProvider(structs);
    }

    public ConsumerContractShape GetShape(string structName)
    {
        HashSet<string> resolving = new(StringComparer.Ordinal);
        return ResolveStruct(structName, resolving);
    }

    private ConsumerContractShape ResolveStruct(string structName, HashSet<string> resolving)
    {
        Assert.True(structs.ContainsKey(structName), $"Rust struct {structName} should exist.");
        Assert.True(resolving.Add(structName), $"Rust struct {structName} contains a recursive contract shape.");

        Dictionary<string, ConsumerContractShape> properties = new(StringComparer.Ordinal);
        foreach (RustFieldDefinition field in structs[structName].Fields)
        {
            properties.Add(field.SerializedName ?? ToCamelCase(field.Name), ResolveType(field.Type, resolving));
        }

        resolving.Remove(structName);
        return ConsumerContractShape.Object(properties);
    }

    private ConsumerContractShape ResolveType(string rustType, HashSet<string> resolving)
    {
        rustType = rustType.Trim();
        if (TryUnwrapGeneric(rustType, "Option", out string optionalType))
        {
            return ResolveType(optionalType, resolving).AsNullable();
        }

        if (TryUnwrapGeneric(rustType, "Vec", out string itemType))
        {
            return ConsumerContractShape.Array(ResolveType(itemType, resolving));
        }

        return rustType switch
        {
            "String" => ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.String),
            "i32" or "i64" or "u32" or "u64" or "usize" or "f32" or "f64" => ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.Number),
            "bool" => ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.Boolean),
            "Value" => ConsumerContractShape.Any(),
            _ when structs.ContainsKey(rustType) => ResolveStruct(rustType, resolving),
            _ => throw new InvalidOperationException($"Unsupported Rust contract type '{rustType}'.")
        };
    }

    private static IReadOnlyList<RustFieldDefinition> ReadFields(string body)
    {
        List<RustFieldDefinition> fields = [];
        string? serdeRename = null;
        foreach (string line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Match rename = Regex.Match(line, """^\#\[serde\(rename\s*=\s*"(?<name>[^"]+)"\)\]$""");
            if (rename.Success)
            {
                serdeRename = rename.Groups["name"].Value;
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            Match field = Regex.Match(line, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*):\s*(?<type>[^,]+),?$");
            if (!field.Success)
            {
                serdeRename = null;
                continue;
            }

            fields.Add(new RustFieldDefinition(
                field.Groups["name"].Value,
                serdeRename,
                field.Groups["type"].Value.Trim()));
            serdeRename = null;
        }

        return fields;
    }

    private static bool TryUnwrapGeneric(string rustType, string genericName, out string innerType)
    {
        string prefix = $"{genericName}<";
        if (rustType.StartsWith(prefix, StringComparison.Ordinal) && rustType.EndsWith(">", StringComparison.Ordinal))
        {
            innerType = rustType[prefix.Length..^1].Trim();
            return true;
        }

        innerType = string.Empty;
        return false;
    }

    private static string ToCamelCase(string snakeCase)
    {
        string[] parts = snakeCase.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return snakeCase;
        }

        return string.Concat(
            parts[0],
            string.Concat(parts.Skip(1).Select(part =>
                string.Concat(char.ToUpperInvariant(part[0]), part.AsSpan(1).ToString()))));
    }
}

internal sealed record RustStructDefinition(string Name, IReadOnlyList<RustFieldDefinition> Fields);

internal sealed record RustFieldDefinition(string Name, string? SerializedName, string Type);

internal sealed class TypeScriptContractShapeProvider(IReadOnlyDictionary<string, TypeScriptTypeDefinition> types)
{
    public static TypeScriptContractShapeProvider Parse(DirectoryInfo typesDirectory)
    {
        return Parse([typesDirectory]);
    }

    public static TypeScriptContractShapeProvider Parse(IEnumerable<DirectoryInfo> typeDirectories)
    {
        Dictionary<string, TypeScriptTypeDefinition> types = new(StringComparer.Ordinal);
        foreach (DirectoryInfo typesDirectory in typeDirectories)
        {
            foreach (FileInfo file in typesDirectory.EnumerateFiles("*.ts"))
            {
                foreach (TypeScriptTypeDefinition definition in ReadTypeDefinitions(File.ReadAllText(file.FullName)))
                {
                    types.Add(definition.Name, definition);
                }
            }
        }

        return new TypeScriptContractShapeProvider(types);
    }

    public ConsumerContractShape GetShape(string typeName)
    {
        HashSet<string> resolving = new(StringComparer.Ordinal);
        return ResolveType(typeName, resolving);
    }

    public ConsumerContractShape GetPropertyShape(string typeName, params string[] propertyPath)
    {
        ConsumerContractShape current = GetShape(typeName).WithoutNullability();
        foreach (string property in propertyPath)
        {
            Assert.Equal(ConsumerContractShapeKind.Object, current.Kind);
            Assert.True(
                current.Properties.TryGetValue(property, out ConsumerContractShape? next),
                $"TypeScript type {typeName} should expose property path {string.Join('.', propertyPath)}.");
            current = next.WithoutNullability();
        }

        return current;
    }

    private ConsumerContractShape ResolveType(string typeExpression, HashSet<string> resolving)
    {
        typeExpression = NormalizeTypeExpression(typeExpression);
        if (typeExpression.Contains('|', StringComparison.Ordinal))
        {
            string[] unionParts = typeExpression.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool isNullable = unionParts.Any(part => part == "null");
            string[] nonNullParts = unionParts.Where(part => part != "null").ToArray();

            if (nonNullParts.All(IsStringLiteral))
            {
                return ApplyNullability(ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.String), isNullable);
            }

            Assert.Single(nonNullParts);
            return ApplyNullability(ResolveType(nonNullParts[0], resolving), isNullable);
        }

        if (typeExpression.EndsWith("[]", StringComparison.Ordinal))
        {
            return ConsumerContractShape.Array(ResolveType(typeExpression[..^2], resolving));
        }

        if (typeExpression.StartsWith("Array<", StringComparison.Ordinal) && typeExpression.EndsWith(">", StringComparison.Ordinal))
        {
            return ConsumerContractShape.Array(ResolveType(typeExpression["Array<".Length..^1], resolving));
        }

        if (IsStringLiteral(typeExpression))
        {
            return ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.String);
        }

        if (typeExpression == "string")
        {
            return ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.String);
        }

        if (typeExpression == "number")
        {
            return ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.Number);
        }

        if (typeExpression == "boolean")
        {
            return ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.Boolean);
        }

        if (typeExpression == "null")
        {
            return ConsumerContractShape.Any().AsNullable();
        }

        if (typeExpression == "unknown" || typeExpression == "Record<string, unknown>")
        {
            return ConsumerContractShape.Any();
        }

        Assert.True(types.ContainsKey(typeExpression), $"TypeScript type {typeExpression} should exist.");
        Assert.True(resolving.Add(typeExpression), $"TypeScript type {typeExpression} contains a recursive contract shape.");

        TypeScriptTypeDefinition definition = types[typeExpression];
        ConsumerContractShape shape = definition.Body.TrimStart().StartsWith("{", StringComparison.Ordinal)
            ? ResolveObject(definition.Body, resolving)
            : ResolveType(definition.Body, resolving);

        resolving.Remove(typeExpression);
        return shape;
    }

    private ConsumerContractShape ResolveObject(string body, HashSet<string> resolving)
    {
        string objectBody = body.Trim();
        Assert.StartsWith("{", objectBody);
        Assert.EndsWith("}", objectBody);

        Dictionary<string, ConsumerContractShape> properties = new(StringComparer.Ordinal);
        foreach (string line in objectBody[1..^1].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Match property = Regex.Match(line, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<optional>\?)?:\s*(?<type>.+?)(,)?$");
            if (!property.Success)
            {
                continue;
            }

            ConsumerContractShape propertyShape = ResolveType(property.Groups["type"].Value, resolving);
            if (property.Groups["optional"].Success)
            {
                propertyShape = propertyShape.AsNullable();
            }

            properties.Add(property.Groups["name"].Value, propertyShape);
        }

        return ConsumerContractShape.Object(properties);
    }

    private static IReadOnlyList<TypeScriptTypeDefinition> ReadTypeDefinitions(string source)
    {
        MatchCollection matches = Regex.Matches(
            source,
            @"export\s+type\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<body>.*?)(?=\nexport\s+type|\z)",
            RegexOptions.Singleline);

        return matches
            .Cast<Match>()
            .Select(match => new TypeScriptTypeDefinition(
                match.Groups["name"].Value,
                TrimTrailingSemicolon(match.Groups["body"].Value)))
            .ToArray();
    }

    private static ConsumerContractShape ApplyNullability(ConsumerContractShape shape, bool isNullable)
    {
        return isNullable ? shape.AsNullable() : shape;
    }

    private static bool IsStringLiteral(string typeExpression)
    {
        return typeExpression.Length >= 2
            && typeExpression.StartsWith("'", StringComparison.Ordinal)
            && typeExpression.EndsWith("'", StringComparison.Ordinal);
    }

    private static string NormalizeTypeExpression(string typeExpression)
    {
        return TrimTrailingSemicolon(typeExpression)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim()
            .TrimEnd(',');
    }

    private static string TrimTrailingSemicolon(string value)
    {
        return value.Trim().TrimEnd(';').Trim();
    }
}

internal sealed record TypeScriptTypeDefinition(string Name, string Body);

internal sealed class DevTauriMockShapeProvider(
    ConsumerContractShape dashboardEntryShape,
    ConsumerContractShape workspaceCommandPayloadShape)
{
    public static DevTauriMockShapeProvider Parse(string source, TypeScriptContractShapeProvider typeScriptShapes)
    {
        string returnObject = ExtractReturnObject(source, "dashboardEntry");
        Assert.Contains(
            "case 'get_repository_workspace':",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "return clone(state.workspaces[getStringArg(args, 'repositoryId')])",
            source,
            StringComparison.Ordinal);

        return new DevTauriMockShapeProvider(
            ResolveObject(returnObject, typeScriptShapes),
            typeScriptShapes.GetShape("RepositoryWorkspaceProjection"));
    }

    public ConsumerContractShape GetDashboardEntryShape()
    {
        return dashboardEntryShape;
    }

    public ConsumerContractShape GetWorkspaceCommandPayloadShape()
    {
        return workspaceCommandPayloadShape;
    }

    private static ConsumerContractShape ResolveObject(string objectLiteral, TypeScriptContractShapeProvider typeScriptShapes)
    {
        string body = objectLiteral.Trim();
        Assert.StartsWith("{", body);
        Assert.EndsWith("}", body);

        Dictionary<string, ConsumerContractShape> properties = new(StringComparer.Ordinal);
        foreach (string entry in SplitTopLevelEntries(body[1..^1]))
        {
            int separator = entry.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            string name = entry[..separator].Trim();
            string expression = entry[(separator + 1)..].Trim();
            properties.Add(name, ResolveExpression(expression, typeScriptShapes));
        }

        return ConsumerContractShape.Object(properties);
    }

    private static ConsumerContractShape ResolveExpression(string expression, TypeScriptContractShapeProvider typeScriptShapes)
    {
        expression = expression.Trim();
        if (expression.StartsWith("{", StringComparison.Ordinal))
        {
            return ResolveObject(expression, typeScriptShapes);
        }

        Match nullableWorkspaceProperty = Regex.Match(
            expression,
            @"^workspace\.[A-Za-z_][A-Za-z0-9_]*\s*===\s*'.*?'\s*\?\s*workspace\.(?<property>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*null$",
            RegexOptions.Singleline);
        if (nullableWorkspaceProperty.Success)
        {
            return typeScriptShapes
                .GetPropertyShape("RepositoryWorkspaceProjection", nullableWorkspaceProperty.Groups["property"].Value)
                .AsNullable();
        }

        Match workspacePath = Regex.Match(
            expression,
            @"^workspace\.(?<path>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)$",
            RegexOptions.Singleline);
        if (workspacePath.Success)
        {
            string[] propertyPath = workspacePath.Groups["path"].Value.Split('.');
            if (propertyPath is [.., "length"])
            {
                return ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.Number);
            }

            return typeScriptShapes.GetPropertyShape("RepositoryWorkspaceProjection", propertyPath);
        }

        throw new InvalidOperationException($"Unsupported devTauriMock dashboardEntry expression '{expression}'.");
    }

    private static string ExtractReturnObject(string source, string functionName)
    {
        Match function = Regex.Match(source, $@"function\s+{Regex.Escape(functionName)}\s*\(");
        Assert.True(function.Success, $"devTauriMock function {functionName} should exist.");

        int returnIndex = source.IndexOf("return", function.Index, StringComparison.Ordinal);
        Assert.True(returnIndex >= 0, $"devTauriMock function {functionName} should return an object.");

        int objectStart = source.IndexOf('{', returnIndex);
        Assert.True(objectStart >= 0, $"devTauriMock function {functionName} should return an object literal.");

        int depth = 0;
        for (int index = objectStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[objectStart..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Could not parse returned object literal for {functionName}.");
    }

    private static IReadOnlyList<string> SplitTopLevelEntries(string body)
    {
        List<string> entries = [];
        int depth = 0;
        int start = 0;
        for (int index = 0; index < body.Length; index++)
        {
            if (body[index] == '{')
            {
                depth++;
            }
            else if (body[index] == '}')
            {
                depth--;
            }
            else if (body[index] == ',' && depth == 0)
            {
                AddEntry(body[start..index]);
                start = index + 1;
            }
        }

        AddEntry(body[start..]);
        return entries;

        void AddEntry(string entry)
        {
            string trimmed = entry.Trim();
            if (trimmed.Length > 0)
            {
                entries.Add(trimmed);
            }
        }
    }
}

internal sealed record ConsumerContractVerifierSpec(
    string Consumer,
    string ConsumerCategory,
    ConsumerContractShape RootShape);

internal sealed record ConsumerContractShape(
    ConsumerContractShapeKind Kind,
    IReadOnlyDictionary<string, ConsumerContractShape> Properties,
    ConsumerContractShape? ItemShape,
    ConsumerContractPrimitiveKind? PrimitiveKind,
    bool IsNullable)
{
    public static ConsumerContractShape Object(IReadOnlyDictionary<string, ConsumerContractShape> properties)
    {
        return new ConsumerContractShape(
            ConsumerContractShapeKind.Object,
            properties,
            null,
            null,
            false);
    }

    public static ConsumerContractShape Array(ConsumerContractShape itemShape)
    {
        return new ConsumerContractShape(
            ConsumerContractShapeKind.Array,
            EmptyProperties,
            itemShape,
            null,
            false);
    }

    public static ConsumerContractShape Primitive(ConsumerContractPrimitiveKind primitiveKind)
    {
        return new ConsumerContractShape(
            ConsumerContractShapeKind.Primitive,
            EmptyProperties,
            null,
            primitiveKind,
            false);
    }

    public static ConsumerContractShape Any()
    {
        return new ConsumerContractShape(
            ConsumerContractShapeKind.Any,
            EmptyProperties,
            null,
            null,
            false);
    }

    public ConsumerContractShape AsNullable()
    {
        return this with { IsNullable = true };
    }

    public ConsumerContractShape WithoutNullability()
    {
        return IsNullable ? this with { IsNullable = false } : this;
    }

    private static readonly IReadOnlyDictionary<string, ConsumerContractShape> EmptyProperties =
        new Dictionary<string, ConsumerContractShape>(StringComparer.Ordinal);
}

internal sealed record ConsumerContractDrift(
    string Consumer,
    string ConsumerCategory,
    ConsumerContractDriftKind Kind,
    string Path,
    string Message);

internal enum ConsumerContractShapeKind
{
    Object,
    Array,
    Primitive,
    Any
}

internal enum ConsumerContractPrimitiveKind
{
    String,
    Number,
    Boolean
}

internal enum ConsumerContractDriftKind
{
    MissingDownstreamField,
    ExtraDownstreamField,
    ValueKindChanged
}
