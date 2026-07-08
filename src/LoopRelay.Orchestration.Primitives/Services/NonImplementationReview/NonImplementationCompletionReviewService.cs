using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public interface INonImplementationCompletionReviewService
{
    Task<NonImplementationCompletionReviewResult> ReviewAsync(
        CancellationToken cancellationToken = default);
}

public sealed class NonImplementationCompletionReviewService(
    RepositoryChangeSetDetector detector,
    NonImplementationArtifactClassifier classifier,
    NonImplementationSemanticConfirmer semanticConfirmer,
    NonImplementationReviewLedgerStore ledgerStore,
    IArtifactStore artifacts,
    string repositoryRootPath,
    NonImplementationInsightSynthesizer? synthesizer = null) : INonImplementationCompletionReviewService
{
    private const string DecisionArtifactPath = OrchestrationArtifactPaths.NonImplementationDecisions;
    private const string ReviewArtifactPath = OrchestrationArtifactPaths.NonImplementationReview;
    private const string SynthesisArtifactPath = OrchestrationArtifactPaths.NonImplementationSynthesis;

    public async Task<NonImplementationCompletionReviewResult> ReviewAsync(
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        cancellationToken.ThrowIfCancellationRequested();

        CompletionRefresh refresh = await RefreshCurrentRepositoryStateAsync(cancellationToken);
        if (synthesizer is not null)
        {
            await synthesizer.SynthesizeAsync(cancellationToken);
        }

        NonImplementationReviewLedgerDocument document = await ledgerStore.LoadOrCreateAsync();
        IReadOnlyList<NonImplementationReviewLedgerEntry> unresolved = BlockingEntries(document);
        bool synthesisDecisionRequired =
            unresolved.Count > 0 &&
            !string.IsNullOrWhiteSpace(await artifacts.ReadAsync(SynthesisArtifactPath));

        if (unresolved.Count == 0)
        {
            NonImplementationCompletionReviewSummary readySummary =
                BuildSummary(refresh, unresolved, resolvedFileDecisionCount: 0, appliedDeleteCount: 0,
                    synthesisDecisionRequired, document.SynthesisDecision?.Decision);
            await artifacts.WriteAsync(
                ReviewArtifactPath,
                RenderReviewEvidence(
                    NonImplementationCompletionReviewStatus.Ready,
                    refresh,
                    document,
                    unresolved,
                    readySummary,
                    blockerMessages: []));
            return new NonImplementationCompletionReviewResult(
                NonImplementationCompletionReviewStatus.Ready,
                ReviewArtifactPath,
                DecisionTemplatePath: null,
                EvidencePaths: [ReviewArtifactPath, OrchestrationArtifactPaths.NonImplementationLedger],
                AppliedDeletePaths: [],
                readySummary,
                BlockerMessages: []);
        }

        string? decisionContent = await artifacts.ReadAsync(DecisionArtifactPath);
        if (string.IsNullOrWhiteSpace(decisionContent))
        {
            await artifacts.WriteAsync(
                DecisionArtifactPath,
                RenderDecisionTemplate(unresolved, synthesisDecisionRequired));

            NonImplementationCompletionReviewSummary blockedSummary =
                BuildSummary(refresh, unresolved, resolvedFileDecisionCount: 0, appliedDeleteCount: 0,
                    synthesisDecisionRequired, document.SynthesisDecision?.Decision);
            IReadOnlyList<string> blockers =
            [
                $"Human decisions are required in `{DecisionArtifactPath}` before completion evaluation can proceed.",
            ];
            await artifacts.WriteAsync(
                ReviewArtifactPath,
                RenderReviewEvidence(
                    NonImplementationCompletionReviewStatus.Blocked,
                    refresh,
                    document,
                    unresolved,
                    blockedSummary,
                    blockers));
            return new NonImplementationCompletionReviewResult(
                NonImplementationCompletionReviewStatus.Blocked,
                ReviewArtifactPath,
                DecisionArtifactPath,
                EvidencePaths: [ReviewArtifactPath, DecisionArtifactPath, OrchestrationArtifactPaths.NonImplementationLedger],
                AppliedDeletePaths: [],
                blockedSummary,
                blockers);
        }

        string decisionSourceHash = Sha256Text(decisionContent);
        DecisionParseResult parsed = ParseDecisions(decisionContent, unresolved, synthesisDecisionRequired);
        if (parsed.Errors.Count > 0)
        {
            NonImplementationCompletionReviewSummary blockedSummary =
                BuildSummary(refresh, unresolved, resolvedFileDecisionCount: 0, appliedDeleteCount: 0,
                    synthesisDecisionRequired, document.SynthesisDecision?.Decision);
            await artifacts.WriteAsync(
                ReviewArtifactPath,
                RenderReviewEvidence(
                    NonImplementationCompletionReviewStatus.Blocked,
                    refresh,
                    document,
                    unresolved,
                    blockedSummary,
                    parsed.Errors));
            return new NonImplementationCompletionReviewResult(
                NonImplementationCompletionReviewStatus.Blocked,
                ReviewArtifactPath,
                DecisionArtifactPath,
                EvidencePaths: [ReviewArtifactPath, DecisionArtifactPath, OrchestrationArtifactPaths.NonImplementationLedger],
                AppliedDeletePaths: [],
                blockedSummary,
                parsed.Errors);
        }

        IReadOnlyList<string> deleteValidationErrors =
            await ValidateDeleteDecisionsAsync(parsed.FileDecisions, unresolved, cancellationToken);
        if (deleteValidationErrors.Count > 0)
        {
            NonImplementationCompletionReviewSummary blockedSummary =
                BuildSummary(refresh, unresolved, resolvedFileDecisionCount: 0, appliedDeleteCount: 0,
                    synthesisDecisionRequired, document.SynthesisDecision?.Decision);
            await artifacts.WriteAsync(
                ReviewArtifactPath,
                RenderReviewEvidence(
                    NonImplementationCompletionReviewStatus.Blocked,
                    refresh,
                    document,
                    unresolved,
                    blockedSummary,
                    deleteValidationErrors));
            return new NonImplementationCompletionReviewResult(
                NonImplementationCompletionReviewStatus.Blocked,
                ReviewArtifactPath,
                DecisionArtifactPath,
                EvidencePaths: [ReviewArtifactPath, DecisionArtifactPath, OrchestrationArtifactPaths.NonImplementationLedger],
                AppliedDeletePaths: [],
                blockedSummary,
                deleteValidationErrors);
        }

        IReadOnlyList<string> appliedDeletes = await ApplyDeleteDecisionsAsync(
            parsed.FileDecisions,
            cancellationToken);
        NonImplementationReviewLedgerDocument updatedDocument = await RecordDecisionsAsync(
            parsed,
            decisionSourceHash,
            cancellationToken);
        IReadOnlyList<NonImplementationReviewLedgerEntry> remainingUnresolved = BlockingEntries(updatedDocument);
        NonImplementationCompletionReviewStatus status = remainingUnresolved.Count == 0
            ? NonImplementationCompletionReviewStatus.Ready
            : NonImplementationCompletionReviewStatus.Blocked;
        IReadOnlyList<string> remainingBlockers = remainingUnresolved.Count == 0
            ? []
            : remainingUnresolved
                .Select(entry => $"Ledger entry `{entry.EntryId}` remains unresolved after decision application.")
                .ToArray();
        NonImplementationCompletionReviewSummary summary =
            BuildSummary(refresh, remainingUnresolved, parsed.FileDecisions.Count, appliedDeletes.Count,
                synthesisDecisionRequired, parsed.SynthesisDecision?.Decision ?? updatedDocument.SynthesisDecision?.Decision);
        await artifacts.WriteAsync(
            ReviewArtifactPath,
            RenderReviewEvidence(
                status,
                refresh,
                updatedDocument,
                remainingUnresolved,
                summary,
                remainingBlockers));

        return new NonImplementationCompletionReviewResult(
            status,
            ReviewArtifactPath,
            DecisionArtifactPath,
            EvidencePaths: [ReviewArtifactPath, DecisionArtifactPath, OrchestrationArtifactPaths.NonImplementationLedger],
            AppliedDeletePaths: appliedDeletes,
            summary,
            remainingBlockers);
    }

    private async Task<CompletionRefresh> RefreshCurrentRepositoryStateAsync(
        CancellationToken cancellationToken)
    {
        string refreshId = $"completion-review-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        RepositorySliceSnapshot snapshot = await detector.CaptureSnapshotAsync(refreshId);
        cancellationToken.ThrowIfCancellationRequested();
        RepositorySliceDelta delta = new(
            refreshId,
            snapshot.Files
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .Select(file => new RepositoryChangedFileFacts(
                    refreshId,
                    file.Path,
                    file.PreviousPath,
                    file.Status,
                    BaselineStatus: null,
                    PostStatus: file.Status,
                    PreExisted: !IsUntracked(file.Status),
                    file.Exists,
                    file.IsDeleted,
                    file.Extension,
                    file.Size,
                    BaselineContentSha256: null,
                    file.ContentSha256,
                    file.TrackedDiffMetadata))
                .ToArray());

        NonImplementationArtifactClassificationSet classificationSet =
            await classifier.ClassifyAsync(delta);
        NonImplementationSemanticConfirmationBatchResult semantic =
            await semanticConfirmer.ConfirmAsync(
                classificationSet,
                DateTimeOffset.UtcNow,
                discoveryContext: $"epic-completion fresh review {refreshId}",
                cancellationToken);

        return new CompletionRefresh(refreshId, snapshot, delta, classificationSet, semantic);
    }

    private static IReadOnlyList<NonImplementationReviewLedgerEntry> BlockingEntries(
        NonImplementationReviewLedgerDocument document) =>
        document.Entries
            .Where(entry =>
                entry.ResolutionState == NonImplementationResolutionState.Unresolved &&
                entry.SemanticDisposition is NonImplementationSemanticDisposition.ConfirmedNonImplementation
                    or NonImplementationSemanticDisposition.Uncertain)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
            .ToArray();

    private static NonImplementationCompletionReviewSummary BuildSummary(
        CompletionRefresh refresh,
        IReadOnlyList<NonImplementationReviewLedgerEntry> unresolved,
        int resolvedFileDecisionCount,
        int appliedDeleteCount,
        bool synthesisDecisionRequired,
        NonImplementationSynthesisDecision? synthesisDecision) =>
        new(
            refresh.RefreshId,
            refresh.Delta.ChangedFiles.Count,
            refresh.Classifications.Classifications.Count,
            refresh.Classifications.Classifications.Count(classification =>
                NonImplementationSemanticConfirmer.RequiresSemanticConfirmation(classification.Route)),
            refresh.Semantic.ConfirmedCount,
            refresh.Semantic.SkippedCount,
            unresolved.Count(entry =>
                entry.SemanticDisposition == NonImplementationSemanticDisposition.ConfirmedNonImplementation),
            unresolved.Count(entry =>
                entry.SemanticDisposition == NonImplementationSemanticDisposition.Uncertain),
            resolvedFileDecisionCount,
            appliedDeleteCount,
            synthesisDecisionRequired,
            synthesisDecision);

    private static DecisionParseResult ParseDecisions(
        string content,
        IReadOnlyList<NonImplementationReviewLedgerEntry> unresolved,
        bool synthesisDecisionRequired)
    {
        var errors = new List<string>();
        var decisions = new List<FileDecisionRecord>();
        Dictionary<string, NonImplementationReviewLedgerEntry> unresolvedById =
            unresolved.ToDictionary(entry => entry.EntryId, StringComparer.Ordinal);

        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = ParseMarkdownTable(
            content,
            ["Entry ID", "Path", "Reviewed SHA-256", "Reviewed Status", "Decision", "HITL Reason"]);
        if (rows.Count == 0)
        {
            errors.Add($"Decision file must contain the file decision table rendered by `{DecisionArtifactPath}`.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (IReadOnlyDictionary<string, string> row in rows)
        {
            string entryId = Field(row, "Entry ID");
            if (string.IsNullOrWhiteSpace(entryId))
            {
                errors.Add("A file decision row is missing `Entry ID`.");
                continue;
            }

            if (!seen.Add(entryId))
            {
                errors.Add($"Decision file contains duplicate entry ID `{entryId}`.");
                continue;
            }

            string rawDecision = Field(row, "Decision");
            if (!unresolvedById.TryGetValue(entryId, out NonImplementationReviewLedgerEntry? entry))
            {
                if (!string.IsNullOrWhiteSpace(rawDecision))
                {
                    errors.Add($"Decision row `{entryId}` is no longer unresolved but still has decision `{rawDecision}`.");
                }

                continue;
            }

            ValidateDecisionTarget(row, entry, errors);
            if (string.IsNullOrWhiteSpace(rawDecision))
            {
                errors.Add($"Decision row `{entryId}` is missing a decision. Use Keep, Delete, ResolveFalsePositive, or Defer.");
                continue;
            }

            if (!Enum.TryParse(rawDecision, ignoreCase: false, out FileReviewDecision decision))
            {
                errors.Add($"Decision row `{entryId}` uses unknown decision `{rawDecision}`.");
                continue;
            }

            decisions.Add(new FileDecisionRecord(entry.EntryId, decision, Field(row, "HITL Reason")));
        }

        foreach (NonImplementationReviewLedgerEntry entry in unresolved)
        {
            if (!seen.Contains(entry.EntryId))
            {
                errors.Add($"Decision file is missing required row for unresolved entry `{entry.EntryId}`.");
            }
        }

        SynthesisDecisionRecord? synthesisDecision = ParseSynthesisDecision(
            content,
            synthesisDecisionRequired,
            errors);
        return new DecisionParseResult(decisions, synthesisDecision, errors);
    }

    private static void ValidateDecisionTarget(
        IReadOnlyDictionary<string, string> row,
        NonImplementationReviewLedgerEntry entry,
        List<string> errors)
    {
        string reviewedHash = entry.ReviewedFileDeleted
            ? NonImplementationReviewLedgerStore.DeletedReviewedIdentity(entry)
            : entry.ReviewedContentSha256 ?? string.Empty;
        string reviewedStatus = entry.SemanticDisposition?.ToString() ?? string.Empty;

        if (!string.Equals(NormalizePath(Field(row, "Path")), entry.Path, StringComparison.Ordinal))
        {
            errors.Add($"Decision row `{entry.EntryId}` path does not match reviewed path `{entry.Path}`.");
        }

        if (!string.Equals(Field(row, "Reviewed SHA-256"), reviewedHash, StringComparison.Ordinal))
        {
            errors.Add($"Decision row `{entry.EntryId}` reviewed hash does not match ledger identity.");
        }

        if (!string.Equals(Field(row, "Reviewed Status"), reviewedStatus, StringComparison.Ordinal))
        {
            errors.Add($"Decision row `{entry.EntryId}` reviewed status does not match ledger status `{reviewedStatus}`.");
        }
    }

    private static SynthesisDecisionRecord? ParseSynthesisDecision(
        string content,
        bool synthesisDecisionRequired,
        List<string> errors)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = ParseMarkdownTable(
            content,
            ["Synthesis Path", "Decision"]);
        if (rows.Count == 0)
        {
            if (synthesisDecisionRequired)
            {
                errors.Add("Decision file is missing the synthesis decision table.");
            }

            return null;
        }

        IReadOnlyDictionary<string, string> row = rows[0];
        if (rows.Count > 1)
        {
            errors.Add("Synthesis decision table must contain exactly one row.");
        }

        string path = NormalizePath(Field(row, "Synthesis Path"));
        string rawDecision = Field(row, "Decision");
        string rationale = Field(row, "HITL Reason");
        if (!string.Equals(path, SynthesisArtifactPath, StringComparison.Ordinal))
        {
            errors.Add($"Synthesis decision row path must be `{SynthesisArtifactPath}`.");
        }

        if (string.IsNullOrWhiteSpace(rawDecision))
        {
            if (synthesisDecisionRequired)
            {
                errors.Add("Synthesis decision row is missing a decision. Use KeepSynthesis, DiscardSynthesis, or DeferSynthesis.");
            }

            return null;
        }

        if (!synthesisDecisionRequired)
        {
            errors.Add("Synthesis decision was supplied, but no current synthesis review artifact exists.");
            return null;
        }

        if (!Enum.TryParse(rawDecision, ignoreCase: false, out NonImplementationSynthesisDecision decision))
        {
            errors.Add($"Synthesis decision row uses unknown decision `{rawDecision}`.");
            return null;
        }

        return new SynthesisDecisionRecord(decision, rationale);
    }

    private async Task<IReadOnlyList<string>> ValidateDeleteDecisionsAsync(
        IReadOnlyList<FileDecisionRecord> decisions,
        IReadOnlyList<NonImplementationReviewLedgerEntry> unresolved,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        Dictionary<string, NonImplementationReviewLedgerEntry> entries =
            unresolved.ToDictionary(entry => entry.EntryId, StringComparer.Ordinal);
        foreach (FileDecisionRecord decision in decisions.Where(item => item.Decision == FileReviewDecision.Delete))
        {
            cancellationToken.ThrowIfCancellationRequested();
            NonImplementationReviewLedgerEntry entry = entries[decision.EntryId];
            if (entry.ReviewedFileDeleted)
            {
                errors.Add($"Delete decision for `{entry.EntryId}` is stale because the reviewed file was already deleted.");
                continue;
            }

            string? pathError = ValidateRepositoryDeletePath(entry.Path);
            if (pathError is not null)
            {
                errors.Add($"Delete decision for `{entry.EntryId}` is invalid: {pathError}");
                continue;
            }

            string? currentHash = await CurrentFileSha256Async(entry.Path);
            if (currentHash is null)
            {
                errors.Add($"Delete decision for `{entry.EntryId}` is stale because `{entry.Path}` is missing.");
                continue;
            }

            if (!string.Equals(currentHash, entry.ReviewedContentSha256, StringComparison.Ordinal))
            {
                errors.Add($"Delete decision for `{entry.EntryId}` is stale because `{entry.Path}` hash changed after review.");
            }
        }

        return errors;
    }

    private async Task<IReadOnlyList<string>> ApplyDeleteDecisionsAsync(
        IReadOnlyList<FileDecisionRecord> decisions,
        CancellationToken cancellationToken)
    {
        var deleted = new List<string>();
        foreach (FileDecisionRecord decision in decisions.Where(item => item.Decision == FileReviewDecision.Delete))
        {
            cancellationToken.ThrowIfCancellationRequested();
            NonImplementationReviewLedgerDocument document = await ledgerStore.LoadOrCreateAsync();
            NonImplementationReviewLedgerEntry entry = document.Entries.First(item =>
                string.Equals(item.EntryId, decision.EntryId, StringComparison.Ordinal));
            await artifacts.DeleteAsync(entry.Path);
            deleted.Add(entry.Path);
        }

        return deleted;
    }

    private async Task<NonImplementationReviewLedgerDocument> RecordDecisionsAsync(
        DecisionParseResult parsed,
        string decisionSourceHash,
        CancellationToken cancellationToken)
    {
        NonImplementationReviewLedgerDocument document = await ledgerStore.LoadOrCreateAsync();
        var entries = document.Entries.ToList();
        DateTimeOffset decidedAtUtc = DateTimeOffset.UtcNow;

        foreach (FileDecisionRecord decision in parsed.FileDecisions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = entries.FindIndex(entry =>
                string.Equals(entry.EntryId, decision.EntryId, StringComparison.Ordinal));
            if (index < 0)
            {
                continue;
            }

            NonImplementationReviewLedgerEntry entry = entries[index];
            NonImplementationResolutionState resolution = decision.Decision switch
            {
                FileReviewDecision.Keep => NonImplementationResolutionState.HitlKept,
                FileReviewDecision.Delete => NonImplementationResolutionState.HitlDeleted,
                FileReviewDecision.ResolveFalsePositive => NonImplementationResolutionState.HitlFalsePositive,
                FileReviewDecision.Defer => NonImplementationResolutionState.HitlDeferred,
                _ => throw new InvalidOperationException($"Unsupported file decision {decision.Decision}."),
            };
            NonImplementationHitlProvenanceKind provenanceKind =
                DetermineHitlProvenance(entry, decision);

            entries[index] = entry with
            {
                ResolutionState = resolution,
                HumanDecision = new NonImplementationHumanDecisionMetadata(
                    resolution,
                    DecisionArtifactPath,
                    decisionSourceHash,
                    decidedAtUtc,
                    Rationale: NullIfWhiteSpace(decision.HitlReason),
                    DecidedBy: "human",
                    ReviewedContentSha256: entry.ReviewedContentSha256,
                    ReviewedDeletedIdentity: entry.ReviewedFileDeleted
                        ? NonImplementationReviewLedgerStore.DeletedReviewedIdentity(entry)
                        : null),
                HitlProvenanceKind = provenanceKind,
                HitlProvenanceEvidencePath = provenanceKind == NonImplementationHitlProvenanceKind.None
                    ? entry.HitlProvenanceEvidencePath
                    : DecisionArtifactPath,
                HitlProvenanceSourceHash = provenanceKind == NonImplementationHitlProvenanceKind.None
                    ? entry.HitlProvenanceSourceHash
                    : decisionSourceHash,
                HitlProvenanceEvidenceExcerpt = provenanceKind == NonImplementationHitlProvenanceKind.None
                    ? entry.HitlProvenanceEvidenceExcerpt
                    : NullIfWhiteSpace(decision.HitlReason),
                HitlProvenanceRationale = provenanceKind == NonImplementationHitlProvenanceKind.None
                    ? entry.HitlProvenanceRationale
                    : NullIfWhiteSpace(decision.HitlReason),
            };
        }

        NonImplementationReviewLedgerDocument updated = document with
        {
            Entries = entries,
            SynthesisDecision = parsed.SynthesisDecision is null
                ? document.SynthesisDecision
                : new NonImplementationSynthesisDecisionMetadata(
                    parsed.SynthesisDecision.Decision,
                    SynthesisArtifactPath,
                    DecisionArtifactPath,
                    decisionSourceHash,
                    decidedAtUtc,
                    Rationale: NullIfWhiteSpace(parsed.SynthesisDecision.HitlReason),
                    DecidedBy: "human"),
        };
        await ledgerStore.SaveAsync(updated);
        return await ledgerStore.LoadOrCreateAsync();
    }

    private static NonImplementationHitlProvenanceKind DetermineHitlProvenance(
        NonImplementationReviewLedgerEntry entry,
        FileDecisionRecord decision)
    {
        if (decision.Decision != FileReviewDecision.Keep)
        {
            return entry.HitlProvenanceKind;
        }

        if (entry.HitlProvenanceKind == NonImplementationHitlProvenanceKind.HitlRequested ||
            decision.HitlReason.Contains("HitlRequested", StringComparison.OrdinalIgnoreCase) ||
            decision.HitlReason.Contains("requested", StringComparison.OrdinalIgnoreCase))
        {
            return NonImplementationHitlProvenanceKind.HitlRequested;
        }

        return NonImplementationHitlProvenanceKind.HitlKept;
    }

    private string? ValidateRepositoryDeletePath(string path)
    {
        string normalized = NormalizePath(path);
        if (Path.IsPathRooted(normalized))
        {
            return "delete path must be repository-relative.";
        }

        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            return "delete path must not contain path traversal segments.";
        }

        if (OrchestrationArtifactPaths.IsAgentsPath(normalized))
        {
            return "delete path must not be under .agents.";
        }

        string root = Path.GetFullPath(repositoryRootPath);
        string fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        string comparisonRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(comparisonRoot, StringComparison.OrdinalIgnoreCase))
        {
            return "delete path must resolve inside the repository.";
        }

        return null;
    }

    private async Task<string?> CurrentFileSha256Async(string path)
    {
        string normalized = NormalizePath(path);
        string fullPath = Path.GetFullPath(
            Path.Combine(repositoryRootPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (File.Exists(fullPath))
        {
            await using FileStream stream = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            byte[] hash = await SHA256.HashDataAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        string? content = await artifacts.ReadAsync(normalized);
        return content is null ? null : Sha256Text(content);
    }

    private static string RenderDecisionTemplate(
        IReadOnlyList<NonImplementationReviewLedgerEntry> unresolved,
        bool includeSynthesisDecision)
    {
        var lines = new List<string>
        {
            "# Non-Implementation Review Decisions",
            string.Empty,
            "Fill every file decision row before rerunning completion. `Defer` is an explicit human decision.",
            string.Empty,
            "Allowed file decisions: `Keep`, `Delete`, `ResolveFalsePositive`, `Defer`.",
            string.Empty,
            "| Entry ID | Path | Reviewed SHA-256 | Reviewed Status | Decision | HITL Reason |",
            "|---|---|---|---|---|---|",
        };

        foreach (NonImplementationReviewLedgerEntry entry in unresolved)
        {
            lines.Add(
                $"| {Escape(entry.EntryId)} | " +
                $"{Escape(entry.Path)} | " +
                $"{Escape(ReviewedHash(entry))} | " +
                $"{Escape(entry.SemanticDisposition?.ToString() ?? "<none>")} |  |  |");
        }

        if (includeSynthesisDecision)
        {
            lines.Add(string.Empty);
            lines.Add("## Synthesis Decision");
            lines.Add(string.Empty);
            lines.Add("Allowed synthesis decisions: `KeepSynthesis`, `DiscardSynthesis`, `DeferSynthesis`.");
            lines.Add(string.Empty);
            lines.Add("| Synthesis Path | Decision | HITL Reason |");
            lines.Add("|---|---|---|");
            lines.Add($"| {SynthesisArtifactPath} |  |  |");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string RenderReviewEvidence(
        NonImplementationCompletionReviewStatus status,
        CompletionRefresh refresh,
        NonImplementationReviewLedgerDocument document,
        IReadOnlyList<NonImplementationReviewLedgerEntry> unresolved,
        NonImplementationCompletionReviewSummary summary,
        IReadOnlyList<string> blockerMessages)
    {
        var lines = new List<string>
        {
            "# Non-Implementation Epic Completion Review",
            string.Empty,
            "This artifact is review evidence for explicit HITL decisions. It is not repository certification or autonomous repository acceptance.",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| Status | {status} |",
            $"| Refresh ID | {Escape(refresh.RefreshId)} |",
            $"| Reviewed At | {DateTimeOffset.UtcNow:O} |",
            $"| Current Changed Files | {summary.CurrentChangedFileCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Semantic Candidates | {summary.SemanticCandidateCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Confirmed This Refresh | {summary.ConfirmedThisRefreshCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Reused Semantic Dispositions | {summary.ReusedSemanticDispositionCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Unresolved Confirmed Non-Implementation | {summary.UnresolvedConfirmedNonImplementationCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Unresolved Semantic Uncertainty | {summary.UnresolvedSemanticUncertaintyCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Resolved File Decisions | {summary.ResolvedFileDecisionCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Applied Deletes | {summary.AppliedDeleteCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Synthesis Decision Required | {summary.SynthesisDecisionRequired} |",
            $"| Synthesis Decision | {summary.SynthesisDecision?.ToString() ?? "None"} |",
            string.Empty,
            "## Blockers",
            string.Empty,
        };

        if (blockerMessages.Count == 0)
        {
            lines.Add("None.");
        }
        else
        {
            foreach (string blocker in blockerMessages)
            {
                lines.Add($"- {Escape(blocker)}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("## Current Refresh Classifications");
        lines.Add(string.Empty);
        if (refresh.Classifications.Classifications.Count == 0)
        {
            lines.Add("No current changed files were detected by the fresh completion review.");
        }
        else
        {
            lines.Add("| Path | Route | Rule | Post SHA-256 |");
            lines.Add("|---|---|---|---|");
            foreach (NonImplementationArtifactClassification classification in refresh.Classifications.Classifications
                         .OrderBy(item => item.File.Path, StringComparer.Ordinal))
            {
                lines.Add(
                    $"| {Escape(classification.File.Path)} | " +
                    $"{classification.Route} | " +
                    $"{Escape(classification.RuleId)} | " +
                    $"{Escape(classification.File.PostContentSha256 ?? "<none>")} |");
            }
        }

        lines.Add(string.Empty);
        lines.Add("## Review Entries");
        lines.Add(string.Empty);
        IReadOnlyList<NonImplementationReviewLedgerEntry> visibleEntries = document.Entries
            .Where(entry => entry.SemanticDisposition is not null)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
            .ToArray();
        if (visibleEntries.Count == 0)
        {
            lines.Add("No semantic review ledger entries exist.");
        }
        else
        {
            lines.Add("| Entry ID | Path | Semantic Disposition | Resolution State | Reviewed SHA-256 | Human Decision |");
            lines.Add("|---|---|---|---|---|---|");
            foreach (NonImplementationReviewLedgerEntry entry in visibleEntries)
            {
                lines.Add(
                    $"| {Escape(entry.EntryId)} | " +
                    $"{Escape(entry.Path)} | " +
                    $"{entry.SemanticDisposition} | " +
                    $"{entry.ResolutionState} | " +
                    $"{Escape(ReviewedHash(entry))} | " +
                    $"{Escape(entry.HumanDecision?.ResolutionState.ToString() ?? "None")} |");
            }
        }

        if (unresolved.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Required Pending Decisions");
            lines.Add(string.Empty);
            lines.Add($"Fill `{DecisionArtifactPath}` and rerun completion.");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseMarkdownTable(
        string content,
        IReadOnlyList<string> requiredColumns)
    {
        string[] lines = content.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        for (int index = 0; index < lines.Length - 1; index++)
        {
            IReadOnlyList<string> headers = SplitMarkdownRow(lines[index]);
            if (headers.Count == 0 ||
                !requiredColumns.All(required =>
                    headers.Any(header => string.Equals(header, required, StringComparison.OrdinalIgnoreCase))) ||
                !IsMarkdownSeparatorRow(lines[index + 1]))
            {
                continue;
            }

            var rows = new List<IReadOnlyDictionary<string, string>>();
            for (int rowIndex = index + 2; rowIndex < lines.Length; rowIndex++)
            {
                if (!lines[rowIndex].TrimStart().StartsWith('|'))
                {
                    break;
                }

                IReadOnlyList<string> values = SplitMarkdownRow(lines[rowIndex]);
                if (values.Count == 0)
                {
                    break;
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int cell = 0; cell < Math.Min(headers.Count, values.Count); cell++)
                {
                    row[headers[cell]] = values[cell].Trim();
                }

                rows.Add(row);
            }

            return rows;
        }

        return Array.Empty<IReadOnlyDictionary<string, string>>();
    }

    private static IReadOnlyList<string> SplitMarkdownRow(string line)
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith('|') || !trimmed.EndsWith('|'))
        {
            return Array.Empty<string>();
        }

        trimmed = trimmed.Trim('|');
        return trimmed
            .Split('|')
            .Select(cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())
            .ToArray();
    }

    private static bool IsMarkdownSeparatorRow(string line)
    {
        IReadOnlyList<string> cells = SplitMarkdownRow(line);
        return cells.Count > 0 &&
            cells.All(cell => cell.Length > 0 && cell.All(character => character is '-' or ':'));
    }

    private static string Field(IReadOnlyDictionary<string, string> row, string column) =>
        row.TryGetValue(column, out string? value) ? value.Trim() : string.Empty;

    private static bool IsUntracked(string status) =>
        status.StartsWith("??", StringComparison.Ordinal);

    private static string ReviewedHash(NonImplementationReviewLedgerEntry entry) =>
        entry.ReviewedFileDeleted
            ? NonImplementationReviewLedgerStore.DeletedReviewedIdentity(entry)
            : entry.ReviewedContentSha256 ?? string.Empty;

    private static string Escape(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim();

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Sha256Text(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record CompletionRefresh(
        string RefreshId,
        RepositorySliceSnapshot Snapshot,
        RepositorySliceDelta Delta,
        NonImplementationArtifactClassificationSet Classifications,
        NonImplementationSemanticConfirmationBatchResult Semantic);

    private enum FileReviewDecision
    {
        Keep,
        Delete,
        ResolveFalsePositive,
        Defer,
    }

    private sealed record FileDecisionRecord(
        string EntryId,
        FileReviewDecision Decision,
        string HitlReason);

    private sealed record SynthesisDecisionRecord(
        NonImplementationSynthesisDecision Decision,
        string HitlReason);

    private sealed record DecisionParseResult(
        IReadOnlyList<FileDecisionRecord> FileDecisions,
        SynthesisDecisionRecord? SynthesisDecision,
        IReadOnlyList<string> Errors);
}
