# Decisions

## Newly Authorized Decisions

- M3 is formally closed.
- The core M3 outcome is the certified execution observability lifecycle: `Executing`, `Completed`, `Failed`, and `Cancelled`.
- Recovery semantics are now considered certified: providers with `SupportsReattach = true` may recover, while providers with `SupportsReattach = false` produce explicit orphan failure.
- M4 may focus on execution completion semantics rather than process semantics.
- M4A.1 is authorized as the next slice.
- M4A.1 must implement handoff validation, completion processing, and the `AwaitingAcceptance` transition.
- Provider completion is necessary but not sufficient for successful execution.
- `Completed` must mean session completion, not accepted or successful execution.
- Session completion and execution acceptance are separate concepts.
- Completion processing should move out of monitoring.
- Monitoring should report provider exit and session completion, then stop.
- Handoff logic should own the transition from `Completed` through handoff validation to `AwaitingAcceptance` or `Failed`.
- Success path: provider exit code `0`, session `Completed`, `.agents/handoffs/handoff.md` exists, repository state becomes `AwaitingAcceptance`, and session state remains `Completed`.
- Failure path: provider exit code `0`, session `Completed`, `.agents/handoffs/handoff.md` missing, repository state becomes `Failed`, with a stable explicit failure reason equivalent to `Execution completed but no current handoff was found.`
- Non-zero provider exit must not enter handoff validation.
- Cancellation must not enter handoff validation.
- M4A.2 should remain separate and handle previous handoff preservation into `handoff.NNNN.md`.
- M4A.3 should follow validation and preservation with `AwaitingAcceptance` projection, restart restoration, and handoff review surface.

## Explicitly Deferred

- Do not combine M4A.1 handoff validation with historical handoff archive preservation.
- Do not treat provider `Completed` as execution success.
- Do not begin acceptance workflow semantics before handoff validation and `AwaitingAcceptance` are stable.
