namespace LoopRelay.Roadmap.Cli.Models.Decisions;

internal sealed record DecisionLedgerPersistenceDocument(
    string SchemaVersion,
    IReadOnlyList<DecisionLedgerEntryDto> Entries)
{
    public const string CurrentSchemaVersion = "decision-ledger.v1";

    public static DecisionLedgerPersistenceDocument Empty { get; } = new(CurrentSchemaVersion, []);

    public static DecisionLedgerPersistenceDocument FromDomain(IReadOnlyList<DecisionLedgerEntry> entries) =>
        new(
            CurrentSchemaVersion,
            entries
                .OrderBy(entry => entry.DecisionId, StringComparer.Ordinal)
                .Select(DecisionLedgerEntryDto.FromDomain)
                .ToArray());

    public IReadOnlyList<DecisionLedgerEntry> ToDomain() =>
        Entries.Select(entry => entry.ToDomain()).OrderBy(entry => entry.DecisionId, StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<string> Validate(DecisionLedgerPersistenceDocument document)
    {
        var errors = new List<string>();
        if (document.Entries is null)
        {
            errors.Add("Decision ledger entries are required.");
            return errors;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (DecisionLedgerEntryDto entry in document.Entries)
        {
            if (entry is null)
            {
                errors.Add("Decision ledger entries cannot be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.DecisionId))
            {
                errors.Add("Decision ledger entries must include a decision ID.");
                continue;
            }

            if (!DecisionLedgerEntryDto.IsDecisionId(entry.DecisionId))
            {
                errors.Add($"Decision ledger entry `{entry.DecisionId}` does not match D0000 format.");
            }

            if (!seen.Add(entry.DecisionId))
            {
                errors.Add($"Decision ledger contains duplicate decision ID `{entry.DecisionId}`.");
            }

            if (entry.InputArtifactPaths is null)
            {
                errors.Add($"Decision ledger entry `{entry.DecisionId}` must include input artifact paths.");
            }

            if (entry.OutputArtifactPaths is null)
            {
                errors.Add($"Decision ledger entry `{entry.DecisionId}` must include output artifact paths.");
            }
        }

        return errors;
    }
}
