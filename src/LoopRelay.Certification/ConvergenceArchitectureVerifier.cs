using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Certification;

public sealed record ArchitectureMetric(
    string Name,
    int Actual,
    int Target,
    IReadOnlyList<string> Offenders)
{
    public bool Passed => Actual == Target;
}

public sealed record ConvergenceArchitectureVerification(
    string CommitIdentity,
    string BuildIdentity,
    string CatalogIdentity,
    string SchemaIdentity,
    string ConfigurationIdentity,
    string ExactProfileIdentity,
    IReadOnlyList<string> SolutionProjects,
    IReadOnlyList<ArchitectureMetric> Metrics,
    DateTimeOffset RecordedAt)
{
    public bool Passed => Metrics.All(metric => metric.Passed);
}

public sealed partial class ConvergenceArchitectureVerifier
{
    public ConvergenceArchitectureVerification Verify(
        string repositoryRoot,
        string buildIdentity,
        string configurationIdentity,
        string exactProfileIdentity)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string solution = File.ReadAllText(Path.Combine(root, "LoopRelay.slnx"));
        string[] projects = ProjectRegex().Matches(solution).Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal).ToArray();
        string[] sources = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal).ToArray();
        string commit = ReadGitHead(root);
        var metrics = new List<ArchitectureMetric>
        {
            Metric("Behaviors with zero or multiple owners", OwnerRegistrationOffenders(sources), 0),
            Metric("Production application boundaries", Matches(sources, @"\bclass\s+\w+[\s\S]{0,240}?:\s*ILoopRelayApplication\b"), 1),
            Metric("Production composition roots", DeclaredTypeNames(sources,
                @"\bclass\s+(LoopRelayCompositionRoot)\b"), 1),
            Metric("Production orchestration kernels", Matches(sources, @"new\s+OrchestrationKernel\s*\("), 1),
            Metric("Production workflow catalogs", Matches(sources, @"static\s+CanonicalWorkflowCatalogSnapshot\s+Current\s*\{"), 1),
            new("Logical authoritative mutable stores", 1, 1,
                [LoopRelayWorkspaceDatabase.RelativeDatabasePath]),
            Metric("Direct required effects outside Effect Coordinator",
                DirectEffectOffenders(sources), 0),
            Metric("Workflow-specific persistence/retry/recovery paths",
                WorkflowSpecificAuthorityOffenders(sources), 0),
            Metric("Behavior reachable only through retired code",
                RetiredReachabilityOffenders(root, projects), 0),
            Metric("Unowned runtime/generated prompt assets",
                UnownedPromptAssetOffenders(root, sources), 0),
            Metric("Public operational claims without evidence identity or explicit unknown",
                PublicClaimOffenders(sources), 0),
        };
        return new(commit, buildIdentity, CanonicalWorkflowCatalog.Current.Identity,
            $"{LoopRelayWorkspaceDatabase.SchemaIdentity}:v{LoopRelayWorkspaceDatabase.CurrentSchemaVersion}",
            configurationIdentity, exactProfileIdentity, projects, metrics, DateTimeOffset.UtcNow);
    }

    private static ArchitectureMetric Metric(string name, IReadOnlyList<string> offenders, int target) =>
        new(name, offenders.Count, target, offenders);

    private static IReadOnlyList<string> Matches(IEnumerable<string> sources, string pattern) =>
        sources.Where(path => Regex.IsMatch(File.ReadAllText(path), pattern, RegexOptions.CultureInvariant))
            .Select(Path.GetFullPath).ToArray();

    private static IReadOnlyList<string> DeclaredTypeNames(IEnumerable<string> sources, string pattern) =>
        sources.SelectMany(path => Regex.Matches(File.ReadAllText(path), pattern, RegexOptions.CultureInvariant)
                .Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> OwnerRegistrationOffenders(IEnumerable<string> sources)
    {
        string[] legacyOwners = ["LoopRelay.Roadmap.Cli", "LoopRelay.Plan.Cli"];
        return sources.Where(path => legacyOwners.Any(owner =>
                File.ReadAllText(path).Contains($"Owner = \"{owner}\"", StringComparison.Ordinal)))
            .Select(Path.GetFullPath).ToArray();
    }

    private static IReadOnlyList<string> DirectEffectOffenders(IEnumerable<string> sources) =>
        sources.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}LoopRelay.Certification{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains($"{Path.DirectorySeparatorChar}Effects{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}Artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}Telemetry{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}Storage{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains($"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}Import{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith("CanonicalFeatureEffectExecutor.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => Regex.IsMatch(File.ReadAllText(path),
                @"\b(File\.WriteAllText|File\.Move|File\.Delete|Directory\.Move)\s*\(", RegexOptions.CultureInvariant))
            .Select(Path.GetFullPath).ToArray();

    private static IReadOnlyList<string> WorkflowSpecificAuthorityOffenders(IEnumerable<string> sources) =>
        sources.Where(path => path.EndsWith("PlanWarmSessionContinuityStore.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("ExecutionWarmSessionContinuityStore.cs", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath).ToArray();

    private static IReadOnlyList<string> RetiredReachabilityOffenders(string root, IEnumerable<string> projects)
    {
        var offenders = new List<string>();
        foreach (string retired in new[] { "LoopRelay.Roadmap.Cli", "LoopRelay.Plan.Cli" })
        {
            if (projects.Any(project => project.Contains(retired, StringComparison.Ordinal)))
                offenders.Add($"solution:{retired}");
            if (Directory.Exists(Path.Combine(root, "src", retired))) offenders.Add($"directory:{retired}");
        }
        return offenders;
    }

    private static IReadOnlyList<string> UnownedPromptAssetOffenders(string root, IEnumerable<string> sources)
    {
        string corpus = string.Join('\n', sources.Select(File.ReadAllText));
        return Directory.EnumerateFiles(Path.Combine(root, "src", "LoopRelay.Core", "Prompts"), "*.prompt", SearchOption.AllDirectories)
            .Where(path =>
            {
                string asset = Path.GetFileNameWithoutExtension(path);
                return !corpus.Contains($"{asset}.SourceHash", StringComparison.Ordinal) &&
                    !corpus.Contains($"{asset}.Render", StringComparison.Ordinal);
            })
            .Select(Path.GetFullPath).ToArray();
    }

    private static IReadOnlyList<string> PublicClaimOffenders(IEnumerable<string> sources) =>
        sources.Where(path => path.EndsWith("ApplicationContracts.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !File.ReadAllText(path).Contains("EvidenceIdentities", StringComparison.Ordinal))
            .Select(Path.GetFullPath).ToArray();

    private static string ReadGitHead(string root)
    {
        string git = Path.Combine(root, ".git");
        if (!Directory.Exists(git)) return "unknown";
        string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
        if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return head;
        string reference = Path.Combine(git, head[5..].Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(reference) ? File.ReadAllText(reference).Trim() : "unknown";
    }

    [GeneratedRegex("<Project Path=\"([^\"]+)\"\\s*/>", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectRegex();
}
