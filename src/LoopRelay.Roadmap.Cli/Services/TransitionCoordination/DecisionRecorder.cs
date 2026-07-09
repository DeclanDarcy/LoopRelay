using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;

namespace LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

internal sealed class DecisionRecorder(IDecisionLedgerStore _decisionLedger)
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
        string id = await _decisionLedger.NextDecisionIdAsync();
        await _decisionLedger.AppendAsync(new DecisionLedgerEntry(
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
