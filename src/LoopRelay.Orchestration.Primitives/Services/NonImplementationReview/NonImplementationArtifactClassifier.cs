using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationArtifactClassifier
{
    public const string Version = "non-implementation-artifact-classifier/v1";

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".fs", ".vb", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".css", ".scss", ".sass",
        ".less", ".html", ".htm", ".razor", ".cshtml", ".xaml", ".sql", ".rs", ".go", ".java",
        ".kt", ".kts", ".py", ".rb", ".php", ".cpp", ".c", ".h", ".hpp", ".swift",
    };

    private static readonly HashSet<string> ScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ps1", ".psm1", ".psd1", ".sh", ".bash", ".zsh", ".cmd", ".bat", ".cake",
    };

    private static readonly HashSet<string> UiAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp", ".avif", ".woff", ".woff2", ".ttf", ".eot",
    };

    private static readonly HashSet<string> MachineRequiredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sln", ".slnx", ".csproj", ".fsproj", ".vbproj", ".props", ".targets", ".lock", ".config",
        ".json", ".yml", ".yaml", ".toml", ".editorconfig", ".ruleset", ".runsettings",
    };

    private static readonly HashSet<string> MachineRequiredFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "global.json",
        "nuget.config",
        "directory.build.props",
        "directory.build.targets",
        "directory.packages.props",
        "packages.lock.json",
        "project.lock.json",
        "project.fragment.lock.json",
        "package.json",
        "package-lock.json",
        "npm-shrinkwrap.json",
        "pnpm-lock.yaml",
        "yarn.lock",
        "bun.lockb",
        "tsconfig.json",
        "jsconfig.json",
        "appsettings.json",
        "launchsettings.json",
        "xunit.runner.json",
    };

    private static readonly HashSet<string> ProseExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".mdx", ".rst", ".adoc", ".txt",
    };

    public NonImplementationArtifactClassification Classify(RepositoryChangedFileFacts file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string path = NormalizePath(file.Path);
        string extension = file.Extension;

        if (OrchestrationArtifactPaths.IsAgentsPath(path))
        {
            return Result(
                file,
                NonImplementationArtifactRoute.ExcludedSanctionedOperationalArtifact,
                "sanctioned-operational-agents",
                "LoopRelay operational artifacts under .agents are sanctioned review state, plans, milestones, evidence, or ledgers.");
        }

        if (IsImplementationArtifact(path, extension))
        {
            return Result(
                file,
                NonImplementationArtifactRoute.ExcludedImplementationArtifact,
                "implementation-artifact-path-or-extension",
                "The changed file is source, test, UI asset, migration, script, prompt resource, or tracked generated implementation content.");
        }

        if (IsMachineRequiredArtifact(path, extension))
        {
            return Result(
                file,
                NonImplementationArtifactRoute.ExcludedMachineRequiredArtifact,
                "machine-required-artifact",
                "The changed file is required by build, package, CI, runtime, test, schema, or tool configuration.");
        }

        if (IsLikelyProseCandidate(path, extension))
        {
            return Result(
                file,
                NonImplementationArtifactRoute.SemanticReviewCandidate,
                "likely-prose-design-audit-roadmap-report",
                "The changed file is likely prose, design, audit, roadmap, issue, report, or root/docs markdown and needs semantic review.");
        }

        return Result(
            file,
            NonImplementationArtifactRoute.AmbiguousForSemanticReview,
            "ambiguous-unknown-file",
            "No deterministic implementation, machine-required, or sanctioned operational exclusion matched this changed file.");
    }

    public async Task<NonImplementationArtifactClassificationSet> ClassifyAsync(RepositorySliceDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        Task<NonImplementationArtifactClassification>[] tasks = delta.ChangedFiles
            .Select(file => Task.FromResult(Classify(file)))
            .ToArray();
        NonImplementationArtifactClassification[] classifications = await Task.WhenAll(tasks);
        return new NonImplementationArtifactClassificationSet(delta.ExecutionSliceId, classifications);
    }

    private NonImplementationArtifactClassification Result(
        RepositoryChangedFileFacts file,
        NonImplementationArtifactRoute route,
        string ruleId,
        string rationale) =>
        new(file, route, ruleId, PathFacts(file), rationale, Version);

    private static IReadOnlyList<string> PathFacts(RepositoryChangedFileFacts file)
    {
        var facts = new List<string>
        {
            $"path={NormalizePath(file.Path)}",
            $"extension={file.Extension}",
            $"exists={file.Exists}",
            $"deleted={file.IsDeleted}",
            $"preExisted={file.PreExisted}",
            $"baselineStatus={file.BaselineStatus ?? "<none>"}",
            $"postStatus={file.PostStatus ?? "<none>"}",
            $"size={file.Size?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<none>"}",
            $"baselineSha256={file.BaselineContentSha256 ?? "<none>"}",
            $"postSha256={file.PostContentSha256 ?? "<none>"}",
        };

        if (!string.IsNullOrWhiteSpace(file.PreviousPath))
        {
            facts.Add($"previousPath={NormalizePath(file.PreviousPath)}");
        }

        if (file.TrackedDiffMetadata.Count > 0)
        {
            facts.Add("trackedDiff=" + string.Join(
                ",",
                file.TrackedDiffMetadata.Select(metadata =>
                    string.IsNullOrWhiteSpace(metadata.PreviousPath)
                        ? $"{metadata.Status}:{metadata.Path}"
                        : $"{metadata.Status}:{metadata.PreviousPath}->{metadata.Path}")));
        }

        return facts;
    }

    private static bool IsImplementationArtifact(string path, string extension)
    {
        if (IsPromptResource(path) ||
            IsMigration(path) ||
            IsGeneratedSource(path) ||
            IsScript(path, extension) ||
            IsUiAsset(path, extension))
        {
            return true;
        }

        return (path.StartsWith("src/", StringComparison.Ordinal) ||
                path.StartsWith("tests/", StringComparison.Ordinal)) &&
            (CodeExtensions.Contains(extension) || ScriptExtensions.Contains(extension));
    }

    private static bool IsMachineRequiredArtifact(string path, string extension)
    {
        string fileName = Path.GetFileName(path);
        string lowerPath = path.ToLowerInvariant();

        return MachineRequiredFileNames.Contains(fileName) ||
            MachineRequiredExtensions.Contains(extension) ||
            fileName.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase) && extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            lowerPath.StartsWith(".github/workflows/", StringComparison.Ordinal) ||
            lowerPath.Equals(".gitlab-ci.yml", StringComparison.Ordinal) ||
            lowerPath.Equals("azure-pipelines.yml", StringComparison.Ordinal) ||
            lowerPath.Equals("appveyor.yml", StringComparison.Ordinal);
    }

    private static bool IsLikelyProseCandidate(string path, string extension)
    {
        if (!ProseExtensions.Contains(extension))
        {
            return false;
        }

        if (!path.Contains('/', StringComparison.Ordinal))
        {
            return true;
        }

        return path.StartsWith("docs/", StringComparison.Ordinal) ||
            path.StartsWith("doc/", StringComparison.Ordinal) ||
            path.StartsWith("issues/", StringComparison.Ordinal) ||
            path.StartsWith("issue/", StringComparison.Ordinal) ||
            path.StartsWith("design/", StringComparison.Ordinal) ||
            path.StartsWith("designs/", StringComparison.Ordinal) ||
            path.StartsWith("audit/", StringComparison.Ordinal) ||
            path.StartsWith("audits/", StringComparison.Ordinal) ||
            path.StartsWith("roadmap/", StringComparison.Ordinal) ||
            path.StartsWith("roadmaps/", StringComparison.Ordinal) ||
            path.StartsWith("planning/", StringComparison.Ordinal) ||
            path.StartsWith("plans/", StringComparison.Ordinal) ||
            path.StartsWith("reports/", StringComparison.Ordinal) ||
            path.StartsWith("report/", StringComparison.Ordinal);
    }

    private static bool IsPromptResource(string path) =>
        path.EndsWith(".prompt", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/prompts/", StringComparison.OrdinalIgnoreCase);

    private static bool IsMigration(string path) =>
        path.Contains("/migrations/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("migrations/", StringComparison.OrdinalIgnoreCase);

    private static bool IsGeneratedSource(string path)
    {
        string fileName = Path.GetFileName(path);
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScript(string path, string extension)
    {
        string fileName = Path.GetFileName(path);
        return ScriptExtensions.Contains(extension) ||
            path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("build.sh", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("build.ps1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUiAsset(string path, string extension) =>
        UiAssetExtensions.Contains(extension) &&
        (path.Contains("/assets/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/wwwroot/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/public/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/static/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("public/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("static/", StringComparison.OrdinalIgnoreCase));

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim();
}
