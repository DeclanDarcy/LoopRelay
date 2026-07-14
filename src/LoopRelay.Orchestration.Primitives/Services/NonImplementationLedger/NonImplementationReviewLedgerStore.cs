using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationLedger;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationLedger;

public sealed class NonImplementationReviewLedgerStore(IArtifactStore _artifacts)
{
    public const int SchemaVersion = NonImplementationReviewLedgerDocument.CurrentSchemaVersion;

    public const string LedgerPath = OrchestrationArtifactPaths.NonImplementationLedger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    static NonImplementationReviewLedgerStore()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<NonImplementationReviewLedgerDocument> LoadOrCreateAsync()
    {
        string? content = await _artifacts.ReadAsync(LedgerPath);
        if (content is null)
        {
            return NonImplementationReviewLedgerDocument.Empty();
        }

        return Deserialize(content);
    }

    public async Task SaveAsync(NonImplementationReviewLedgerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document = Normalize(document);
        Validate(document);
        string content = JsonSerializer.Serialize(document, JsonOptions);
        await _artifacts.WriteAsync(LedgerPath, content + Environment.NewLine);
    }

    public async Task<IReadOnlyList<NonImplementationReviewLedgerEntry>> LoadConfirmedNonImplementationAsync()
    {
        NonImplementationReviewLedgerDocument document = await LoadOrCreateAsync();
        return document.Entries
            .Where(entry => entry.SemanticDisposition == NonImplementationSemanticDisposition.ConfirmedNonImplementation)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<NonImplementationReviewLedgerEntry>> LoadFalsePositivesAsync()
    {
        NonImplementationReviewLedgerDocument document = await LoadOrCreateAsync();
        return document.Entries
            .Where(entry => entry.SemanticDisposition == NonImplementationSemanticDisposition.FalsePositive)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<NonImplementationReviewLedgerEntry>> LoadSemanticUncertaintiesAsync()
    {
        NonImplementationReviewLedgerDocument document = await LoadOrCreateAsync();
        return document.Entries
            .Where(entry => entry.SemanticDisposition == NonImplementationSemanticDisposition.Uncertain)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<NonImplementationReviewLedgerEntry>> LoadPendingSemanticConfirmationAsync()
    {
        NonImplementationReviewLedgerDocument document = await LoadOrCreateAsync();
        return document.Entries
            .Where(entry => entry.SemanticDisposition is null &&
                entry.Route is NonImplementationArtifactRoute.SemanticReviewCandidate
                    or NonImplementationArtifactRoute.AmbiguousForSemanticReview)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<NonImplementationReviewLedgerEntry?> FindReusableSemanticDispositionAsync(
        NonImplementationArtifactClassification classification,
        string confirmationPromptSourceHash)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationPromptSourceHash);

        NonImplementationReviewLedgerDocument document = await LoadOrCreateAsync();
        return FindReusableSemanticDisposition(
            document.Entries,
            classification,
            confirmationPromptSourceHash);
    }

    public async Task<NonImplementationReviewLedgerEntry> UpsertPendingCandidateAsync(
        NonImplementationArtifactClassification classification,
        string confirmationPromptSourceHash,
        DateTimeOffset seenAtUtc,
        string? discoveryContext = null)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationPromptSourceHash);

        if (classification.Route is not NonImplementationArtifactRoute.SemanticReviewCandidate
            and not NonImplementationArtifactRoute.AmbiguousForSemanticReview)
        {
            throw new NonImplementationReviewLedgerException(
                $"Only semantic review candidates can be recorded in the non-implementation review ledger; received {classification.Route} for {classification.File.Path}.");
        }

        NonImplementationReviewLedgerDocument document = await LoadOrCreateAsync();
        var entries = document.Entries.ToList();
        DateTimeOffset utc = seenAtUtc.ToUniversalTime();
        int existingIndex = entries.FindIndex(entry =>
            SameSemanticReviewIdentity(entry, classification, confirmationPromptSourceHash));

        NonImplementationReviewLedgerEntry entry;
        if (existingIndex >= 0)
        {
            entry = entries[existingIndex] with
            {
                ExecutionSliceId = classification.File.ExecutionSliceId,
                DiscoveryContext = discoveryContext ?? entries[existingIndex].DiscoveryContext,
                PreviousPath = NormalizeOptionalPath(classification.File.PreviousPath),
                BaselineStatus = classification.File.BaselineStatus,
                PostStatus = classification.File.PostStatus,
                BaselineContentSha256 = classification.File.BaselineContentSha256,
                PreExisted = classification.File.PreExisted,
                Route = classification.Route,
                ClassificationRuleId = classification.RuleId,
                ClassificationRationale = classification.Rationale,
                ClassificationPathFacts = classification.PathFacts.ToArray(),
                LastSeenAtUtc = utc,
            };
            entries[existingIndex] = entry;
        }
        else
        {
            entry = CreatePendingEntry(classification, confirmationPromptSourceHash, utc, discoveryContext);
            entries.Add(entry);
        }

        await SaveAsync(document with { Entries = entries });
        return entry;
    }

    public async Task<NonImplementationReviewLedgerEntry> AttachHitlProvenanceAsync(
        string entryId,
        NonImplementationHitlProvenanceKind provenanceKind,
        string evidencePath,
        string? sourceHash,
        string? rationale,
        string? evidenceExcerpt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidencePath);

        if (provenanceKind is NonImplementationHitlProvenanceKind.None)
        {
            throw new NonImplementationReviewLedgerException(
                "HITL provenance attachment requires HitlRequested or HitlKept provenance.");
        }

        NonImplementationReviewLedgerDocument document = await LoadOrCreateAsync();
        var entries = document.Entries.ToList();
        int index = entries.FindIndex(entry => string.Equals(entry.EntryId, entryId, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new NonImplementationReviewLedgerException(
                $"Cannot attach HITL provenance to unknown non-implementation ledger entry {entryId}.");
        }

        NonImplementationReviewLedgerEntry updated = entries[index] with
        {
            HitlProvenanceKind = provenanceKind,
            HitlProvenanceEvidencePath = evidencePath,
            HitlProvenanceSourceHash = sourceHash,
            HitlProvenanceRationale = rationale,
            HitlProvenanceEvidenceExcerpt = evidenceExcerpt,
        };
        entries[index] = updated;
        await SaveAsync(document with { Entries = entries });
        return updated;
    }

    public async Task<NonImplementationReviewLedgerEntry> RecordSemanticConfirmationAsync(
        Models.NonImplementationSemanticConfirmation.NonImplementationSemanticConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);

        NonImplementationReviewLedgerDocument document = await LoadOrCreateAsync();
        var entries = document.Entries.ToList();
        int index = entries.FindIndex(entry =>
            string.Equals(entry.EntryId, confirmation.LedgerEntryId, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new NonImplementationReviewLedgerException(
                $"Cannot record semantic confirmation for unknown non-implementation ledger entry {confirmation.LedgerEntryId}.");
        }

        ValidateSemanticConfirmation(entries[index], confirmation);
        NonImplementationReviewLedgerEntry updated = entries[index] with
        {
            SemanticDisposition = confirmation.Disposition,
            SemanticRationale = confirmation.Rationale.Trim(),
            SemanticEvidence = confirmation.EvidenceExcerptsOrPathFacts
                .Where(evidence => !string.IsNullOrWhiteSpace(evidence))
                .Select(evidence => evidence.Trim())
                .ToArray(),
            SemanticUncertaintyNote = string.IsNullOrWhiteSpace(confirmation.UncertaintyNote)
                ? null
                : confirmation.UncertaintyNote.Trim(),
        };
        entries[index] = updated;
        await SaveAsync(document with { Entries = entries });
        return updated;
    }

    public static NonImplementationReviewLedgerEntry? FindReusableSemanticDisposition(
        IReadOnlyList<NonImplementationReviewLedgerEntry> entries,
        NonImplementationArtifactClassification classification,
        string confirmationPromptSourceHash)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationPromptSourceHash);

        return entries.FirstOrDefault(entry =>
            entry.SemanticDisposition is not null &&
            SameSemanticReviewIdentity(entry, classification, confirmationPromptSourceHash));
    }

    private static NonImplementationReviewLedgerDocument Deserialize(string content)
    {
        try
        {
            NonImplementationReviewLedgerDocument? document =
                JsonSerializer.Deserialize<NonImplementationReviewLedgerDocument>(content, JsonOptions);
            if (document is null)
            {
                throw new NonImplementationReviewLedgerException("Non-implementation review ledger was empty.");
            }

            document = Normalize(document);
            Validate(document);
            return document;
        }
        catch (JsonException ex)
        {
            throw new NonImplementationReviewLedgerException(
                $"Invalid non-implementation review ledger JSON at {LedgerPath}: {ex.Message}",
                ex);
        }
    }

    private static void Validate(NonImplementationReviewLedgerDocument document)
    {
        if (document.SchemaVersion != SchemaVersion)
        {
            throw new NonImplementationReviewLedgerException(
                $"Unsupported non-implementation review ledger schema version {document.SchemaVersion}; expected {SchemaVersion}.");
        }

        if (document.Entries is null)
        {
            throw new NonImplementationReviewLedgerException(
                "Non-implementation review ledger entries section is required.");
        }

        if (document.HitlRequests is null)
        {
            throw new NonImplementationReviewLedgerException(
                "Non-implementation review ledger HITL requests section is required.");
        }

        foreach (NonImplementationReviewLedgerEntry entry in document.Entries)
        {
            ValidateEntry(entry);
        }

        foreach (NonImplementationHitlRequestEntry request in document.HitlRequests)
        {
            ValidateHitlRequest(request);
        }
    }

    private static NonImplementationReviewLedgerDocument Normalize(NonImplementationReviewLedgerDocument document) =>
        document with
        {
            Entries = document.Entries?.Select(NormalizeEntry).ToArray()
                ?? Array.Empty<NonImplementationReviewLedgerEntry>(),
            HitlRequests = document.HitlRequests ?? Array.Empty<NonImplementationHitlRequestEntry>(),
            SynthesisDecision = document.SynthesisDecision is null
                ? null
                : document.SynthesisDecision with
                {
                    DecidedAtUtc = document.SynthesisDecision.DecidedAtUtc.ToUniversalTime(),
                },
        };

    private static NonImplementationReviewLedgerEntry CreatePendingEntry(
        NonImplementationArtifactClassification classification,
        string confirmationPromptSourceHash,
        DateTimeOffset seenAtUtc,
        string? discoveryContext)
    {
        RepositoryChangedFileFacts file = classification.File;
        bool deleted = IsReviewedDeletion(file);
        string? reviewedContentSha256 = deleted ? null : file.PostContentSha256;
        string entryId = CreateEntryId(classification, confirmationPromptSourceHash);

        return new NonImplementationReviewLedgerEntry
        {
            EntryId = entryId,
            ExecutionSliceId = file.ExecutionSliceId,
            DiscoveryContext = discoveryContext,
            Path = NormalizePath(file.Path),
            PreviousPath = NormalizeOptionalPath(file.PreviousPath),
            BaselineStatus = file.BaselineStatus,
            PostStatus = file.PostStatus,
            ReviewedContentSha256 = reviewedContentSha256,
            ReviewedFileDeleted = deleted,
            BaselineContentSha256 = file.BaselineContentSha256,
            PreExisted = file.PreExisted,
            Route = classification.Route,
            ClassificationRuleId = classification.RuleId,
            ClassificationRationale = classification.Rationale,
            ClassificationPathFacts = classification.PathFacts.ToArray(),
            ClassifierVersion = classification.ClassifierVersion,
            SemanticDisposition = null,
            SemanticRationale = null,
            SemanticEvidence = Array.Empty<string>(),
            ConfirmationPromptSourceHash = confirmationPromptSourceHash.Trim(),
            FirstSeenAtUtc = seenAtUtc,
            LastSeenAtUtc = seenAtUtc,
            HitlProvenanceKind = NonImplementationHitlProvenanceKind.None,
            ResolutionState = NonImplementationResolutionState.Unresolved,
        };
    }

    private static NonImplementationReviewLedgerEntry NormalizeEntry(NonImplementationReviewLedgerEntry entry) =>
        entry with
        {
            Path = NormalizePath(entry.Path),
            PreviousPath = NormalizeOptionalPath(entry.PreviousPath),
            ClassificationPathFacts = entry.ClassificationPathFacts ?? Array.Empty<string>(),
            SemanticEvidence = entry.SemanticEvidence ?? Array.Empty<string>(),
            SemanticUncertaintyNote = string.IsNullOrWhiteSpace(entry.SemanticUncertaintyNote)
                ? null
                : entry.SemanticUncertaintyNote.Trim(),
            FirstSeenAtUtc = entry.FirstSeenAtUtc.ToUniversalTime(),
            LastSeenAtUtc = entry.LastSeenAtUtc.ToUniversalTime(),
        };

    private static void ValidateEntry(NonImplementationReviewLedgerEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EntryId))
        {
            throw new NonImplementationReviewLedgerException(
                "Non-implementation review ledger entry ID is required.");
        }

        if (string.IsNullOrWhiteSpace(entry.Path))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} path is required.");
        }

        if (string.IsNullOrWhiteSpace(entry.ClassificationRuleId))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} classification rule ID is required.");
        }

        if (string.IsNullOrWhiteSpace(entry.ClassificationRationale))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} classification rationale is required.");
        }

        if (entry.ClassificationPathFacts is null)
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} classification path facts are required.");
        }

        if (string.IsNullOrWhiteSpace(entry.ClassifierVersion))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} classifier version is required.");
        }

        if (string.IsNullOrWhiteSpace(entry.ConfirmationPromptSourceHash))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} confirmation prompt source hash is required.");
        }

        if (entry.SemanticEvidence is null)
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} semantic evidence section is required.");
        }

        if (entry.SemanticDisposition is not null && string.IsNullOrWhiteSpace(entry.SemanticRationale))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} semantic rationale is required when a semantic disposition is recorded.");
        }

        if (!entry.ReviewedFileDeleted && string.IsNullOrWhiteSpace(entry.ReviewedContentSha256))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} reviewed content hash is required for non-deleted files.");
        }

        if (entry.FirstSeenAtUtc == default)
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} first-seen timestamp is required.");
        }

        if (entry.LastSeenAtUtc == default)
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation review ledger entry {entry.EntryId} last-seen timestamp is required.");
        }
    }

    private static void ValidateHitlRequest(NonImplementationHitlRequestEntry request)
    {
        if (string.IsNullOrWhiteSpace(request.DeliverablePathOrPattern))
        {
            throw new NonImplementationReviewLedgerException(
                "Non-implementation HITL request deliverable path or pattern is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceArtifactPath))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation HITL request for {request.DeliverablePathOrPattern} source artifact path is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceHash))
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation HITL request for {request.DeliverablePathOrPattern} source hash is required.");
        }

        if (request.HitlProvenanceKind == NonImplementationHitlProvenanceKind.None)
        {
            throw new NonImplementationReviewLedgerException(
                $"Non-implementation HITL request for {request.DeliverablePathOrPattern} must record HITL provenance.");
        }
    }

    private static bool SameSemanticReviewIdentity(
        NonImplementationReviewLedgerEntry entry,
        NonImplementationArtifactClassification classification,
        string confirmationPromptSourceHash)
    {
        RepositoryChangedFileFacts file = classification.File;
        if (!string.Equals(entry.Path, NormalizePath(file.Path), StringComparison.Ordinal) ||
            !string.Equals(entry.ClassifierVersion, classification.ClassifierVersion, StringComparison.Ordinal) ||
            !string.Equals(
                entry.ConfirmationPromptSourceHash,
                confirmationPromptSourceHash.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        bool deleted = IsReviewedDeletion(file);
        if (deleted)
        {
            return entry.ReviewedFileDeleted &&
                string.Equals(entry.BaselineContentSha256, file.BaselineContentSha256, StringComparison.Ordinal);
        }

        return !entry.ReviewedFileDeleted &&
            !string.IsNullOrWhiteSpace(file.PostContentSha256) &&
            string.Equals(entry.ReviewedContentSha256, file.PostContentSha256, StringComparison.Ordinal);
    }

    private static void ValidateSemanticConfirmation(
        NonImplementationReviewLedgerEntry entry,
        Models.NonImplementationSemanticConfirmation.NonImplementationSemanticConfirmation confirmation)
    {
        if (!string.Equals(entry.EntryId, confirmation.LedgerEntryId, StringComparison.Ordinal))
        {
            throw new NonImplementationReviewLedgerException(
                $"Semantic confirmation entry ID {confirmation.LedgerEntryId} does not match ledger entry {entry.EntryId}.");
        }

        if (!string.Equals(entry.Path, NormalizePath(confirmation.CandidatePath), StringComparison.Ordinal))
        {
            throw new NonImplementationReviewLedgerException(
                $"Semantic confirmation path {confirmation.CandidatePath} does not match ledger entry {entry.Path}.");
        }

        if (entry.ReviewedFileDeleted != confirmation.ReviewedFileDeleted)
        {
            throw new NonImplementationReviewLedgerException(
                $"Semantic confirmation reviewed-file status does not match ledger entry {entry.EntryId}.");
        }

        if (entry.ReviewedFileDeleted)
        {
            string expectedDeletedIdentity = DeletedReviewedIdentity(entry);
            if (!string.Equals(
                confirmation.DeletedReviewedIdentity,
                expectedDeletedIdentity,
                StringComparison.Ordinal))
            {
                throw new NonImplementationReviewLedgerException(
                    $"Semantic confirmation deleted-reviewed identity does not match ledger entry {entry.EntryId}.");
            }
        }
        else if (!string.Equals(
            entry.ReviewedContentSha256,
            confirmation.ReviewedContentSha256,
            StringComparison.Ordinal))
        {
            throw new NonImplementationReviewLedgerException(
                $"Semantic confirmation reviewed content hash does not match ledger entry {entry.EntryId}.");
        }

        if (string.IsNullOrWhiteSpace(confirmation.Rationale))
        {
            throw new NonImplementationReviewLedgerException(
                $"Semantic confirmation rationale is required for ledger entry {entry.EntryId}.");
        }

        if (confirmation.Disposition == NonImplementationSemanticDisposition.Uncertain &&
            string.IsNullOrWhiteSpace(confirmation.UncertaintyNote))
        {
            throw new NonImplementationReviewLedgerException(
                $"Semantic confirmation uncertainty note is required for uncertain ledger entry {entry.EntryId}.");
        }
    }

    private static string CreateEntryId(
        NonImplementationArtifactClassification classification,
        string confirmationPromptSourceHash)
    {
        RepositoryChangedFileFacts file = classification.File;
        string reviewedIdentity = IsReviewedDeletion(file)
            ? $"deleted:{file.BaselineContentSha256 ?? "<none>"}"
            : $"sha256:{file.PostContentSha256 ?? "<none>"}";
        string identity = string.Join(
            "\n",
            NormalizePath(file.Path),
            reviewedIdentity,
            classification.ClassifierVersion,
            confirmationPromptSourceHash.Trim());
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return "ni-" + Convert.ToHexString(hash).ToLowerInvariant()[..24];
    }

    private static bool IsReviewedDeletion(RepositoryChangedFileFacts file) =>
        file.IsDeleted || !file.Exists;

    public static string DeletedReviewedIdentity(NonImplementationReviewLedgerEntry entry) =>
        $"deleted:{entry.BaselineContentSha256 ?? "<none>"}";

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim();

    private static string? NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : NormalizePath(path);
}
