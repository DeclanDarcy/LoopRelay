using System.Security.Cryptography;
using System.Text;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Orchestration.Services;

public sealed record PlanPromptContextResult(
    bool IsUsable,
    IReadOnlyList<PromptContextSection> Sections,
    IReadOnlyDictionary<string, string> Metadata,
    string Explanation,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<ConsumedInputFile> ConsumedFiles);

public static class PlanPromptContext
{
    public const string AdversarialPlanReviewProjectionPath =
        OrchestrationArtifactPaths.AgentsDirectory + "/projections/adversarial-plan-review.md";

    public static WorkflowTransitionIdentity WriteExecutablePlan { get; } =
        new("WriteExecutablePlan");

    public static WorkflowTransitionIdentity RunAdversarialReview { get; } =
        new("RunAdversarialReview");

    public static WorkflowTransitionIdentity RevisePlan { get; } =
        new("RevisePlan");

    public static WorkflowTransitionIdentity GenerateOperationalContext { get; } =
        new("GenerateOperationalContext");

    public static WorkflowTransitionIdentity CollectExecutionDetails { get; } =
        new("CollectExecutionDetails");

    public static WorkflowTransitionIdentity GenerateExecutionMilestones { get; } =
        new("GenerateExecutionMilestones");

    public static WorkflowTransitionIdentity RefineExecutionDetails { get; } =
        new("RefineExecutionDetails");

    public static bool Supports(WorkflowTransitionIdentity transition) =>
        transition == WriteExecutablePlan ||
        transition == RunAdversarialReview ||
        transition == RevisePlan ||
        transition == GenerateOperationalContext ||
        transition == CollectExecutionDetails ||
        transition == GenerateExecutionMilestones ||
        transition == RefineExecutionDetails;

    public static PlanPromptContextResult Build(
        string repositoryPath,
        WorkflowTransitionDefinition definition,
        ProductResolutionResult inputs)
    {
        string root = Path.GetFullPath(repositoryPath);
        IReadOnlyList<PlanPromptContextSource> sources = definition.Identity.Value switch
        {
            "WriteExecutablePlan" =>
            [
                Product("Prepared Epic", ProductIdentity.PreparedEpic, [OrchestrationArtifactPaths.AgentsDirectory + "/epic.md"]),
                Product("Milestone Specification", ProductIdentity.MilestoneSpecificationSet, ListRelativeFiles(root, OrchestrationArtifactPaths.SpecsDirectory, "*.md")),
            ],
            "GenerateOperationalContext" =>
            [
                Product("Executable Plan", ProductIdentity.ExecutablePlan, [OrchestrationArtifactPaths.Plan]),
            ],
            "RunAdversarialReview" =>
            [
                Product("Executable Plan", ProductIdentity.ExecutablePlan, [OrchestrationArtifactPaths.Plan]),
                Product("Adversarial Projection", ProductIdentity.AdversarialProjection, [AdversarialPlanReviewProjectionPath]),
            ],
            "RevisePlan" =>
            [
                Product("Executable Plan", ProductIdentity.ExecutablePlan, [OrchestrationArtifactPaths.Plan]),
                Product("Adversarial Review", ProductIdentity.AdversarialReview, []),
            ],
            "CollectExecutionDetails" =>
            [
                Product("Executable Plan", ProductIdentity.ExecutablePlan, [OrchestrationArtifactPaths.Plan]),
                Product("Milestone Specification", ProductIdentity.MilestoneSpecificationSet, ListRelativeFiles(root, OrchestrationArtifactPaths.SpecsDirectory, "*.md")),
            ],
            "GenerateExecutionMilestones" =>
            [
                Product("Executable Plan", ProductIdentity.ExecutablePlan, [OrchestrationArtifactPaths.Plan]),
            ],
            "RefineExecutionDetails" =>
            [
                Product("Execution Details", ProductIdentity.ExecutionDetails, [OrchestrationArtifactPaths.Details]),
                Product("Execution Milestone", ProductIdentity.ExecutionMilestoneSet, ListRelativeFiles(root, OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)),
            ],
            _ => [],
        };

        var sections = new List<PromptContextSection>();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["plan.context.transition"] = definition.Identity.Value,
        };
        var evidence = new List<string>();
        var blockers = new List<string>();
        var consumedFiles = new List<ConsumedInputFile>();

        if (PlanScopedArtifactOperationCatalog.TryGet(definition.Identity, out PlanScopedArtifactOperationSpec operation))
        {
            AddScopedOperationContract(definition, operation, sections, metadata);
        }

        foreach (PlanPromptContextSource source in sources)
        {
            IReadOnlyList<string> paths = ResolveProductPaths(inputs, source.Product, source.DefaultPaths);
            if (paths.Count == 0)
            {
                blockers.Add($"{source.Title} product has no storage representation.");
                continue;
            }

            foreach (string relativePath in paths)
            {
                string absolutePath = Path.Combine(root, Normalize(relativePath));
                if (!File.Exists(absolutePath))
                {
                    blockers.Add($"{source.Title} source `{relativePath}` is missing.");
                    evidence.Add(relativePath);
                    continue;
                }

                string content = File.ReadAllText(absolutePath);
                consumedFiles.Add(ConsumedInputFile.FromContent(relativePath, content));
                if (string.IsNullOrWhiteSpace(content))
                {
                    blockers.Add($"{source.Title} source `{relativePath}` is empty.");
                    evidence.Add(relativePath);
                    continue;
                }

                string trimmed = content.Trim();
                string title = paths.Count == 1 ? source.Title : $"{source.Title}: {Path.GetFileName(relativePath)}";
                string hash = Hash(trimmed);
                sections.Add(new PromptContextSection(title, trimmed, relativePath, [relativePath]));
                evidence.Add(relativePath);
                string key = $"plan.context.{SanitizeKey(source.Title)}.{SanitizeKey(relativePath)}.hash";
                metadata[key] = hash;
            }
        }

        metadata["plan.context.section_count"] = sections.Count.ToString();

        if (blockers.Count > 0)
        {
            return new PlanPromptContextResult(
                IsUsable: false,
                Sections: sections,
                Metadata: metadata,
                Explanation: "Plan prompt context is unavailable: " + string.Join("; ", blockers),
                Evidence: evidence.Distinct(StringComparer.Ordinal).ToArray(),
                ConsumedFiles: consumedFiles);
        }

        return new PlanPromptContextResult(
            IsUsable: true,
            Sections: sections,
            Metadata: metadata,
            Explanation: $"Plan prompt context loaded for `{definition.Identity}`.",
            Evidence: evidence.Distinct(StringComparer.Ordinal).ToArray(),
            ConsumedFiles: consumedFiles);
    }

    private static PlanPromptContextSource Product(
        string title,
        ProductIdentity product,
        IReadOnlyList<string> defaultPaths) =>
        new(title, product, defaultPaths);

    private static IReadOnlyList<string> ResolveProductPaths(
        ProductResolutionResult inputs,
        ProductIdentity product,
        IReadOnlyList<string> defaultPaths)
    {
        ProductRecord? resolved = inputs.Products.FirstOrDefault(item => item.Identity == product);
        string[] paths = (resolved?.StorageRepresentations.Count > 0
                ? resolved.StorageRepresentations
                : defaultPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return paths;
    }

    private static IReadOnlyList<string> ListRelativeFiles(
        string root,
        string relativeDirectory,
        string pattern)
    {
        string directory = Path.Combine(root, Normalize(relativeDirectory));
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void AddScopedOperationContract(
        WorkflowTransitionDefinition definition,
        PlanScopedArtifactOperationSpec operation,
        List<PromptContextSection> sections,
        Dictionary<string, string> metadata)
    {
        string evidence = $"canonical://plan/scoped-artifact-operation/{operation.Label}";
        metadata["plan.scoped_operation.transition"] = operation.Transition.Value;
        metadata["plan.scoped_operation.prompt_identity"] = operation.PromptIdentity;
        metadata["plan.scoped_operation.label"] = operation.Label;
        metadata["plan.scoped_operation.execution_posture"] = definition.ExecutionPosture.Kind.ToString();
        metadata["plan.scoped_operation.allowed_reads"] = Join(operation.AllowedReads);
        metadata["plan.scoped_operation.allowed_read_globs"] = JoinGlobs(operation.AllowedReadGlobs);
        metadata["plan.scoped_operation.allowed_writes"] = Join(operation.AllowedWrites);
        metadata["plan.scoped_operation.allowed_write_globs"] = JoinGlobs(operation.AllowedWriteGlobs);
        metadata["plan.scoped_operation.required_outputs"] = Join(operation.RequiredOutputs);
        metadata["plan.scoped_operation.required_output_glob"] = operation.RequiredOutputGlob is null
            ? string.Empty
            : FormatGlob(operation.RequiredOutputGlob);
        metadata["plan.scoped_operation.changed_guard"] = operation.ChangedGuard ?? string.Empty;
        metadata["plan.scoped_operation.require_checklist_in_glob"] = operation.RequireChecklistInGlob
            ? "true"
            : "false";
        metadata["plan.scoped_operation.preserve_write_glob_file_set"] = operation.PreserveWriteGlobFileSet
            ? "true"
            : "false";

        sections.Add(new PromptContextSection(
            "Scoped Operation Contract",
            $"""
            Transition: {operation.Transition}
            Prompt identity: {operation.PromptIdentity}
            Label: {operation.Label}
            Execution posture: {definition.ExecutionPosture.Kind}
            Allowed reads:
            {Bullets(operation.AllowedReads)}
            Allowed read globs:
            {Bullets(operation.AllowedReadGlobs.Select(FormatGlob))}
            Allowed writes:
            {Bullets(operation.AllowedWrites)}
            Allowed write globs:
            {Bullets(operation.AllowedWriteGlobs.Select(FormatGlob))}
            Required outputs:
            {Bullets(operation.RequiredOutputs)}
            Required output glob: {(operation.RequiredOutputGlob is null ? "(none)" : FormatGlob(operation.RequiredOutputGlob))}
            Changed guard: {operation.ChangedGuard ?? "(none)"}
            Requires checklist in required output glob: {operation.RequireChecklistInGlob.ToString().ToLowerInvariant()}
            Preserve write-glob file set: {operation.PreserveWriteGlobFileSet.ToString().ToLowerInvariant()}
            """,
            evidence,
            [evidence]));
    }

    private static string Join(IEnumerable<string> values) =>
        string.Join("|", values);

    private static string JoinGlobs(IEnumerable<OperationPathGlob> globs) =>
        string.Join("|", globs.Select(FormatGlob));

    private static string FormatGlob(OperationPathGlob glob) =>
        $"{glob.Directory.TrimEnd('/')}/{glob.Pattern}";

    private static string Bullets(IEnumerable<string> values)
    {
        string[] items = values.ToArray();
        return items.Length == 0
            ? "- (none)"
            : string.Join(Environment.NewLine, items.Select(item => $"- {item}"));
    }

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string SanitizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }

        return builder.ToString().Trim('_');
    }

    private sealed record PlanPromptContextSource(
        string Title,
        ProductIdentity Product,
        IReadOnlyList<string> DefaultPaths);
}
