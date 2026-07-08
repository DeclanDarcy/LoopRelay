using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationInsightSynthesizer
{
    public const int MetadataSchemaVersion = 1;

    internal const string MetadataStartMarker = "<!-- non-implementation-synthesis-metadata";

    internal const string MetadataEndMarker = "-->";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly NonImplementationReviewLedgerStore ledgerStore;
    private readonly INonImplementationReviewRunner runner;
    private readonly IArtifactStore artifacts;
    private readonly NonImplementationInsightSynthesizerOptions options;

    static NonImplementationInsightSynthesizer()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public NonImplementationInsightSynthesizer(
        NonImplementationReviewLedgerStore ledgerStore,
        INonImplementationReviewRunner runner,
        IArtifactStore artifacts,
        NonImplementationInsightSynthesizerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ledgerStore);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(artifacts);

        this.options = options ?? NonImplementationInsightSynthesizerOptions.Default;
        ArgumentException.ThrowIfNullOrWhiteSpace(this.options.PromptName);
        ArgumentException.ThrowIfNullOrWhiteSpace(this.options.SynthesisPromptSourceHash);
        if (this.options.MaxPromptPayloadCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum prompt payload characters must be positive.");
        }

        if (this.options.MaxFileContentCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum file content characters must be positive.");
        }

        runner.Capabilities.EnsureReadOnly();
        this.ledgerStore = ledgerStore;
        this.runner = runner;
        this.artifacts = artifacts;
    }

    public async Task<NonImplementationInsightSynthesisResult> SynthesizeAsync(
        CancellationToken cancellationToken = default)
    {
        NonImplementationReviewLedgerDocument document = await ledgerStore.LoadOrCreateAsync();
        IReadOnlyList<NonImplementationReviewLedgerEntry> confirmed = document.Entries
            .Where(entry =>
                entry.SemanticDisposition == NonImplementationSemanticDisposition.ConfirmedNonImplementation &&
                entry.ResolutionState == NonImplementationResolutionState.Unresolved)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<NonImplementationReviewLedgerEntry> uncertain = document.Entries
            .Where(entry =>
                entry.SemanticDisposition == NonImplementationSemanticDisposition.Uncertain &&
                entry.ResolutionState == NonImplementationResolutionState.Unresolved)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
            .ToArray();

        string? existing = await artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationSynthesis);
        if (confirmed.Count == 0)
        {
            return new NonImplementationInsightSynthesisResult(
                OrchestrationArtifactPaths.NonImplementationSynthesis,
                options.SynthesisPromptSourceHash,
                Generated: false,
                ReusedExisting: false,
                SkippedNoConfirmedEntries: true,
                PreviousSynthesisWasStale: existing is not null,
                SourceEntries: Array.Empty<NonImplementationInsightSynthesisSource>(),
                Content: null);
        }

        IReadOnlyList<NonImplementationInsightSynthesisSource> sources = confirmed
            .Concat(uncertain)
            .Select(SourceFromEntry)
            .ToArray();
        var metadata = new NonImplementationInsightSynthesisMetadata(
            MetadataSchemaVersion,
            options.SynthesisPromptSourceHash,
            sources);
        bool existingFresh =
            existing is not null &&
            TryReadMetadata(existing, out NonImplementationInsightSynthesisMetadata? existingMetadata) &&
            MetadataEquals(existingMetadata, metadata);

        if (existingFresh)
        {
            return new NonImplementationInsightSynthesisResult(
                OrchestrationArtifactPaths.NonImplementationSynthesis,
                options.SynthesisPromptSourceHash,
                Generated: false,
                ReusedExisting: true,
                SkippedNoConfirmedEntries: false,
                PreviousSynthesisWasStale: false,
                SourceEntries: sources,
                Content: existing);
        }

        string prompt = LoopRelay.Core.Prompts.SynthesizeNonImplementationInsights.Render(
            await BuildPromptPayloadAsync(confirmed, uncertain, cancellationToken));
        var request = new NonImplementationReviewRunnerRequest(
            options.PromptName,
            prompt,
            options.MaxPromptPayloadCharacters);
        request.Constraints.EnsureReadOnly();

        NonImplementationReviewRunnerResponse response =
            await runner.RunAsync(request, cancellationToken);
        string content = RenderSynthesisArtifact(metadata, response.StructuredText);
        await artifacts.WriteAsync(OrchestrationArtifactPaths.NonImplementationSynthesis, content);

        return new NonImplementationInsightSynthesisResult(
            OrchestrationArtifactPaths.NonImplementationSynthesis,
            options.SynthesisPromptSourceHash,
            Generated: true,
            ReusedExisting: false,
            SkippedNoConfirmedEntries: false,
            PreviousSynthesisWasStale: existing is not null,
            SourceEntries: sources,
            Content: content);
    }

    private async Task<string> BuildPromptPayloadAsync(
        IReadOnlyList<NonImplementationReviewLedgerEntry> confirmed,
        IReadOnlyList<NonImplementationReviewLedgerEntry> uncertain,
        CancellationToken cancellationToken)
    {
        var confirmedPayload = new List<NonImplementationInsightSynthesisEntryPayload>(confirmed.Count);
        foreach (NonImplementationReviewLedgerEntry entry in confirmed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            confirmedPayload.Add(await EntryPayloadAsync(entry));
        }

        var uncertainPayload = new List<NonImplementationInsightSynthesisEntryPayload>(uncertain.Count);
        foreach (NonImplementationReviewLedgerEntry entry in uncertain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uncertainPayload.Add(await EntryPayloadAsync(entry));
        }

        var payload = new NonImplementationInsightSynthesisPromptPayload(
            ReviewSupportOnly:
                "This synthesis supports human review only. It must not authorize keeping, deleting, retaining, promoting, committing, pushing, or otherwise mutating any source file.",
            ConfirmedNonImplementationEntries: confirmedPayload,
            SemanticUncertaintyEntries: uncertainPayload,
            RequiredOutput:
                "Return compact free-form Markdown only. Cite every factual point with source path and ledger entry ID. Put uncertain entries only under a separate 'Uncertain, Not Synthesized As Fact' section if they are useful.");

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private async Task<NonImplementationInsightSynthesisEntryPayload> EntryPayloadAsync(
        NonImplementationReviewLedgerEntry entry)
    {
        string? content = null;
        bool contentTruncated = false;
        string? contentOmittedReason = null;

        if (entry.ReviewedFileDeleted)
        {
            contentOmittedReason = "Reviewed file was deleted; no current file content is available.";
        }
        else
        {
            string? fileContent = await artifacts.ReadAsync(entry.Path);
            if (fileContent is null)
            {
                contentOmittedReason = "File was not present when synthesis ran.";
            }
            else if (fileContent.Length > options.MaxFileContentCharacters)
            {
                content = fileContent[..options.MaxFileContentCharacters];
                contentTruncated = true;
            }
            else
            {
                content = fileContent;
            }
        }

        return new NonImplementationInsightSynthesisEntryPayload(
            EntryId: entry.EntryId,
            Path: entry.Path,
            ReviewedContentSha256: entry.ReviewedContentSha256,
            ReviewedFileDeleted: entry.ReviewedFileDeleted,
            DeletedReviewedIdentity: entry.ReviewedFileDeleted
                ? NonImplementationReviewLedgerStore.DeletedReviewedIdentity(entry)
                : null,
            SemanticDisposition: entry.SemanticDisposition!.Value,
            SemanticRationale: entry.SemanticRationale,
            SemanticEvidence: entry.SemanticEvidence,
            SemanticUncertaintyNote: entry.SemanticUncertaintyNote,
            BoundedFileContent: content,
            FileContentTruncated: contentTruncated,
            FileContentOmittedReason: contentOmittedReason);
    }

    private static string RenderSynthesisArtifact(
        NonImplementationInsightSynthesisMetadata metadata,
        string synthesisMarkdown)
    {
        string sourceEntries = string.Join(
            Environment.NewLine,
            metadata.SourceEntries.Select(source =>
            {
                string reviewedIdentity = source.ReviewedFileDeleted
                    ? source.DeletedReviewedIdentity ?? "deleted:<unknown>"
                    : $"sha256:{source.ReviewedContentSha256 ?? "<unknown>"}";
                return $"- `{source.EntryId}` `{source.Path}` `{reviewedIdentity}` `{source.SemanticDisposition}`";
            }));

        return string.Join(
            Environment.NewLine,
            [
                MetadataStartMarker,
                JsonSerializer.Serialize(metadata, JsonOptions),
                MetadataEndMarker,
                string.Empty,
                "# Non-Implementation Review Synthesis",
                string.Empty,
                "This synthesis is review support only. It does not authorize keeping, deleting, retaining, promoting, committing, pushing, or otherwise mutating source files.",
                string.Empty,
                "## Source Entries",
                string.Empty,
                sourceEntries,
                string.Empty,
                "## Synthesis",
                string.Empty,
                synthesisMarkdown.Trim(),
                string.Empty,
            ]);
    }

    private static bool TryReadMetadata(
        string content,
        out NonImplementationInsightSynthesisMetadata? metadata)
    {
        metadata = null;
        int start = content.IndexOf(MetadataStartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        int jsonStart = content.IndexOf('\n', start);
        if (jsonStart < 0)
        {
            return false;
        }

        int end = content.IndexOf(MetadataEndMarker, jsonStart + 1, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        string json = content[(jsonStart + 1)..end].Trim();
        try
        {
            metadata = JsonSerializer.Deserialize<NonImplementationInsightSynthesisMetadata>(json, JsonOptions);
            return metadata is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool MetadataEquals(
        NonImplementationInsightSynthesisMetadata? left,
        NonImplementationInsightSynthesisMetadata right) =>
        left is not null &&
        left.SchemaVersion == right.SchemaVersion &&
        string.Equals(left.SynthesisPromptSourceHash, right.SynthesisPromptSourceHash, StringComparison.Ordinal) &&
        left.SourceEntries.SequenceEqual(right.SourceEntries);

    private static NonImplementationInsightSynthesisSource SourceFromEntry(
        NonImplementationReviewLedgerEntry entry) =>
        new(
            entry.EntryId,
            entry.Path,
            entry.ReviewedContentSha256,
            entry.ReviewedFileDeleted,
            entry.ReviewedFileDeleted
                ? NonImplementationReviewLedgerStore.DeletedReviewedIdentity(entry)
                : null,
            entry.SemanticDisposition!.Value);

    private sealed record NonImplementationInsightSynthesisMetadata(
        int SchemaVersion,
        string SynthesisPromptSourceHash,
        IReadOnlyList<NonImplementationInsightSynthesisSource> SourceEntries);

    private sealed record NonImplementationInsightSynthesisPromptPayload(
        string ReviewSupportOnly,
        IReadOnlyList<NonImplementationInsightSynthesisEntryPayload> ConfirmedNonImplementationEntries,
        IReadOnlyList<NonImplementationInsightSynthesisEntryPayload> SemanticUncertaintyEntries,
        string RequiredOutput);

    private sealed record NonImplementationInsightSynthesisEntryPayload(
        string EntryId,
        string Path,
        string? ReviewedContentSha256,
        bool ReviewedFileDeleted,
        string? DeletedReviewedIdentity,
        NonImplementationSemanticDisposition SemanticDisposition,
        string? SemanticRationale,
        IReadOnlyList<string> SemanticEvidence,
        string? SemanticUncertaintyNote,
        string? BoundedFileContent,
        bool FileContentTruncated,
        string? FileContentOmittedReason);
}
