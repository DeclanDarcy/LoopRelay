using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandCenter.Backend.Tests;

internal sealed record ContractGenerationIr(
    string ContractId,
    string ContractName,
    ContractGenerationShape RootShape,
    IReadOnlyList<ContractGenerationField> Fields,
    IReadOnlyList<ContractGenerationFieldMetadata> FieldMetadata);

internal sealed record ContractGenerationField(
    string Path,
    ContractGenerationShapeKind ShapeKind,
    string TypeScriptType);

internal sealed record ContractGenerationFieldMetadata(
    string Path,
    ContractGenerationPresence Presence,
    ContractGenerationNullability Nullability,
    string? SemanticDomain,
    IReadOnlyList<string> DomainValues,
    string? IdentityRole,
    ContractGenerationArrayOrdering? ArrayOrdering,
    string? StringFormat,
    string Source);

internal sealed record ContractGenerationShape(
    ContractGenerationShapeKind Kind,
    IReadOnlyList<ContractGenerationShape> Items,
    IReadOnlyList<ContractGenerationProperty> Properties);

internal sealed record ContractGenerationProperty(
    string Name,
    ContractGenerationShape Shape);

internal enum ContractGenerationShapeKind
{
    Array,
    Object,
    String,
    Number,
    Boolean,
    Null
}

internal enum ContractGenerationPresence
{
    Required,
    Optional
}

internal enum ContractGenerationNullability
{
    NonNullable,
    Nullable
}

internal enum ContractGenerationArrayOrdering
{
    Semantic,
    StableByProjection,
    Observational
}

internal static class ContractGenerationIrBuilder
{
    public static ContractGenerationIr Build(
        string contractId,
        string contractName,
        JsonElement source,
        IReadOnlyList<ContractGenerationFieldMetadata>? fieldMetadata = null)
    {
        ContractGenerationShape rootShape = ReadShape(source);
        return new ContractGenerationIr(
            contractId,
            contractName,
            rootShape,
            EnumerateFields("$", rootShape).ToArray(),
            fieldMetadata ?? []);
    }

    private static ContractGenerationShape ReadShape(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => new ContractGenerationShape(
                ContractGenerationShapeKind.Array,
                element.EnumerateArray().Take(1).Select(ReadShape).ToArray(),
                []),
            JsonValueKind.Object => new ContractGenerationShape(
                ContractGenerationShapeKind.Object,
                [],
                element.EnumerateObject()
                    .Select(property => new ContractGenerationProperty(property.Name, ReadShape(property.Value)))
                    .ToArray()),
            JsonValueKind.String => Leaf(ContractGenerationShapeKind.String),
            JsonValueKind.Number => Leaf(ContractGenerationShapeKind.Number),
            JsonValueKind.True or JsonValueKind.False => Leaf(ContractGenerationShapeKind.Boolean),
            JsonValueKind.Null => Leaf(ContractGenerationShapeKind.Null),
            _ => throw new InvalidOperationException($"Unsupported contract JSON value kind {element.ValueKind}.")
        };
    }

    private static IEnumerable<ContractGenerationField> EnumerateFields(string path, ContractGenerationShape shape)
    {
        yield return new ContractGenerationField(path, shape.Kind, ToTypeScriptType(shape));

        if (shape.Kind == ContractGenerationShapeKind.Array && shape.Items.Count > 0)
        {
            foreach (ContractGenerationField field in EnumerateFields($"{path}[]", shape.Items[0]))
            {
                yield return field;
            }
        }

        if (shape.Kind == ContractGenerationShapeKind.Object)
        {
            foreach (ContractGenerationProperty property in shape.Properties)
            {
                foreach (ContractGenerationField field in EnumerateFields($"{path}.{property.Name}", property.Shape))
                {
                    yield return field;
                }
            }
        }
    }

    private static ContractGenerationShape Leaf(ContractGenerationShapeKind kind)
    {
        return new ContractGenerationShape(kind, [], []);
    }

    private static string ToTypeScriptType(ContractGenerationShape shape)
    {
        return shape.Kind switch
        {
            ContractGenerationShapeKind.Array => shape.Items.Count == 0
                ? "unknown[]"
                : $"{ToTypeScriptType(shape.Items[0])}[]",
            ContractGenerationShapeKind.Object => "Record<string, unknown>",
            ContractGenerationShapeKind.String => "string",
            ContractGenerationShapeKind.Number => "number",
            ContractGenerationShapeKind.Boolean => "boolean",
            ContractGenerationShapeKind.Null => "null",
            _ => throw new InvalidOperationException($"Unsupported contract shape kind {shape.Kind}.")
        };
    }
}

internal static class RepositoryDashboardGenerationMetadata
{
    private const string Source = "M1.2 repository-dashboard governed schema metadata pilot";

    public static IReadOnlyList<ContractGenerationFieldMetadata> Fields { get; } =
    [
        RequiredNonNullable("$[]", arrayOrdering: null),
        RequiredNonNullable("$[].repository.id", identityRole: "RepositoryId"),
        RequiredNonNullable("$[].repository.name"),
        RequiredNonNullable("$[].repository.path", semanticDomain: "RepositoryPath"),
        RequiredNonNullable(
            "$[].availability",
            semanticDomain: "RepositoryAvailability",
            domainValues: ["Available", "Missing", "AccessDenied"]),
        RequiredNonNullable(
            "$[].readiness",
            semanticDomain: "ExecutionReadiness",
            domainValues: ["MissingPlan", "MissingMilestones", "Ready"]),
        RequiredNonNullable(
            "$[].executionState",
            semanticDomain: "RepositoryExecutionState",
            domainValues:
            [
                "Ready",
                "Executing",
                "AwaitingAcceptance",
                "Accepted",
                "AwaitingCommit",
                "AwaitingPush",
                "Failed",
                "Cancelled"
            ]),
        RequiredNullable("$[].activeExecutionSession", semanticDomain: "ExecutionSessionSummary"),
        RequiredNullable("$[].executionSummary", semanticDomain: "ExecutionSessionSummary"),
        RequiredNonNullable("$[].executionHistory", arrayOrdering: ContractGenerationArrayOrdering.StableByProjection),
        RequiredNullable("$[].executionSummary.completedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionSummary.duration", stringFormat: "duration"),
        RequiredNullable("$[].executionSummary.acceptedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionSummary.rejectedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionSummary.decisionNote", semanticDomain: "HumanAuthoredText"),
        RequiredNullable("$[].executionSummary.providerExecutablePath", semanticDomain: "ProviderExecutablePath"),
        RequiredNullable("$[].executionSummary.providerProcessId"),
        RequiredNullable("$[].executionSummary.providerStartedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionSummary.commitSha", semanticDomain: "GitCommitSha"),
        RequiredNullable("$[].executionSummary.committedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionSummary.commitMessage", semanticDomain: "GitCommitMessage"),
        RequiredNullable("$[].executionSummary.pushAttemptedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionSummary.pushedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionSummary.pushedCommitSha", semanticDomain: "GitCommitSha"),
        RequiredNullable("$[].executionSummary.pushRemoteName", semanticDomain: "GitRemoteName"),
        RequiredNullable("$[].executionSummary.pushBranchName", semanticDomain: "GitBranchName"),
        RequiredNullable("$[].executionSummary.failureReason", semanticDomain: "ExecutionFailureReason"),
        RequiredNullable("$[].executionHistory[].completedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionHistory[].duration", stringFormat: "duration"),
        RequiredNullable("$[].executionHistory[].acceptedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionHistory[].rejectedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionHistory[].decisionNote", semanticDomain: "HumanAuthoredText"),
        RequiredNullable("$[].executionHistory[].providerExecutablePath", semanticDomain: "ProviderExecutablePath"),
        RequiredNullable("$[].executionHistory[].providerProcessId"),
        RequiredNullable("$[].executionHistory[].providerStartedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionHistory[].commitSha", semanticDomain: "GitCommitSha"),
        RequiredNullable("$[].executionHistory[].committedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionHistory[].commitMessage", semanticDomain: "GitCommitMessage"),
        RequiredNullable("$[].executionHistory[].pushAttemptedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionHistory[].pushedAt", stringFormat: "date-time"),
        RequiredNullable("$[].executionHistory[].pushedCommitSha", semanticDomain: "GitCommitSha"),
        RequiredNullable("$[].executionHistory[].pushRemoteName", semanticDomain: "GitRemoteName"),
        RequiredNullable("$[].executionHistory[].pushBranchName", semanticDomain: "GitBranchName"),
        RequiredNullable("$[].executionHistory[].failureReason", semanticDomain: "ExecutionFailureReason"),
        RequiredNullable("$[].continuitySummary.operationalContextLastUpdatedAt", stringFormat: "date-time"),
        RequiredNullable("$[].reasoningSummary.lastEventAt", stringFormat: "date-time"),
        RequiredNullable("$[].reasoningSummary.lastThreadActivityAt", stringFormat: "date-time"),
        RequiredNullable("$[].reasoningSummary.lastRelationshipAt", stringFormat: "date-time"),
        RequiredNullable("$[].reasoningSummary.lastActivityAt", stringFormat: "date-time"),
        RequiredNullable("$[].reasoningSummary.lastReconstructionAt", stringFormat: "date-time"),
        RequiredNullable("$[].reasoningSummary.lastCertificationAt", stringFormat: "date-time"),
        RequiredNullable("$[].reasoningSummary.certificationResult", semanticDomain: "ReasoningCertificationResult"),
        RequiredNullable("$[].decisionSessionSummary.decisionSessionId", identityRole: "DecisionSessionId"),
        RequiredNullable(
            "$[].decisionSessionSummary.state",
            semanticDomain: "DecisionSessionState",
            domainValues: ["Created", "Active", "TransferPending", "Transferred", "Retired"]),
        RequiredNullable(
            "$[].decisionSessionSummary.lifecycleDecision",
            semanticDomain: "DecisionSessionLifecycleDecision",
            domainValues: ["Continue", "Transfer"]),
        RequiredNullable(
            "$[].decisionSessionSummary.transferEligibilityStatus",
            semanticDomain: "DecisionSessionTransferEligibilityStatus",
            domainValues: ["NotApplicable", "Eligible", "Blocked", "Deferred"]),
        RequiredNullable("$[].decisionSessionSummary.estimatedTokenCount"),
        RequiredNullable("$[].decisionSessionSummary.estimatedCacheTtl", stringFormat: "duration"),
        RequiredNullable("$[].decisionSessionSummary.cacheMissRisk"),
        RequiredNullable("$[].decisionSessionSummary.coherenceScore"),
        RequiredNullable("$[].decisionSessionSummary.transferPressure"),
        RequiredNonNullable("$[].decisionSessionSummary.healthDimensions", arrayOrdering: ContractGenerationArrayOrdering.StableByProjection),
        RequiredNonNullable("$[].decisionSessionSummary.recentTransferLineage", arrayOrdering: ContractGenerationArrayOrdering.StableByProjection),
        RequiredNonNullable("$[].decisionSessionSummary.diagnostics", arrayOrdering: ContractGenerationArrayOrdering.StableByProjection),
        RequiredNullable("$[].decisionSessionSummary.recentTransferLineage[].targetSessionId", identityRole: "DecisionSessionId"),
        RequiredNullable("$[].decisionSessionSummary.recentTransferLineage[].continuityArtifactId", identityRole: "ContinuityArtifactId"),
        RequiredNullable("$[].decisionSessionSummary.recentTransferLineage[].completedAt", stringFormat: "date-time"),
        RequiredNullable("$[].decisionSessionSummary.generatedAt", stringFormat: "date-time")
    ];

    private static ContractGenerationFieldMetadata RequiredNonNullable(
        string path,
        string? semanticDomain = null,
        IReadOnlyList<string>? domainValues = null,
        string? identityRole = null,
        ContractGenerationArrayOrdering? arrayOrdering = null,
        string? stringFormat = null)
    {
        return Field(
            path,
            ContractGenerationNullability.NonNullable,
            semanticDomain,
            domainValues,
            identityRole,
            arrayOrdering,
            stringFormat);
    }

    private static ContractGenerationFieldMetadata RequiredNullable(
        string path,
        string? semanticDomain = null,
        IReadOnlyList<string>? domainValues = null,
        string? identityRole = null,
        ContractGenerationArrayOrdering? arrayOrdering = null,
        string? stringFormat = null)
    {
        return Field(
            path,
            ContractGenerationNullability.Nullable,
            semanticDomain,
            domainValues,
            identityRole,
            arrayOrdering,
            stringFormat);
    }

    private static ContractGenerationFieldMetadata Field(
        string path,
        ContractGenerationNullability nullability,
        string? semanticDomain,
        IReadOnlyList<string>? domainValues,
        string? identityRole,
        ContractGenerationArrayOrdering? arrayOrdering,
        string? stringFormat)
    {
        return new ContractGenerationFieldMetadata(
            path,
            ContractGenerationPresence.Required,
            nullability,
            semanticDomain,
            domainValues ?? [],
            identityRole,
            arrayOrdering,
            stringFormat,
            Source);
    }
}

internal static class ContractGenerationIrSerializer
{
    public static string Serialize(ContractGenerationIr ir)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

        return JsonSerializer.Serialize(ir, options) + Environment.NewLine;
    }
}

internal static class ContractTypeScriptMetadataGenerator
{
    public static string Generate(ContractGenerationIr ir)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("// Generated from the Contract Oracle fixture. Do not edit by hand.");
        builder.AppendLine();
        builder.AppendLine($"export const repositoryDashboardContractId = '{ir.ContractId}' as const");
        builder.AppendLine($"export const repositoryDashboardContractName = '{ir.ContractName}' as const");
        builder.AppendLine();
        builder.AppendLine("export type RepositoryDashboardContractField = {");
        builder.AppendLine("  path: string");
        builder.AppendLine("  shapeKind: RepositoryDashboardContractShapeKind");
        builder.AppendLine("  typeScriptType: string");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("export type RepositoryDashboardContractShapeKind =");

        foreach (ContractGenerationShapeKind kind in Enum.GetValues<ContractGenerationShapeKind>())
        {
            builder.AppendLine($"  | '{ToCamelCase(kind.ToString())}'");
        }

        builder.AppendLine();
        builder.AppendLine("export const repositoryDashboardContractFields = [");

        foreach (ContractGenerationField field in ir.Fields)
        {
            builder.AppendLine("  {");
            builder.AppendLine($"    path: '{field.Path}',");
            builder.AppendLine($"    shapeKind: '{ToCamelCase(field.ShapeKind.ToString())}',");
            builder.AppendLine($"    typeScriptType: '{field.TypeScriptType}',");
            builder.AppendLine("  },");
        }

        builder.AppendLine("] satisfies RepositoryDashboardContractField[]");
        builder.AppendLine();
        AppendFieldMetadata(builder, ir);
        AppendGeneratedTypeAliases(builder, ir);
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendFieldMetadata(StringBuilder builder, ContractGenerationIr ir)
    {
        builder.AppendLine("export type RepositoryDashboardContractFieldMetadata = {");
        builder.AppendLine("  path: string");
        builder.AppendLine("  presence: 'required' | 'optional'");
        builder.AppendLine("  nullability: 'nonNullable' | 'nullable'");
        builder.AppendLine("  semanticDomain?: string");
        builder.AppendLine("  domainValues?: readonly string[]");
        builder.AppendLine("  identityRole?: string");
        builder.AppendLine("  arrayOrdering?: 'semantic' | 'stableByProjection' | 'observational'");
        builder.AppendLine("  stringFormat?: string");
        builder.AppendLine("  source: string");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("export const repositoryDashboardContractFieldMetadata = [");

        foreach (ContractGenerationFieldMetadata metadata in ir.FieldMetadata)
        {
            builder.AppendLine("  {");
            builder.AppendLine($"    path: '{metadata.Path}',");
            builder.AppendLine($"    presence: '{ToCamelCase(metadata.Presence.ToString())}',");
            builder.AppendLine($"    nullability: '{ToCamelCase(metadata.Nullability.ToString())}',");
            if (!string.IsNullOrWhiteSpace(metadata.SemanticDomain))
            {
                builder.AppendLine($"    semanticDomain: '{metadata.SemanticDomain}',");
            }

            if (metadata.DomainValues.Count > 0)
            {
                builder.AppendLine($"    domainValues: [{string.Join(", ", metadata.DomainValues.Select(value => $"'{value}'"))}],");
            }

            if (!string.IsNullOrWhiteSpace(metadata.IdentityRole))
            {
                builder.AppendLine($"    identityRole: '{metadata.IdentityRole}',");
            }

            if (metadata.ArrayOrdering is not null)
            {
                builder.AppendLine($"    arrayOrdering: '{ToCamelCase(metadata.ArrayOrdering.Value.ToString())}',");
            }

            if (!string.IsNullOrWhiteSpace(metadata.StringFormat))
            {
                builder.AppendLine($"    stringFormat: '{metadata.StringFormat}',");
            }

            builder.AppendLine($"    source: '{metadata.Source}',");
            builder.AppendLine("  },");
        }

        builder.AppendLine("] satisfies RepositoryDashboardContractFieldMetadata[]");
        builder.AppendLine();
    }

    private static void AppendGeneratedTypeAliases(StringBuilder builder, ContractGenerationIr ir)
    {
        builder.AppendLine("export type RepositoryDashboardGeneratedContract = RepositoryDashboardGeneratedProjection[]");
        builder.AppendLine();
        builder.AppendLine("export type RepositoryDashboardGeneratedProjection = RepositoryDashboardGeneratedRootItem");
        builder.AppendLine();

        ContractGenerationShape rootItem = ir.RootShape.Kind == ContractGenerationShapeKind.Array && ir.RootShape.Items.Count > 0
            ? ir.RootShape.Items[0]
            : throw new InvalidOperationException("Repository dashboard generated aliases require an array root item shape.");

        foreach ((string name, ContractGenerationShape shape) in EnumerateObjectTypes("RepositoryDashboardGeneratedRootItem", rootItem))
        {
            builder.AppendLine($"export type {name} = {{");
            foreach (ContractGenerationProperty property in shape.Properties)
            {
                builder.AppendLine($"  {property.Name}: {ToGeneratedTypeScriptType(name, property.Name, property.Shape)}");
            }

            builder.AppendLine("}");
            builder.AppendLine();
        }
    }

    private static IEnumerable<(string Name, ContractGenerationShape Shape)> EnumerateObjectTypes(
        string typeName,
        ContractGenerationShape shape)
    {
        if (shape.Kind != ContractGenerationShapeKind.Object)
        {
            yield break;
        }

        yield return (typeName, shape);

        foreach (ContractGenerationProperty property in shape.Properties)
        {
            foreach ((string childName, ContractGenerationShape childShape) in EnumerateObjectTypes(
                ToGeneratedObjectTypeName(typeName, property.Name, property.Shape),
                property.Shape))
            {
                yield return (childName, childShape);
            }

            if (property.Shape.Kind == ContractGenerationShapeKind.Array && property.Shape.Items.Count > 0)
            {
                foreach ((string childName, ContractGenerationShape childShape) in EnumerateObjectTypes(
                    ToGeneratedObjectTypeName(typeName, property.Name, property.Shape.Items[0]),
                    property.Shape.Items[0]))
                {
                    yield return (childName, childShape);
                }
            }
        }
    }

    private static string ToGeneratedTypeScriptType(
        string parentTypeName,
        string propertyName,
        ContractGenerationShape shape)
    {
        return shape.Kind switch
        {
            ContractGenerationShapeKind.Array when shape.Items.Count == 0 => "unknown[]",
            ContractGenerationShapeKind.Array => $"{ToGeneratedTypeScriptType(parentTypeName, propertyName, shape.Items[0])}[]",
            ContractGenerationShapeKind.Object => ToGeneratedObjectTypeName(parentTypeName, propertyName, shape),
            ContractGenerationShapeKind.String => "string",
            ContractGenerationShapeKind.Number => "number",
            ContractGenerationShapeKind.Boolean => "boolean",
            ContractGenerationShapeKind.Null => "null",
            _ => throw new InvalidOperationException($"Unsupported contract shape kind {shape.Kind}.")
        };
    }

    private static string ToGeneratedObjectTypeName(
        string parentTypeName,
        string propertyName,
        ContractGenerationShape shape)
    {
        if (shape.Kind != ContractGenerationShapeKind.Object)
        {
            return ToGeneratedTypeScriptType(parentTypeName, propertyName, shape);
        }

        return parentTypeName + ToPascalCase(propertyName);
    }

    private static string ToCamelCase(string value)
    {
        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string ToPascalCase(string value)
    {
        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
