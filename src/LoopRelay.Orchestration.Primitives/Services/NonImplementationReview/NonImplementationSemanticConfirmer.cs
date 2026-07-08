using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationSemanticConfirmer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly NonImplementationReviewLedgerStore ledgerStore;
    private readonly INonImplementationReviewRunner runner;
    private readonly NonImplementationSemanticConfirmerOptions options;

    static NonImplementationSemanticConfirmer()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public NonImplementationSemanticConfirmer(
        NonImplementationReviewLedgerStore ledgerStore,
        INonImplementationReviewRunner runner,
        NonImplementationSemanticConfirmerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ledgerStore);
        ArgumentNullException.ThrowIfNull(runner);

        this.options = options ?? NonImplementationSemanticConfirmerOptions.Default;
        ArgumentException.ThrowIfNullOrWhiteSpace(this.options.PromptName);
        ArgumentException.ThrowIfNullOrWhiteSpace(this.options.ConfirmationPromptSourceHash);
        if (this.options.MaxPromptPayloadCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum prompt payload characters must be positive.");
        }

        runner.Capabilities.EnsureReadOnly();
        this.ledgerStore = ledgerStore;
        this.runner = runner;
    }

    public async Task<NonImplementationSemanticConfirmationBatchResult> ConfirmAsync(
        NonImplementationArtifactClassificationSet classificationSet,
        DateTimeOffset seenAtUtc,
        string? discoveryContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(classificationSet);

        var confirmed = new List<NonImplementationReviewLedgerEntry>();
        var skipped = new List<NonImplementationReviewLedgerEntry>();
        var ignored = new List<NonImplementationArtifactClassification>();

        foreach (NonImplementationArtifactClassification classification in classificationSet.Classifications)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!RequiresSemanticConfirmation(classification.Route))
            {
                ignored.Add(classification);
                continue;
            }

            NonImplementationReviewLedgerEntry? reusable =
                await ledgerStore.FindReusableSemanticDispositionAsync(
                    classification,
                    options.ConfirmationPromptSourceHash);
            if (reusable is not null)
            {
                skipped.Add(reusable);
                continue;
            }

            NonImplementationReviewLedgerEntry pending =
                await ledgerStore.UpsertPendingCandidateAsync(
                    classification,
                    options.ConfirmationPromptSourceHash,
                    seenAtUtc,
                    discoveryContext);

            string prompt = LoopRelay.Core.Prompts.ConfirmNonImplementationCandidate.Render(
                BuildPromptPayload(pending, classification));
            var request = new NonImplementationReviewRunnerRequest(
                options.PromptName,
                prompt,
                options.MaxPromptPayloadCharacters);
            request.Constraints.EnsureReadOnly();

            NonImplementationReviewRunnerResponse response =
                await runner.RunAsync(request, cancellationToken);
            NonImplementationSemanticConfirmation confirmation =
                NonImplementationSemanticConfirmationParser.ParseAndValidate(
                    response.StructuredText,
                    pending);
            NonImplementationReviewLedgerEntry updated =
                await ledgerStore.RecordSemanticConfirmationAsync(confirmation);
            confirmed.Add(updated);
        }

        return new NonImplementationSemanticConfirmationBatchResult(confirmed, skipped, ignored);
    }

    public static bool RequiresSemanticConfirmation(NonImplementationArtifactRoute route) =>
        route is NonImplementationArtifactRoute.SemanticReviewCandidate
            or NonImplementationArtifactRoute.AmbiguousForSemanticReview;

    private static string BuildPromptPayload(
        NonImplementationReviewLedgerEntry entry,
        NonImplementationArtifactClassification classification)
    {
        var payload = new SemanticConfirmationPromptPayload(
            LedgerEntryId: entry.EntryId,
            CandidatePath: entry.Path,
            Route: entry.Route,
            ExecutionSliceId: entry.ExecutionSliceId,
            DiscoveryContext: entry.DiscoveryContext,
            BaselineStatus: entry.BaselineStatus,
            PostStatus: entry.PostStatus,
            ReviewedContentSha256: entry.ReviewedContentSha256,
            ReviewedFileDeleted: entry.ReviewedFileDeleted,
            DeletedReviewedIdentity: entry.ReviewedFileDeleted
                ? NonImplementationReviewLedgerStore.DeletedReviewedIdentity(entry)
                : null,
            BaselineContentSha256: entry.BaselineContentSha256,
            DeterministicEvidence: new DeterministicClassificationEvidence(
                classification.RuleId,
                classification.Rationale,
                classification.PathFacts,
                classification.ClassifierVersion),
            ReadOnlyInspectionInstructions:
                "If deterministic evidence is insufficient, inspect candidatePath read-only in the repository. Do not write files, run mutation-capable operations, decide keep/delete, commit, or push.",
            RequiredOutput:
                "Return exactly one strict JSON object matching the prompt schema. The ledgerEntryId, candidatePath, reviewedContentSha256 or deletedReviewedIdentity, and reviewedFileDeleted values must match this payload.");

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private sealed record SemanticConfirmationPromptPayload(
        string LedgerEntryId,
        string CandidatePath,
        NonImplementationArtifactRoute Route,
        string? ExecutionSliceId,
        string? DiscoveryContext,
        string? BaselineStatus,
        string? PostStatus,
        string? ReviewedContentSha256,
        bool ReviewedFileDeleted,
        string? DeletedReviewedIdentity,
        string? BaselineContentSha256,
        DeterministicClassificationEvidence DeterministicEvidence,
        string ReadOnlyInspectionInstructions,
        string RequiredOutput);

    private sealed record DeterministicClassificationEvidence(
        string RuleId,
        string Rationale,
        IReadOnlyList<string> PathFacts,
        string ClassifierVersion);
}
