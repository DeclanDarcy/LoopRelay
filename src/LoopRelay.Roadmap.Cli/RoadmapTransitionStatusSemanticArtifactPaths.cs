namespace LoopRelay.Roadmap.Cli;

internal static class RoadmapTransitionStatusSemanticArtifactPaths
{
    public const string Root = RepositoryWorkSemanticArtifactPaths.Root + "/transitions/status-report";
    public const string SubjectIdentity = Root + "/subject-identity.json";
    public const string ProtocolDefinition = Root + "/protocol-definition.json";
    public const string Ledger = Root + "/semantic-ledger.json";
    public const string LatestReport = Root + "/reports/latest.md";

    public static string Admission(string runId) => $"{Root}/admissions/admission.{runId}.json";

    public static string CurrentRoadmapStateSource(string runId) => $"{Root}/sources/current-roadmap-state.{runId}.json";

    public static string StatusObservation(string runId) => $"{Root}/observations/status-output.{runId}.md";

    public static string LegacyStatusObservation(string runId) => $"{Root}/observations/legacy-status-output.{runId}.md";

    public static string Evidence(string runId) => $"{Root}/evidence/status-evidence.{runId}.md";

    public static string Decision(string runId) => $"{Root}/decisions/report-only-decision.{runId}.json";

    public static string Equivalence(string runId) => $"{Root}/equivalence/equivalence.{runId}.json";

    public static string RunReport(string runId) => $"{Root}/reports/report.{runId}.md";
}
