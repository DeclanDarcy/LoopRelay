namespace LoopRelay.Certification;

public enum CertificationCommandKind
{
    Projection,
    Deterministic,
    Live,
}

public sealed record CertificationCommandDefinition(
    string Name,
    string Purpose,
    CertificationCommandKind Kind,
    bool RequiresCli,
    bool RequiresProvider,
    string? EvidenceFile);

public static class CertificationCommandCatalog
{
    public const string CoverageLedger = "coverage-ledger";
    public const string PlatformProbe = "platform-probe";
    public const string StatusCanary = "status-canary";
    public const string PublicCliContracts = "public-cli-contracts";
    public const string ProviderProfile = "provider-profile";
    public const string TransitionRecovery = "transition-recovery";
    public const string PlanWorkflow = "plan-workflow";
    public const string ExecuteWorkflow = "execute-workflow";
    public const string GitPublication = "git-publication";
    public const string PersistenceLifecycle = "persistence-lifecycle";
    public const string TraditionalRoadmap = "traditional-roadmap";
    public const string EvalRoadmap = "eval-roadmap";
    public const string CompletionClosure = "completion-closure";
    public const string FailureOracleMatrix = "failure-oracle-matrix";
    public const string TraditionalFullChain = "traditional-full-chain";
    public const string EvalFullChain = "eval-full-chain";
    public const string ReleaseGate = "release-gate";

    public static IReadOnlyList<CertificationCommandDefinition> Commands { get; } =
    [
        new(CoverageLedger, "Project the production-derived certification coverage ledger.", CertificationCommandKind.Projection, false, false, null),
        new(PlatformProbe, "Probe the local filesystem, Git, encoding, and path contract.", CertificationCommandKind.Deterministic, false, false, "platform-<os>.latest.json"),
        new(StatusCanary, "Repeat the deterministic public status fixture and compare normalized behavior.", CertificationCommandKind.Deterministic, true, false, "status-canary.latest.json"),
        new(PublicCliContracts, "Exercise public CLI outcomes, mutation boundaries, and storage commands.", CertificationCommandKind.Deterministic, true, false, "public-cli-contracts.latest.json"),
        new(ProviderProfile, "Certify the exact Codex profile, posture, approvals, and scoped writes.", CertificationCommandKind.Live, false, true, "provider-profile.latest.json"),
        new(TransitionRecovery, "Exercise live interruption boundaries and recovery dispositions.", CertificationCommandKind.Live, true, true, "transition-recovery.latest.json"),
        new(PlanWorkflow, "Certify Plan transitions from both roadmap producers.", CertificationCommandKind.Live, true, true, "plan-workflow.latest.json"),
        new(ExecuteWorkflow, "Certify execution, continuity, decisions, and independent acceptance.", CertificationCommandKind.Live, true, true, "execute-workflow.latest.json"),
        new(GitPublication, "Certify parent and nested .agents publication semantics.", CertificationCommandKind.Deterministic, true, false, "git-publication.latest.json"),
        new(PersistenceLifecycle, "Certify storage initialization, verification, import, and failure handling.", CertificationCommandKind.Deterministic, true, false, "persistence-lifecycle.latest.json"),
        new(TraditionalRoadmap, "Certify the live TraditionalRoadmap transition chain to Plan entry.", CertificationCommandKind.Live, true, true, "traditional-roadmap.latest.json"),
        new(EvalRoadmap, "Certify the live EvalRoadmap transition chain to Plan entry.", CertificationCommandKind.Live, true, true, "eval-roadmap.latest.json"),
        new(CompletionClosure, "Certify completion, archival closure, continuity retirement, and rerun idempotency.", CertificationCommandKind.Live, true, true, "completion-closure.latest.json"),
        new(FailureOracleMatrix, "Audit maintained failure classes, recovery coverage, oracle controls, and governance.", CertificationCommandKind.Deterministic, false, false, "failure-oracle-matrix.latest.json"),
        new(TraditionalFullChain, "Run the TraditionalRoadmap-to-Plan-to-Execute full chain.", CertificationCommandKind.Live, true, true, "traditional-full-chain.latest.json"),
        new(EvalFullChain, "Run the EvalRoadmap-to-Plan-to-Execute full chain.", CertificationCommandKind.Live, true, true, "eval-full-chain.latest.json"),
        new(ReleaseGate, "Evaluate current durable evidence without running fixture campaigns.", CertificationCommandKind.Projection, false, false, "release-gate.latest.json"),
    ];

    public static bool IsKnown(string command) =>
        Commands.Any(item => string.Equals(item.Name, command, StringComparison.Ordinal));
}
