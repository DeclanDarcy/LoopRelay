using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Cli.Services.Decisions.Recovery;

internal sealed class CanonicalRecoveryEnvelopeFactory(
    int _outputReserveTokens = 8_000,
    int _mandatoryOverheadTokens = 2_000) : IRecoveryEnvelopeFactory
{
    public RecoveryEnvelopePayload Build(
        string attemptId,
        string scopeId,
        ProviderSessionReference original,
        RecoveryFailure failure,
        IReadOnlyList<RecoverySourceObservation> sources,
        SessionContinuityProfile profile,
        int contextBudget)
    {
        string marker = $"looprelay-recovery:{attemptId}";
        int certifiedMaximum = profile.MaximumRecoverableContext
            ?? throw new RecoveryEnvelopeException("MaximumRecoverableContext is Unknown; textual recovery is ineligible.");
        int boundedMaximum = Math.Min(certifiedMaximum, checked(contextBudget + _outputReserveTokens + _mandatoryOverheadTokens));
        RecoveryEnvelope envelope = new RecoveryEnvelopeBuilder().Build(
            marker,
            scopeId,
            original.ThreadId,
            sources,
            boundedMaximum,
            _outputReserveTokens,
            _mandatoryOverheadTokens);
        string prompt = RecoverDecisionSessionContext.Render(marker, envelope.CanonicalJson);
        return new RecoveryEnvelopePayload(
            marker,
            prompt,
            envelope.Digest,
            new Dictionary<string, string>
            {
                ["schema"] = envelope.SchemaVersion,
                ["completeness"] = envelope.Completeness.ToString(),
                ["estimated-tokens"] = envelope.EstimatedTokens.ToString(),
                ["source-count"] = envelope.Sources.Count.ToString(),
                ["item-count"] = envelope.Items.Count.ToString(),
                ["omission-count"] = envelope.Omissions.Count.ToString(),
                ["failure-classification"] = failure.Classification,
            });
    }
}
