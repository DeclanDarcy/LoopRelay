# Decisions: 2026-06-27 Phase 0 Agent Process Boundary Acceptance

These decisions capture only newly authorized direction from the user response accepting the Phase 0 agent process boundary slice.

## Authorized Decisions

1. Accept the Phase 0 agent process boundary slice.
   - `IAgentProcess` is accepted as the role-agnostic live process handle boundary.
   - The current layering remains accepted: `ProcessRunner`, then `IAgentProcess`, then future supervision, future runtime, and Repository Runtime.

2. Treat `IAgentProcess` as a narrow architectural invariant.
   - A process handle may expose process facts and mechanics such as start-related identity, completion, standard input and output interaction, cancellation, and disposal.
   - It must not own retries, ownership policy, health policy, restart decisions, session routing, or repository coordination.

3. Keep `IProcessRunner` as a temporary compatibility boundary.
   - Execution and Git may continue using `IProcessRunner` until consumers are deliberately migrated.
   - `IProcessRunner` should eventually become a thin adapter over newer process abstractions or be retired; it must not become a permanent second process-launch authority.

4. Keep provider-semantic executable resolution out of `CommandCenter.Agents`.
   - The extraction criterion is whether executable discovery expresses provider semantics, not whether it launches or locates a process.
   - `CodexExecutableResolver` remains in Execution while it returns provider-specific models or provider-specific error types.
   - Future decomposition may introduce provider-neutral executable discovery below Execution, with Codex adaptation remaining in Execution.

5. Sequence process supervision before stream/event primitives and `IAgentRuntime`.
   - The next layer should be process supervision over `IAgentProcess`.
   - Streams should reflect the supervisor's authoritative lifecycle state rather than define process lifecycle semantics.

6. Model supervision as a state machine, not as generic monitoring.
   - Supervision should own valid lifecycle transitions such as created, starting, running, stopping, completed, failed, cancelled, and disposed.
   - Monitoring, metrics, and notifications should be consequences of lifecycle transitions.

7. Keep supervision scoped to one process lifecycle.
   - `IAgentProcess` owns one process.
   - Process supervision owns one process lifecycle.
   - `IAgentRuntime` will own many supervised processes.
   - Repository Runtime will own repository coordination.
   - The supervision layer must not accumulate session registry, process lookup, repository ownership, routing decisions, or telemetry aggregation responsibilities.

8. Preserve the environment-test isolation improvement.
   - Tests mutating process-wide `COMMAND_CENTER_*` environment variables should remain isolated from parallel test execution.

## Next Authorized Sequence

1. Stage the completed Phase 0 agent process boundary slice and this decision rotation.
2. Commit on `dev`.
3. Push to `origin/dev`.
4. Stop executing after the push.
