using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationLedger;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationLedger;

namespace LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;

public sealed class NonImplementationSemanticConfirmer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly NonImplementationReviewLedgerStore _ledgerStore;
    private readonly INonImplementationReviewRunner _runner;
    private readonly NonImplementationSemanticConfirmerOptions _options;

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

        _options = options ?? NonImplementationSemanticConfirmerOptions.Default;
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.PromptName);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ConfirmationPromptSourceHash);
        if (_options.MaxPromptPayloadCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum prompt payload characters must be positive.");
        }

        runner.Capabilities.EnsureReadOnly();
        _ledgerStore = ledgerStore;
        _runner = runner;
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
                await _ledgerStore.FindReusableSemanticDispositionAsync(
                    classification,
                    _options.ConfirmationPromptSourceHash);
            if (reusable is not null)
            {
                skipped.Add(reusable);
                continue;
            }

            NonImplementationReviewLedgerEntry pending =
                await _ledgerStore.UpsertPendingCandidateAsync(
                    classification,
                    _options.ConfirmationPromptSourceHash,
                    seenAtUtc,
                    discoveryContext);

            string prompt = LoopRelay.Core.Prompts.ConfirmNonImplementationCandidate.Render(
                BuildPromptPayload(pending, classification));
            var request = new NonImplementationReviewRunnerRequest(
                _options.PromptName,
                prompt,
                _options.MaxPromptPayloadCharacters);
            request.Constraints.EnsureReadOnly();

            NonImplementationReviewRunnerResponse response =
                await _runner.RunAsync(request, cancellationToken);
            Models.NonImplementationSemanticConfirmation.NonImplementationSemanticConfirmation confirmation =
                NonImplementationSemanticConfirmationParser.ParseAndValidate(
                    response.StructuredText,
                    pending);
            NonImplementationReviewLedgerEntry updated =
                await _ledgerStore.RecordSemanticConfirmationAsync(confirmation);
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
                classification.ClassifierVersion));

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    // The payload is pure data (M7/B4): the inspection and output instructions live in the
    // ConfirmNonImplementationCandidate template, so the template source hash covers every
    // instruction the agent receives.
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
        DeterministicClassificationEvidence DeterministicEvidence);

    private sealed record DeterministicClassificationEvidence(
        string RuleId,
        string Rationale,
        IReadOnlyList<string> PathFacts,
        string ClassifierVersion);
}
