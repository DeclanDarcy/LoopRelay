namespace LoopRelay.Roadmap.Cli;

internal sealed class DecisionRecorder(DecisionLedgerStore decisionLedger)
{
    public async Task AppendAsync(
        RoadmapState state,
        string transition,
        string projectionPath,
        string outputPath,
        string decision,
        string confidence,
        string rationale)
    {
        string id = await decisionLedger.NextDecisionIdAsync();
        await decisionLedger.AppendAsync(new DecisionLedgerEntry(
            id,
            DateTimeOffset.UtcNow,
            state,
            transition,
            transition,
            projectionPath,
            [],
            [outputPath],
            decision,
            confidence,
            rationale));
    }
}
