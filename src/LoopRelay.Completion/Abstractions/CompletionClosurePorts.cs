using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Parsing;

namespace LoopRelay.Completion.Abstractions;

public sealed record CertifiedCompletionCandidate(
    CompletionEvaluationDecision Evaluation,
    CompletionCertificationRoute Route,
    IReadOnlyList<string> EvidenceIdentities);

public interface ICertifiedCompletionCandidateSink
{
    Task PersistAsync(CertifiedCompletionCandidate candidate, CancellationToken cancellationToken);
}

public interface ICompletionContextMaterializer
{
    Task<string> MaterializeAsync(
        string roadmapCompletionContextPath,
        string evidenceDirectory,
        string evidenceStem,
        string content,
        CancellationToken cancellationToken);
}
