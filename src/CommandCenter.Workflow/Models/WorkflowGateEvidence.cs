namespace CommandCenter.Workflow.Models;

public sealed record WorkflowGateEvidence(
    string SourceDomain,
    string SourceArtifact,
    string Summary,
    DateTimeOffset ObservedAt,
    string Fingerprint);
