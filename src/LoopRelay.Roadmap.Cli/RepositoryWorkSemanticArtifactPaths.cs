namespace LoopRelay.Roadmap.Cli;

internal static class RepositoryWorkSemanticArtifactPaths
{
    public const string Root = RoadmapArtifactPaths.AgentsDirectory + "/semantic/repository-work";
    public const string SubjectIdentity = Root + "/subject-identity.json";
    public const string ProtocolDefinition = Root + "/protocol-definition.json";
    public const string Ledger = Root + "/semantic-ledger.json";
    public const string CapturedSourceView = Root + "/captured-source-view.md";
    public const string CurrentSummary = Root + "/current-semantic-summary.md";
    public const string CurrentState = Root + "/state.json";
    public const string CurrentUnderstanding = Root + "/current-understanding.md";
    public const string CapabilityDeclaration = Root + "/capability-declaration.json";
    public const string CapabilityConformanceReport = Root + "/capability-conformance-report.md";
    public const string CompletionCertification = Root + "/completion-certification.json";
    public const string LatestReport = Root + "/reports/latest.md";

    public static string Admission(string runId) => $"{Root}/admissions/admission.{runId}.json";

    public static string Observation(string runId) => $"{Root}/observations/observation.{runId}.md";

    public static string Evidence(string runId) => $"{Root}/evidence/evidence.{runId}.md";

    public static string CandidateSummary(int version) => $"{Root}/candidates/current-semantic-summary.v{version:0000}.md";

    public static string SummaryVersion(int version) => $"{Root}/versions/current-semantic-summary.v{version:0000}.md";

    public static string UnderstandingVersion(int version) => $"{Root}/versions/current-understanding.v{version:0000}.md";

    public static string Blocker(string runId) => $"{Root}/blockers/blocker.{runId}.md";

    public static string RecoveryReview(string runId) => $"{Root}/recovery/recovery-review.{runId}.md";

    public static string RunReport(string runId) => $"{Root}/reports/report.{runId}.md";
}
