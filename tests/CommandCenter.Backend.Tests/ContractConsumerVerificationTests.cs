using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommandCenter.Backend.Tests;

public sealed class ContractConsumerVerificationTests
{
    [Fact]
    public void RepositoryDashboardRustMirrorReportsKnownDecisionSessionSummaryOmission()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        RustContractShapeProvider rustShapes = ReadRustContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Rust shell RepositoryDashboardProjection",
            rustShapes.GetShape("RepositoryDashboardProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        ConsumerContractDrift drift = Assert.Single(drifts);
        Assert.Equal(ConsumerContractDriftKind.MissingDownstreamField, drift.Kind);
        Assert.Equal("$[].decisionSessionSummary", drift.Path);
        Assert.Equal("Rust shell RepositoryDashboardProjection", drift.Consumer);
        Assert.Equal(
            "backend serialized field is omitted by the downstream mirror",
            drift.Message);
    }

    [Fact]
    public void RepositoryDashboardRustMirrorRecursivelyVerifiesMirroredNestedShape()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        RustContractShapeProvider rustShapes = ReadRustContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Rust shell RepositoryDashboardProjection",
            rustShapes.GetShape("RepositoryDashboardProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].repository.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].executionSummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].executionHistory[].", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].continuitySummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].reasoningSummary.", StringComparison.Ordinal));
    }

    [Fact]
    public void ConsumerVerifierReportsNestedMissingFields()
    {
        using JsonDocument backend = JsonDocument.Parse("""
            {
              "repository": {
                "id": "repository-id",
                "name": "Repository",
                "path": "C:/Repository"
              }
            }
            """);
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Synthetic consumer",
            ConsumerContractShape.Object(new Dictionary<string, ConsumerContractShape>(StringComparer.Ordinal)
            {
                ["repository"] = ConsumerContractShape.Object(new Dictionary<string, ConsumerContractShape>(StringComparer.Ordinal)
                {
                    ["id"] = ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.String),
                    ["name"] = ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.String)
                })
            })));

        ConsumerContractDrift[] drifts = verifier.Compare("$", backend.RootElement).ToArray();

        ConsumerContractDrift drift = Assert.Single(drifts);
        Assert.Equal(ConsumerContractDriftKind.MissingDownstreamField, drift.Kind);
        Assert.Equal("$.repository.path", drift.Path);
    }

    private static JsonElement ReadRepositoryDashboardGoldenFixture()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            "repository-dashboard.golden.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        return document.RootElement.Clone();
    }

    private static RustContractShapeProvider ReadRustContractShapes()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.Shell", "src", "main.rs"));

        return RustContractShapeProvider.Parse(source);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(directory.Combine("src", "CommandCenter.Shell", "src", "main.rs")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class ContractConsumerVerifier(ConsumerContractVerifierSpec spec)
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
                        ConsumerContractDriftKind.MissingDownstreamField,
                        $"{path}.{missing}",
                        "backend serialized field is omitted by the downstream mirror");
                }

                foreach (string extra in consumerProperties.Keys.Except(backendProperties.Keys, StringComparer.Ordinal))
                {
                    yield return new ConsumerContractDrift(
                        spec.Consumer,
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

    private sealed class RustContractShapeProvider(IReadOnlyDictionary<string, RustStructDefinition> structs)
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
                properties.Add(ToCamelCase(field.Name), ResolveType(field.Type, resolving));
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
            return body
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
                .Select(line => Regex.Match(line, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*):\s*(?<type>[^,]+),?$"))
                .Where(field => field.Success)
                .Select(field => new RustFieldDefinition(
                    field.Groups["name"].Value,
                    field.Groups["type"].Value.Trim()))
                .ToArray();
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

    private sealed record RustStructDefinition(string Name, IReadOnlyList<RustFieldDefinition> Fields);

    private sealed record RustFieldDefinition(string Name, string Type);

    private sealed record ConsumerContractVerifierSpec(string Consumer, ConsumerContractShape RootShape);

    private sealed record ConsumerContractShape(
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

    private sealed record ConsumerContractDrift(
        string Consumer,
        ConsumerContractDriftKind Kind,
        string Path,
        string Message);

    private enum ConsumerContractShapeKind
    {
        Object,
        Array,
        Primitive,
        Any
    }

    private enum ConsumerContractPrimitiveKind
    {
        String,
        Number,
        Boolean
    }

    private enum ConsumerContractDriftKind
    {
        MissingDownstreamField,
        ExtraDownstreamField,
        ValueKindChanged
    }
}

internal static class DirectoryInfoExtensions
{
    public static string Combine(this DirectoryInfo directory, params string[] paths)
    {
        return Path.Combine([directory.FullName, .. paths]);
    }
}
