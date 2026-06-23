namespace CommandCenter.Decisions.Models;

public sealed record DecisionGenerationCertificationResult(
    bool GenerationCertified,
    bool GovernanceCertified,
    bool ThroughputCertified,
    bool QualityCertified,
    bool ConsumptionCertified,
    bool WorkflowReplacementCertified,
    IReadOnlyList<DecisionGenerationCertificationFinding> Findings,
    IReadOnlyList<string> Failures)
{
    public bool Certified =>
        GenerationCertified &&
        GovernanceCertified &&
        ThroughputCertified &&
        QualityCertified &&
        ConsumptionCertified &&
        WorkflowReplacementCertified &&
        Failures.Count == 0;
}
