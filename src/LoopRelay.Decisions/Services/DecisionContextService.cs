using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LoopRelay.Continuity.Models;
using LoopRelay.Continuity.Abstractions;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Persistence;

namespace LoopRelay.Decisions.Services;

public sealed partial class DecisionContextService(
    IRepositoryService repositoryService,
    IArtifactStore artifactStore,
    IDecisionRepository decisionRepository,
    IContinuityDiagnosticsService? continuityDiagnosticsService = null) : IDecisionContextService, IDecisionContextProjectionService
{
    private const string PlanPath = ".agents/plan.md";
    private const string MilestonesPath = ".agents/milestones";
    private const string OperationalContextPath = ".agents/operational_context.md";
    private const string DecisionsPath = ".agents/decisions";
    private const string HandoffsPath = ".agents/handoffs";
    private const string ContextsPath = ".agents/decisions/contexts";

    public async Task<DecisionContext> BuildContextAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        var items = new List<DecisionContextItem>();
        var diagnostics = new List<DecisionContextSourceDiagnostic>();
        var warnings = new List<string>();

        await LoadSingleMarkdownAsync(repository, items, diagnostics, "Plan", PlanPath, required: true);
        await LoadMilestonesAsync(repository, items, diagnostics);
        await LoadSingleMarkdownAsync(repository, items, diagnostics, "OperationalContext", OperationalContextPath, required: false);
        await LoadStructuredDecisionsAsync(repository, items, diagnostics);
        await LoadDecisionMarkdownFallbackAsync(repository, items, diagnostics);
        await LoadRecentHandoffsAsync(repository, items, diagnostics);
        await LoadContinuityDiagnosticsAsync(repository, items, diagnostics, warnings);

        DecisionContextValidationResult validation = Validate(diagnostics, warnings);
        IReadOnlyList<DecisionContextItem> orderedItems = items
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var contextDiagnostics = new DecisionContextDiagnostics(
            diagnostics
                .OrderBy(diagnostic => diagnostic.Name, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.RelativePath, StringComparer.Ordinal)
                .ToArray(),
            warnings.Order(StringComparer.Ordinal).ToArray());

        return new DecisionContext(
            repository.Id,
            FingerprintContext(orderedItems),
            orderedItems,
            contextDiagnostics,
            validation);
    }

    public async Task<DecisionContextSnapshot> CreateSnapshotAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionContext context = await BuildContextAsync(repositoryId);
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        string snapshotId = $"context.{createdAt:yyyyMMddHHmmssfffffff}";
        var snapshot = new DecisionContextSnapshot(
            snapshotId,
            repository.Id,
            createdAt,
            context.Fingerprint,
            context,
            context.Diagnostics,
            context.Validation);

        await WriteSnapshotAsync(repository, snapshot);
        return snapshot;
    }

    public async Task<IReadOnlyList<DecisionContextSnapshot>> ListSnapshotsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        string root = ArtifactPath.ResolveRepositoryPath(repository, ContextsPath);
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "context.*.json");
        var snapshots = new List<DecisionContextSnapshot>();
        foreach (string file in files.Order(StringComparer.Ordinal))
        {
            string? json = await artifactStore.ReadAsync(file);
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            DecisionArtifactDocument<DecisionContextSnapshot>? document =
                JsonSerializer.Deserialize<DecisionArtifactDocument<DecisionContextSnapshot>>(json, DecisionJson.Options);
            if (document is null)
            {
                continue;
            }

            if (!string.Equals(document.SchemaVersion, DecisionArtifactPaths.SchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported decision context snapshot schema version '{document.SchemaVersion}'.");
            }

            if (document.RepositoryId != repository.Id)
            {
                throw new InvalidOperationException("Decision context snapshot belongs to a different repository.");
            }

            snapshots.Add(document.Payload);
        }

        return snapshots
            .OrderBy(snapshot => snapshot.SnapshotId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionGenerationContext> BuildGenerationContextAsync(Guid repositoryId)
    {
        DecisionContext context = await BuildContextAsync(repositoryId);
        DecisionGenerationContextEntry[] goals = ProjectEntries(context, IsGoalLine, "goal").ToArray();
        DecisionGenerationContextEntry[] constraints = ProjectEntries(context, IsConstraintLine, "constraint").ToArray();
        DecisionGenerationContextEntry[] risks = ProjectEntries(context, IsRiskLine, "risk").ToArray();
        DecisionGenerationContextEntry[] questions = ProjectEntries(context, IsQuestionLine, "question").ToArray();
        DecisionGenerationContextEntry[] priorDecisions = ProjectPriorDecisions(context).ToArray();
        DecisionGenerationContextEntry[] repositoryState = ProjectRepositoryState(context).ToArray();
        DecisionGenerationContextEntry[] dependencies = ProjectEntries(context, IsDependencyLine, "dependency").ToArray();
        DecisionGenerationContextEntry[] handoffState = ProjectHandoffState(context).ToArray();
        string fingerprint = FingerprintContextProjection(
            context.Fingerprint,
            goals,
            constraints,
            risks,
            questions,
            priorDecisions,
            repositoryState,
            dependencies,
            handoffState);
        string[] diagnostics = [
            $"Projected decision generation context from {context.Items.Count} context items.",
            $"Goals: {goals.Length}; constraints: {constraints.Length}; risks: {risks.Length}; questions: {questions.Length}; prior decisions: {priorDecisions.Length}; repository state: {repositoryState.Length}; dependencies: {dependencies.Length}; handoff entries: {handoffState.Length}."
        ];

        return new DecisionGenerationContext(
            context.RepositoryId,
            fingerprint,
            goals,
            constraints,
            risks,
            questions,
            priorDecisions,
            repositoryState,
            dependencies,
            handoffState,
            diagnostics);
    }

    private async Task LoadSingleMarkdownAsync(
        Repository repository,
        List<DecisionContextItem> items,
        List<DecisionContextSourceDiagnostic> diagnostics,
        string name,
        string relativePath,
        bool required)
    {
        string? content = await ReadRelativeAsync(repository, relativePath);
        if (content is null)
        {
            diagnostics.Add(Missing(name, relativePath, required));
            return;
        }

        string fingerprint = Fingerprint(content);
        diagnostics.Add(Loaded(name, relativePath, required, content, fingerprint));
        items.Add(new DecisionContextItem(
            StableItemId(name, relativePath),
            name,
            TitleFromMarkdown(content, Path.GetFileNameWithoutExtension(relativePath)),
            NormalizeContent(content),
            required,
            fingerprint,
            [new DecisionSourceReference(name, relativePath, Excerpt: Excerpt(content))]));
    }

    private async Task LoadMilestonesAsync(
        Repository repository,
        List<DecisionContextItem> items,
        List<DecisionContextSourceDiagnostic> diagnostics)
    {
        string root = ArtifactPath.ResolveRepositoryPath(repository, MilestonesPath);
        IReadOnlyList<string> milestoneFiles = await artifactStore.ListAsync(root, "*.md");
        if (milestoneFiles.Count == 0)
        {
            diagnostics.Add(Missing("Milestones", MilestonesPath, required: true));
            return;
        }

        foreach (string file in milestoneFiles.Order(StringComparer.OrdinalIgnoreCase))
        {
            string relativePath = ArtifactPath.ToRepositoryRelativePath(repository, file);
            string content = await artifactStore.ReadAsync(file) ?? string.Empty;
            string fingerprint = Fingerprint(content);
            diagnostics.Add(Loaded("Milestone", relativePath, required: true, content, fingerprint));
            items.Add(new DecisionContextItem(
                StableItemId("Milestone", relativePath),
                "Milestone",
                TitleFromMarkdown(content, Path.GetFileNameWithoutExtension(relativePath)),
                NormalizeContent(content),
                Required: true,
                fingerprint,
                [new DecisionSourceReference("Milestone", relativePath, Excerpt: Excerpt(content))]));
        }
    }

    private async Task LoadStructuredDecisionsAsync(
        Repository repository,
        List<DecisionContextItem> items,
        List<DecisionContextSourceDiagnostic> diagnostics)
    {
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);

        if (decisions.Count + candidates.Count + proposals.Count == 0)
        {
            diagnostics.Add(new DecisionContextSourceDiagnostic(
                "StructuredDecisions",
                DecisionsPath,
                Required: false,
                DecisionContextSourceStatus.Missing,
                "No structured decision lifecycle artifacts found."));
            return;
        }

        foreach (Decision decision in decisions)
        {
            string relativePath = $".agents/decisions/records/{decision.Id.Value}/decision.json";
            string content = JsonSerializer.Serialize(decision, DecisionJson.Options);
            AddStructuredItem(items, diagnostics, "Decision", decision.Id.Value, decision.Title, relativePath, content);
        }

        foreach (DecisionCandidate candidate in candidates)
        {
            string relativePath = $".agents/decisions/candidates/{candidate.Id}/candidate.json";
            string content = JsonSerializer.Serialize(candidate, DecisionJson.Options);
            AddStructuredItem(items, diagnostics, "DecisionCandidate", candidate.Id, candidate.Title, relativePath, content);
        }

        foreach (DecisionProposal proposal in proposals)
        {
            string relativePath = $".agents/decisions/proposals/{proposal.Id}/proposal.json";
            string content = JsonSerializer.Serialize(proposal, DecisionJson.Options);
            AddStructuredItem(items, diagnostics, "DecisionProposal", proposal.Id, proposal.Title, relativePath, content);
        }
    }

    private static void AddStructuredItem(
        List<DecisionContextItem> items,
        List<DecisionContextSourceDiagnostic> diagnostics,
        string kind,
        string id,
        string title,
        string relativePath,
        string content)
    {
        string fingerprint = Fingerprint(content);
        diagnostics.Add(Loaded(kind, relativePath, required: false, content, fingerprint));
        items.Add(new DecisionContextItem(
            id,
            kind,
            title,
            NormalizeContent(content),
            Required: false,
            fingerprint,
            [new DecisionSourceReference(kind, relativePath)]));
    }

    private async Task LoadDecisionMarkdownFallbackAsync(
        Repository repository,
        List<DecisionContextItem> items,
        List<DecisionContextSourceDiagnostic> diagnostics)
    {
        bool hasStructuredDecisions = items.Any(item =>
            item.Kind is "Decision" or "DecisionCandidate" or "DecisionProposal");
        string? content = await ReadRelativeAsync(repository, ".agents/decisions/decisions.md");
        if (content is null)
        {
            diagnostics.Add(Missing("CurrentDecisionMarkdown", ".agents/decisions/decisions.md", required: false));
            return;
        }

        string fingerprint = Fingerprint(content);
        diagnostics.Add(Loaded("CurrentDecisionMarkdown", ".agents/decisions/decisions.md", required: false, content, fingerprint));
        if (hasStructuredDecisions)
        {
            return;
        }

        items.Add(new DecisionContextItem(
            StableItemId("CurrentDecisionMarkdown", ".agents/decisions/decisions.md"),
            "CurrentDecisionMarkdown",
            TitleFromMarkdown(content, "decisions"),
            NormalizeContent(content),
            Required: false,
            fingerprint,
            [new DecisionSourceReference("CurrentDecisionMarkdown", ".agents/decisions/decisions.md", Excerpt: Excerpt(content))]));
    }

    private async Task LoadRecentHandoffsAsync(
        Repository repository,
        List<DecisionContextItem> items,
        List<DecisionContextSourceDiagnostic> diagnostics)
    {
        string root = ArtifactPath.ResolveRepositoryPath(repository, HandoffsPath);
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "handoff*.md");
        string[] recent = files
            .OrderByDescending(file => HandoffSequence(file))
            .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (recent.Length == 0)
        {
            diagnostics.Add(Missing("RecentHandoffs", HandoffsPath, required: false));
            return;
        }

        foreach (string file in recent)
        {
            string relativePath = ArtifactPath.ToRepositoryRelativePath(repository, file);
            string content = await artifactStore.ReadAsync(file) ?? string.Empty;
            string fingerprint = Fingerprint(content);
            diagnostics.Add(Loaded("RecentHandoff", relativePath, required: false, content, fingerprint));
            items.Add(new DecisionContextItem(
                StableItemId("RecentHandoff", relativePath),
                "RecentHandoff",
                TitleFromMarkdown(content, Path.GetFileNameWithoutExtension(relativePath)),
                NormalizeContent(content),
                Required: false,
                fingerprint,
                [new DecisionSourceReference("RecentHandoff", relativePath, Excerpt: Excerpt(content))]));
        }
    }

    private async Task LoadContinuityDiagnosticsAsync(
        Repository repository,
        List<DecisionContextItem> items,
        List<DecisionContextSourceDiagnostic> diagnostics,
        List<string> warnings)
    {
        if (continuityDiagnosticsService is null)
        {
            diagnostics.Add(new DecisionContextSourceDiagnostic(
                "ContinuityDiagnostics",
                ".agents/operational_context.md",
                Required: false,
                DecisionContextSourceStatus.Missing,
                "Continuity diagnostics service is not available."));
            return;
        }

        try
        {
            ContinuityDiagnostics continuityDiagnostics = await continuityDiagnosticsService.GetDiagnosticsAsync(repository.Id);
            object stableDiagnostics = new
            {
                continuityDiagnostics.RevisionCount,
                continuityDiagnostics.CurrentContextByteCount,
                continuityDiagnostics.CurrentContextCharacterCount,
                continuityDiagnostics.ContextByteGrowth,
                continuityDiagnostics.AverageBytesPerRevision,
                continuityDiagnostics.ArchitectureTrend,
                continuityDiagnostics.ConstraintTrend,
                continuityDiagnostics.DecisionTrend,
                continuityDiagnostics.RationaleTrend,
                continuityDiagnostics.OpenQuestionTrend,
                continuityDiagnostics.ActiveRiskTrend,
                continuityDiagnostics.CompressionTrend,
                continuityDiagnostics.RepeatedInvestigationIndicators,
                continuityDiagnostics.RepeatedQuestionIndicators,
                continuityDiagnostics.DecisionReworkIndicators,
                continuityDiagnostics.ContinuityWarnings
            };
            string content = JsonSerializer.Serialize(stableDiagnostics, DecisionJson.Options);
            string fingerprint = Fingerprint(content);
            diagnostics.Add(Loaded("ContinuityDiagnostics", ".agents/operational_context.md", required: false, content, fingerprint));
            items.Add(new DecisionContextItem(
                "continuity-diagnostics",
                "ContinuityDiagnostics",
                "Continuity diagnostics",
                NormalizeContent(content),
                Required: false,
                fingerprint,
                [new DecisionSourceReference("ContinuityDiagnostics", ".agents/operational_context.md")]));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Continuity diagnostics omitted: {exception.Message}");
            diagnostics.Add(new DecisionContextSourceDiagnostic(
                "ContinuityDiagnostics",
                ".agents/operational_context.md",
                Required: false,
                DecisionContextSourceStatus.Warning,
                exception.Message));
        }
    }

    private async Task WriteSnapshotAsync(Repository repository, DecisionContextSnapshot snapshot)
    {
        var document = new DecisionArtifactDocument<DecisionContextSnapshot>(
            DecisionArtifactPaths.SchemaVersion,
            repository.Id,
            snapshot.CreatedAt,
            snapshot.CreatedAt,
            snapshot);
        string relativePath = ArtifactPath.CombineRelative(ContextsPath, $"{snapshot.SnapshotId}.json");
        await artifactStore.WriteAsync(
            ArtifactPath.ResolveRepositoryPath(repository, relativePath),
            JsonSerializer.Serialize(document, DecisionJson.Options));
    }

    private async Task<string?> ReadRelativeAsync(Repository repository, string relativePath)
    {
        return await artifactStore.ReadAsync(ArtifactPath.ResolveRepositoryPath(repository, relativePath));
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static DecisionContextValidationResult Validate(
        IReadOnlyList<DecisionContextSourceDiagnostic> diagnostics,
        IReadOnlyList<string> warnings)
    {
        string[] errors = diagnostics
            .Where(diagnostic => diagnostic.Required && diagnostic.Status == DecisionContextSourceStatus.Missing)
            .Select(diagnostic => $"Required decision context source is missing: {diagnostic.RelativePath}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] validationWarnings = diagnostics
            .Where(diagnostic => !diagnostic.Required && diagnostic.Status != DecisionContextSourceStatus.Loaded)
            .Select(diagnostic => diagnostic.Message is null
                ? $"Optional decision context source omitted: {diagnostic.RelativePath}"
                : $"Optional decision context source omitted: {diagnostic.RelativePath} ({diagnostic.Message})")
            .Concat(warnings)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new DecisionContextValidationResult(errors.Length == 0, errors, validationWarnings);
    }

    private static DecisionContextSourceDiagnostic Loaded(
        string name,
        string relativePath,
        bool required,
        string content,
        string fingerprint)
    {
        return new DecisionContextSourceDiagnostic(
            name,
            relativePath,
            required,
            DecisionContextSourceStatus.Loaded,
            ByteCount: Encoding.UTF8.GetByteCount(content),
            CharacterCount: content.Length,
            Fingerprint: fingerprint);
    }

    private static DecisionContextSourceDiagnostic Missing(string name, string relativePath, bool required)
    {
        return new DecisionContextSourceDiagnostic(
            name,
            relativePath,
            required,
            DecisionContextSourceStatus.Missing,
            required ? "Required source is missing." : "Optional source is not present.");
    }

    private static string FingerprintContext(IReadOnlyList<DecisionContextItem> items)
    {
        var builder = new StringBuilder();
        foreach (DecisionContextItem item in items)
        {
            builder
                .Append(item.Kind).Append('\n')
                .Append(item.Id).Append('\n')
                .Append(item.Title).Append('\n')
                .Append(item.Required).Append('\n')
                .Append(item.Fingerprint).Append('\n');
            foreach (DecisionSourceReference source in item.Sources.OrderBy(source => source.RelativePath, StringComparer.Ordinal))
            {
                builder
                    .Append(source.SourceKind).Append('|')
                    .Append(source.RelativePath).Append('|')
                    .Append(source.Section).Append('|')
                    .Append(source.ItemId).Append('|')
                    .Append(source.DecisionId).Append('|')
                    .Append(source.ProposalId).Append('|')
                    .Append(source.CandidateId).Append('\n');
            }
        }

        return Fingerprint(builder.ToString());
    }

    private static IEnumerable<DecisionGenerationContextEntry> ProjectEntries(
        DecisionContext context,
        Func<string, bool> predicate,
        string category)
    {
        foreach (DecisionContextItem item in context.Items.OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal))
        {
            string[] matches = ExtractContextLines(item.Content)
                .Where(predicate)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal)
                .Take(6)
                .ToArray();
            for (int index = 0; index < matches.Length; index++)
            {
                yield return new DecisionGenerationContextEntry(
                    StableItemId($"{category}-{index + 1}", $"{item.Id}-{matches[index]}"),
                    matches[index],
                    EvidenceForContextItem(item, matches[index]));
            }
        }
    }

    private static IEnumerable<DecisionGenerationContextEntry> ProjectPriorDecisions(DecisionContext context)
    {
        foreach (DecisionContextItem item in context.Items.Where(item =>
            item.Kind is "Decision" or "DecisionProposal" or "CurrentDecisionMarkdown").OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            string statement = item.Kind == "CurrentDecisionMarkdown"
                ? FirstContextLine(item.Content)
                : item.Title;
            if (string.IsNullOrWhiteSpace(statement))
            {
                continue;
            }

            yield return new DecisionGenerationContextEntry(
                StableItemId("prior-decision", $"{item.Kind}-{item.Id}"),
                statement,
                EvidenceForContextItem(item, statement));
        }
    }

    private static IEnumerable<DecisionGenerationContextEntry> ProjectRepositoryState(DecisionContext context)
    {
        foreach (DecisionContextItem item in context.Items.Where(item =>
            item.Kind is "Plan" or "Milestone" or "OperationalContext" or "ContinuityDiagnostics").OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal))
        {
            string statement = item.Kind == "ContinuityDiagnostics"
                ? "Continuity diagnostics are available for repository-state analysis."
                : item.Title;
            yield return new DecisionGenerationContextEntry(
                StableItemId("repository-state", $"{item.Kind}-{item.Id}"),
                statement,
                EvidenceForContextItem(item, statement));
        }
    }

    private static IEnumerable<DecisionGenerationContextEntry> ProjectHandoffState(DecisionContext context)
    {
        foreach (DecisionContextItem item in context.Items.Where(item => item.Kind == "RecentHandoff").OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            foreach (string line in ExtractContextLines(item.Content).Take(8))
            {
                yield return new DecisionGenerationContextEntry(
                    StableItemId("handoff", $"{item.Id}-{line}"),
                    line,
                    EvidenceForContextItem(item, line));
            }
        }
    }

    private static DecisionEvidence[] EvidenceForContextItem(DecisionContextItem item, string statement)
    {
        return [
            new DecisionEvidence(
                statement,
                item.Sources.Count == 0
                    ? [new DecisionSourceReference(item.Kind, ItemId: item.Id, Excerpt: statement)]
                    : item.Sources
                        .Select(source => source with { Excerpt = string.IsNullOrWhiteSpace(source.Excerpt) ? statement : source.Excerpt })
                        .ToArray())
        ];
    }

    private static IEnumerable<string> ExtractContextLines(string content)
    {
        return NormalizeContent(content)
            .Split('\n')
            .Select(line => line.Trim().TrimStart('-', '*').Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
            .Select(line => line.Length <= 240 ? line : $"{line[..237]}...");
    }

    private static string FirstContextLine(string content)
    {
        return ExtractContextLines(content).FirstOrDefault() ?? string.Empty;
    }

    private static bool IsGoalLine(string line)
    {
        return ContainsAny(line, "goal", "objective", "milestone", "delivery target", "success", "exit state");
    }

    private static bool IsConstraintLine(string line)
    {
        return ContainsAny(line, "constraint", "must", "must not", "do not", "rule", "non-goal", "non-goals", "boundary");
    }

    private static bool IsRiskLine(string line)
    {
        return ContainsAny(line, "risk", "blocker", "blocking", "unknown", "warning", "conflict", "contradiction", "stale");
    }

    private static bool IsQuestionLine(string line)
    {
        return line.Contains('?') || ContainsAny(line, "question", "open issue", "unresolved");
    }

    private static bool IsDependencyLine(string line)
    {
        return ContainsAny(line, "dependency", "depends", "requires", "prerequisite", "sequencing");
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string FingerprintContextProjection(
        string contextFingerprint,
        params IReadOnlyList<DecisionGenerationContextEntry>[] entryGroups)
    {
        var builder = new StringBuilder();
        builder.Append(contextFingerprint).Append('\n');
        foreach (IReadOnlyList<DecisionGenerationContextEntry> entries in entryGroups)
        {
            foreach (DecisionGenerationContextEntry entry in entries.OrderBy(entry => entry.Id, StringComparer.Ordinal))
            {
                builder.Append(entry.Id).Append('|').Append(entry.Statement).Append('\n');
            }
        }

        return Fingerprint(builder.ToString());
    }

    private static string Fingerprint(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeContent(content)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeContent(string content)
    {
        return content.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    private static string StableItemId(string kind, string relativePath)
    {
        string slug = SlugPattern().Replace($"{kind}-{relativePath}".ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? kind.ToLowerInvariant() : slug;
    }

    private static string TitleFromMarkdown(string content, string fallback)
    {
        string? title = NormalizeContent(content)
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(title) ? fallback : title[2..].Trim();
    }

    private static string Excerpt(string content)
    {
        return NormalizeContent(content)
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? string.Empty;
    }

    private static int HandoffSequence(string path)
    {
        string fileName = Path.GetFileName(path);
        if (string.Equals(fileName, "handoff.md", StringComparison.OrdinalIgnoreCase))
        {
            return int.MaxValue;
        }

        Match match = HandoffPattern().Match(fileName);
        return match.Success && int.TryParse(match.Groups[1].Value, out int sequence) ? sequence : 0;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^handoff\\.([0-9]{4})\\.md$", RegexOptions.IgnoreCase)]
    private static partial Regex HandoffPattern();
}
