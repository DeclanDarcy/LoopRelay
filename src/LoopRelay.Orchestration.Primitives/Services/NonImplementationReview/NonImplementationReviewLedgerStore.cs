using System.Text.Json;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationReviewLedgerException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public sealed class NonImplementationReviewLedgerStore(IArtifactStore artifacts)
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

    public async Task<NonImplementationReviewLedgerDocument> LoadOrCreateAsync()
    {
        string? content = await artifacts.ReadAsync(LedgerPath);
        if (content is null)
        {
            return NonImplementationReviewLedgerDocument.Empty();
        }

        return Deserialize(content);
    }

    public async Task SaveAsync(NonImplementationReviewLedgerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Validate(document);
        string content = JsonSerializer.Serialize(document, JsonOptions);
        await artifacts.WriteAsync(LedgerPath, content + Environment.NewLine);
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
    }
}
