using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationSemanticConfirmationParseException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public static class NonImplementationSemanticConfirmationParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    static NonImplementationSemanticConfirmationParser()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    }

    public static NonImplementationSemanticConfirmation ParseAndValidate(
        string structuredText,
        NonImplementationReviewLedgerEntry expectedEntry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(structuredText);
        ArgumentNullException.ThrowIfNull(expectedEntry);

        string trimmed = structuredText.Trim();
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output must be a strict JSON object.");
        }

        SemanticConfirmationDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<SemanticConfirmationDto>(trimmed, JsonOptions)
                ?? throw new NonImplementationSemanticConfirmationParseException(
                    "Semantic confirmation output JSON was empty.");
        }
        catch (JsonException ex)
        {
            throw new NonImplementationSemanticConfirmationParseException(
                $"Invalid semantic confirmation JSON: {ex.Message}",
                ex);
        }

        NonImplementationSemanticConfirmation confirmation = ToConfirmation(dto);
        ValidateAgainstExpectedEntry(confirmation, expectedEntry);
        return confirmation;
    }

    private static NonImplementationSemanticConfirmation ToConfirmation(SemanticConfirmationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.LedgerEntryId))
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output is missing ledgerEntryId.");
        }

        if (string.IsNullOrWhiteSpace(dto.CandidatePath))
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output is missing candidatePath.");
        }

        if (dto.ReviewedFileDeleted is null)
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output is missing reviewedFileDeleted.");
        }

        if (dto.Disposition is null)
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output is missing disposition.");
        }

        if (string.IsNullOrWhiteSpace(dto.Rationale))
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output is missing rationale.");
        }

        if (dto.EvidenceExcerptsOrPathFacts is null || dto.EvidenceExcerptsOrPathFacts.Count == 0)
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output is missing evidenceExcerptsOrPathFacts.");
        }

        IReadOnlyList<string> evidence = dto.EvidenceExcerptsOrPathFacts
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();
        if (evidence.Count == 0)
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output evidenceExcerptsOrPathFacts must contain non-empty evidence.");
        }

        if (dto.Disposition == NonImplementationSemanticDisposition.Uncertain &&
            string.IsNullOrWhiteSpace(dto.UncertaintyNote))
        {
            throw new NonImplementationSemanticConfirmationParseException(
                "Semantic confirmation output must include uncertaintyNote when disposition is Uncertain.");
        }

        return new NonImplementationSemanticConfirmation(
            dto.LedgerEntryId.Trim(),
            NormalizePath(dto.CandidatePath),
            string.IsNullOrWhiteSpace(dto.ReviewedContentSha256) ? null : dto.ReviewedContentSha256.Trim(),
            dto.ReviewedFileDeleted.Value,
            string.IsNullOrWhiteSpace(dto.DeletedReviewedIdentity) ? null : dto.DeletedReviewedIdentity.Trim(),
            dto.Disposition.Value,
            dto.Rationale.Trim(),
            evidence,
            string.IsNullOrWhiteSpace(dto.UncertaintyNote) ? null : dto.UncertaintyNote.Trim());
    }

    private static void ValidateAgainstExpectedEntry(
        NonImplementationSemanticConfirmation confirmation,
        NonImplementationReviewLedgerEntry expectedEntry)
    {
        if (!string.Equals(confirmation.LedgerEntryId, expectedEntry.EntryId, StringComparison.Ordinal))
        {
            throw new NonImplementationSemanticConfirmationParseException(
                $"Semantic confirmation ledgerEntryId {confirmation.LedgerEntryId} does not match expected entry {expectedEntry.EntryId}.");
        }

        if (!string.Equals(confirmation.CandidatePath, NormalizePath(expectedEntry.Path), StringComparison.Ordinal))
        {
            throw new NonImplementationSemanticConfirmationParseException(
                $"Semantic confirmation candidatePath {confirmation.CandidatePath} does not match expected path {expectedEntry.Path}.");
        }

        if (confirmation.ReviewedFileDeleted != expectedEntry.ReviewedFileDeleted)
        {
            throw new NonImplementationSemanticConfirmationParseException(
                $"Semantic confirmation reviewedFileDeleted does not match expected status for {expectedEntry.EntryId}.");
        }

        if (expectedEntry.ReviewedFileDeleted)
        {
            string expectedDeletedIdentity = NonImplementationReviewLedgerStore.DeletedReviewedIdentity(expectedEntry);
            if (!string.Equals(
                confirmation.DeletedReviewedIdentity,
                expectedDeletedIdentity,
                StringComparison.Ordinal))
            {
                throw new NonImplementationSemanticConfirmationParseException(
                    $"Semantic confirmation deletedReviewedIdentity does not match expected identity for {expectedEntry.EntryId}.");
            }

            return;
        }

        if (!string.Equals(
            confirmation.ReviewedContentSha256,
            expectedEntry.ReviewedContentSha256,
            StringComparison.Ordinal))
        {
            throw new NonImplementationSemanticConfirmationParseException(
                $"Semantic confirmation reviewedContentSha256 does not match expected hash for {expectedEntry.EntryId}.");
        }
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim();

    private sealed class SemanticConfirmationDto
    {
        public string? LedgerEntryId { get; set; }

        public string? CandidatePath { get; set; }

        public string? ReviewedContentSha256 { get; set; }

        public bool? ReviewedFileDeleted { get; set; }

        public string? DeletedReviewedIdentity { get; set; }

        public NonImplementationSemanticDisposition? Disposition { get; set; }

        public string? Rationale { get; set; }

        public IReadOnlyList<string>? EvidenceExcerptsOrPathFacts { get; set; }

        public string? UncertaintyNote { get; set; }
    }
}
