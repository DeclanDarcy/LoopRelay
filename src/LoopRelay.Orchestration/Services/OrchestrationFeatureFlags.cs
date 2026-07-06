namespace LoopRelay.Orchestration.Services;

/// <summary>
/// Hardening/certification feature flags (m10) for <see cref="RepositoryOrchestrator"/>. Every member's
/// default reproduces today's production behavior BYTE-FOR-BYTE, so a default-constructed instance is a
/// no-op overlay: a flag is only a switch a deployment flips to opt OUT of (or, for the fallback, INTO) a
/// behavior. Mirrors the <see cref="DecisionSessionRouterOptions"/> sealed-record idiom and is registered the
/// same way (<c>TryAddSingleton(new OrchestrationFeatureFlags())</c>), optionally bound from configuration.
/// </summary>
/// <param name="PersistentPlanningProcessEnabled">
/// When true (default, today's behavior) a Write/Revise planning turn runs against the held-open Operational
/// planning process (warm reuse). When false each planning turn runs as a fresh one-shot via
/// <c>RunOneShotAsync</c>; in one-shot mode Revise has no warm session, so it re-runs RevisePlan as its own
/// one-shot against the freshly persisted plan (see RepositoryOrchestrator for the documented semantics).
/// </param>
/// <param name="PersistentDecisionProcessReuseEnabled">
/// When true (default, today's behavior) the held-open zero-permission Decision process is reused across runs
/// (the warm fast-path). When false every decision run treats the process as unseeded — open, seed, propose,
/// then close — so no Decision process is held between runs. The read-only posture is unchanged either way.
/// </param>
/// <param name="TransferOnlyDecisionFallbackEnabled">
/// When false (default, today's behavior) the router decides Continue vs Transfer. When true the router's
/// verdict is forced to Transfer AFTER it is evaluated, while the existing safety downgrades (unseeded =&gt;
/// Continue, and the execution-gate deferral) still apply — so an unseeded or contended process still degrades
/// safely to warm reuse.
/// </param>
/// <param name="AutomaticCommitPushAfterExecuteEnabled">
/// When true (default, today's behavior) an Execute Plan run commits+pushes the planning/milestone artifacts.
/// When false the commit/push (and its <c>committed</c> frame) is skipped and the run proceeds with no commit,
/// leaving the operator to commit/push manually. m10 DECISION: a synchronous user-confirmation gate is
/// intentionally NOT added — it would break the 202 fire-and-forget Execute Plan contract — so this flag is the
/// off-switch instead. Scoped to the Execute Plan run only; the continuation path never commits/pushes today.
/// </param>
/// <param name="SandboxOperationalContextEvolutionEnabled">
/// When true (default, the Stage-2 cost behavior) a Transfer's <c>UpdateOperationalContext</c> evolution one-shot
/// runs in an isolated temp workspace seeded with ONLY <c>operational_context.md</c> (read/write) and
/// <c>operational_delta.md</c> (read); codex's <c>--cd</c> confines its <c>workspace-write</c> sandbox to that
/// directory, so it can no longer re-explore the whole repository each transfer (the dominant ~425k-token
/// transfer cost the economics investigation measured). The evolved context is copied back into the repo. When
/// false the evolution runs against the repository working directory (the pre-Stage-2 behavior) — the rollback
/// off-switch if a runtime ever needs the agent to see the full repo during the rewrite.
/// </param>
public sealed record OrchestrationFeatureFlags(
    bool PersistentPlanningProcessEnabled = true,
    bool PersistentDecisionProcessReuseEnabled = true,
    bool TransferOnlyDecisionFallbackEnabled = false,
    bool AutomaticCommitPushAfterExecuteEnabled = true,
    bool SandboxOperationalContextEvolutionEnabled = true);
