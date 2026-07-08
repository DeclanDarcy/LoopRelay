using System.Globalization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public interface INonImplementationPostExecutionReviewService
{
    Task<RepositorySliceBaseline> CapturePreSliceBaselineAsync(CancellationToken cancellationToken = default);

    Task<NonImplementationPostExecutionReviewResult> ReviewAfterExecutionAsync(
        RepositorySliceBaseline baseline,
        CancellationToken cancellationToken = default);
}

public sealed class NonImplementationPostExecutionReviewException : InvalidOperationException
{
    public NonImplementationPostExecutionReviewException(
        string message,
        IReadOnlyList<string> evidencePaths,
        Exception? innerException = null)
        : base(message, innerException)
    {
        EvidencePaths = evidencePaths;
    }

    public IReadOnlyList<string> EvidencePaths { get; }
}

public sealed class NonImplementationPostExecutionReviewService(
    RepositorySliceBaselineStore baselineStore,
    NonImplementationArtifactClassifier classifier,
    NonImplementationSemanticConfirmer semanticConfirmer,
    IArtifactStore artifacts) : INonImplementationPostExecutionReviewService
{
    public async Task<RepositorySliceBaseline> CapturePreSliceBaselineAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await baselineStore.CapturePreSliceAsync();
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException &&
            ex is not NonImplementationPostExecutionReviewException)
        {
            throw await CreateFailureExceptionAsync(
                baseline: null,
                operation: "capture the pre-slice repository baseline",
                ex);
        }
    }

    public async Task<NonImplementationPostExecutionReviewResult> ReviewAfterExecutionAsync(
        RepositorySliceBaseline baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepositorySliceSnapshot postSnapshot = await baselineStore.CapturePostSliceAsync(baseline);
            return await ReviewCoreAsync(baseline, postSnapshot, cancellationToken);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException &&
            ex is not NonImplementationPostExecutionReviewException)
        {
            throw await CreateFailureExceptionAsync(
                baseline,
                "review post-execution non-implementation file changes",
                ex);
        }
    }

    public async Task<NonImplementationPostExecutionReviewResult> ReviewAsync(
        RepositorySliceBaseline baseline,
        RepositorySliceSnapshot postSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(postSnapshot);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ReviewCoreAsync(baseline, postSnapshot, cancellationToken);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException &&
            ex is not NonImplementationPostExecutionReviewException)
        {
            throw await CreateFailureExceptionAsync(
                baseline,
                "review post-execution non-implementation file changes",
                ex);
        }
    }

    private async Task<NonImplementationPostExecutionReviewResult> ReviewCoreAsync(
        RepositorySliceBaseline baseline,
        RepositorySliceSnapshot postSnapshot,
        CancellationToken cancellationToken)
    {
        DateTimeOffset reviewedAtUtc = DateTimeOffset.UtcNow;
        RepositorySliceDelta delta = baselineStore.ComputeDelta(baseline, postSnapshot);
        NonImplementationArtifactClassificationSet classificationSet =
            await classifier.ClassifyAsync(delta);
        NonImplementationSemanticConfirmationBatchResult semantic =
            await semanticConfirmer.ConfirmAsync(
                classificationSet,
                reviewedAtUtc,
                discoveryContext: $"post-execution slice {baseline.ExecutionSliceId}",
                cancellationToken);

        NonImplementationPostExecutionReviewSummary summary =
            BuildSummary(delta, classificationSet, semantic);
        string reviewPath = OrchestrationArtifactPaths.NonImplementationSliceReview(baseline.ExecutionSliceId);
        await artifacts.WriteAsync(
            reviewPath,
            RenderReviewEvidence(
                baseline,
                postSnapshot,
                delta,
                classificationSet,
                semantic,
                summary,
                reviewedAtUtc));

        IReadOnlyList<string> evidencePaths = await BuildEvidencePathsAsync(baseline, reviewPath);
        return new NonImplementationPostExecutionReviewResult(
            baseline.ExecutionSliceId,
            evidencePaths,
            summary);
    }

    private async Task<IReadOnlyList<string>> BuildEvidencePathsAsync(
        RepositorySliceBaseline baseline,
        string reviewPath)
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseline.PersistedPath))
        {
            paths.Add(baseline.PersistedPath);
        }

        string postSnapshotPath = OrchestrationArtifactPaths.NonImplementationSlicePostSnapshot(
            baseline.ExecutionSliceId);
        if (await artifacts.ExistsAsync(postSnapshotPath))
        {
            paths.Add(postSnapshotPath);
        }

        paths.Add(reviewPath);
        return paths;
    }

    private async Task<NonImplementationPostExecutionReviewException> CreateFailureExceptionAsync(
        RepositorySliceBaseline? baseline,
        string operation,
        Exception ex)
    {
        string failurePath = baseline is null
            ? OrchestrationArtifactPaths.NonImplementationReviewFailure(NewFailureId())
            : OrchestrationArtifactPaths.NonImplementationSliceFailure(baseline.ExecutionSliceId);
        IReadOnlyList<string> evidencePaths = [];
        try
        {
            await artifacts.WriteAsync(failurePath, RenderFailureEvidence(baseline, operation, ex));
            evidencePaths = [failurePath];
        }
        catch
        {
            // The original review failure is more useful than a secondary evidence-write failure.
        }

        return new NonImplementationPostExecutionReviewException(
            $"Post-execution non-implementation review failed while trying to {operation}: {ex.Message}",
            evidencePaths,
            ex);
    }

    private static NonImplementationPostExecutionReviewSummary BuildSummary(
        RepositorySliceDelta delta,
        NonImplementationArtifactClassificationSet classificationSet,
        NonImplementationSemanticConfirmationBatchResult semantic)
    {
        IReadOnlyList<NonImplementationReviewLedgerEntry> semanticEntries =
        [
            ..semantic.ConfirmedEntries,
            ..semantic.SkippedEntries,
        ];

        return new NonImplementationPostExecutionReviewSummary(
            ChangedFileCount: delta.ChangedFiles.Count,
            ClassifiedFileCount: classificationSet.Classifications.Count,
            SemanticCandidateCount: classificationSet.Classifications.Count(classification =>
                NonImplementationSemanticConfirmer.RequiresSemanticConfirmation(classification.Route)),
            ConfirmedCount: semantic.ConfirmedCount,
            ReusedSemanticDispositionCount: semantic.SkippedCount,
            IgnoredCount: semantic.IgnoredCount,
            ConfirmedNonImplementationCount: semanticEntries.Count(entry =>
                entry.SemanticDisposition == NonImplementationSemanticDisposition.ConfirmedNonImplementation),
            FalsePositiveCount: semanticEntries.Count(entry =>
                entry.SemanticDisposition == NonImplementationSemanticDisposition.FalsePositive),
            SemanticUncertaintyCount: semanticEntries.Count(entry =>
                entry.SemanticDisposition == NonImplementationSemanticDisposition.Uncertain));
    }

    private static string RenderReviewEvidence(
        RepositorySliceBaseline baseline,
        RepositorySliceSnapshot postSnapshot,
        RepositorySliceDelta delta,
        NonImplementationArtifactClassificationSet classificationSet,
        NonImplementationSemanticConfirmationBatchResult semantic,
        NonImplementationPostExecutionReviewSummary summary,
        DateTimeOffset reviewedAtUtc)
    {
        Dictionary<string, NonImplementationReviewLedgerEntry> semanticByPath =
            semantic.ConfirmedEntries
                .Concat(semantic.SkippedEntries)
                .GroupBy(entry => entry.Path, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var lines = new List<string>
        {
            "# Non-Implementation Post-Execution Review",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| Execution Slice ID | {Escape(baseline.ExecutionSliceId)} |",
            $"| Reviewed At | {reviewedAtUtc:O} |",
            $"| Baseline Captured At | {baseline.Snapshot.CapturedAtUtc:O} |",
            $"| Post Snapshot Captured At | {postSnapshot.CapturedAtUtc:O} |",
            $"| Changed Files | {summary.ChangedFileCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Classified Files | {summary.ClassifiedFileCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Semantic Candidates | {summary.SemanticCandidateCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Confirmed This Run | {summary.ConfirmedCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Reused Semantic Dispositions | {summary.ReusedSemanticDispositionCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Ignored Deterministic Exclusions | {summary.IgnoredCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Confirmed Non-Implementation | {summary.ConfirmedNonImplementationCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| False Positives | {summary.FalsePositiveCount.ToString(CultureInfo.InvariantCulture)} |",
            $"| Semantic Uncertainties | {summary.SemanticUncertaintyCount.ToString(CultureInfo.InvariantCulture)} |",
            string.Empty,
            "## Changed Files",
            string.Empty,
        };

        if (delta.ChangedFiles.Count == 0)
        {
            lines.Add("No execution-produced changed files were detected.");
            lines.Add(string.Empty);
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        lines.Add("| Path | Route | Rule | Post SHA-256 | Semantic Disposition | Ledger Entry |");
        lines.Add("|---|---|---|---|---|---|");
        foreach (NonImplementationArtifactClassification classification in
            classificationSet.Classifications.OrderBy(item => item.File.Path, StringComparer.Ordinal))
        {
            semanticByPath.TryGetValue(classification.File.Path, out NonImplementationReviewLedgerEntry? entry);
            lines.Add(
                $"| {Escape(classification.File.Path)} | " +
                $"{classification.Route} | " +
                $"{Escape(classification.RuleId)} | " +
                $"{Escape(classification.File.PostContentSha256 ?? "<none>")} | " +
                $"{(entry?.SemanticDisposition.ToString() ?? "<none>")} | " +
                $"{Escape(entry?.EntryId ?? "<none>")} |");
        }

        lines.Add(string.Empty);
        lines.Add("## Semantic Evidence");
        lines.Add(string.Empty);
        IReadOnlyList<NonImplementationReviewLedgerEntry> semanticEntries =
        [
            ..semantic.ConfirmedEntries,
            ..semantic.SkippedEntries,
        ];
        if (semanticEntries.Count == 0)
        {
            lines.Add("No semantic confirmation was required.");
        }
        else
        {
            foreach (NonImplementationReviewLedgerEntry entry in
                semanticEntries.OrderBy(item => item.Path, StringComparer.Ordinal))
            {
                lines.Add($"### {Escape(entry.Path)}");
                lines.Add(string.Empty);
                lines.Add("| Field | Value |");
                lines.Add("|---|---|");
                lines.Add($"| Entry ID | {Escape(entry.EntryId)} |");
                lines.Add($"| Disposition | {entry.SemanticDisposition?.ToString() ?? "<none>"} |");
                lines.Add($"| Rationale | {Escape(entry.SemanticRationale ?? "<none>")} |");
                lines.Add($"| Reviewed SHA-256 | {Escape(entry.ReviewedContentSha256 ?? "<deleted>")} |");
                lines.Add($"| Reviewed Deleted | {entry.ReviewedFileDeleted} |");
                if (!string.IsNullOrWhiteSpace(entry.SemanticUncertaintyNote))
                {
                    lines.Add($"| Uncertainty Note | {Escape(entry.SemanticUncertaintyNote)} |");
                }

                lines.Add(string.Empty);
                lines.Add("Evidence:");
                foreach (string evidence in entry.SemanticEvidence)
                {
                    lines.Add($"- {Escape(evidence)}");
                }

                lines.Add(string.Empty);
            }
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string RenderFailureEvidence(
        RepositorySliceBaseline? baseline,
        string operation,
        Exception ex) =>
        string.Join(
            Environment.NewLine,
            [
                "# Non-Implementation Post-Execution Review Failure",
                string.Empty,
                "| Field | Value |",
                "|---|---|",
                $"| Execution Slice ID | {Escape(baseline?.ExecutionSliceId ?? "<pre-baseline>")} |",
                $"| Operation | {Escape(operation)} |",
                $"| Exception Type | {Escape(ex.GetType().FullName ?? ex.GetType().Name)} |",
                $"| Message | {Escape(ex.Message)} |",
                $"| Created At | {DateTimeOffset.UtcNow:O} |",
                string.Empty,
            ]) + Environment.NewLine;

    private static string NewFailureId() =>
        $"pre-slice-failure-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

    private static string Escape(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}
