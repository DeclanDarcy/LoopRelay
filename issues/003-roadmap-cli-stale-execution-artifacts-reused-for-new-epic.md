# Roadmap CLI stale execution artifacts reused for new epic

## Finding

Promoting a new active epic overwrites `.agents/epic.md`, but previously generated milestone specs, operational context, and execution prompt are not invalidated. `EnsureExecutionReadinessAsync` skips regeneration when `.agents/operational_context.md` or `.agents/execution-prompt.md` already exists, and `InvariantValidator` only checks that execution prerequisites exist. It does not prove they were generated from the current active epic and current spec bundle.

## Impact

The execution bridge can run a prompt assembled for a previous epic against a newly promoted active epic. This can cause implementation work to target the wrong acceptance criteria, completion certification to reason over stale specs, or lifecycle state to report execution readiness when causal inputs have drifted.

## Evidence

- `PromoteActiveEpicAsync` updates `.agents/epic.md` and its lifecycle state only.
- `GenerateMilestoneSpecsAsync` writes specs but does not clear older specs first.
- `EnsureExecutionReadinessAsync` treats existing operational context and execution prompt as ready by presence.
- `InvariantValidator` checks for active epic, operational context, execution prompt, and at least one spec, but not hashes or provenance.

## Proposal

Model execution preparation artifacts as derived artifacts with explicit provenance.

A robust approach:

1. Create an execution-preparation manifest that records:
   - active epic path and SHA-256,
   - ordered spec paths and hashes,
   - operational context hash,
   - execution prompt hash,
   - generation timestamps and source transition IDs.
2. Whenever `.agents/epic.md` is promoted, mark all downstream execution-preparation artifacts as stale: specs, bundle manifest, operational context, execution prompt, `.agents/plan.md`, and `.agents/milestones/*.md`.
3. Regenerate operational context and execution prompt unless their manifest entries match the current active epic and ordered spec hash set.
4. Make `InvariantValidator` enforce manifest freshness for `ExecutionPromptReady` and `ExecutionLoop`.
5. Add tests for:
   - promoting a second epic after a first execution prep exists,
   - stale operational context being regenerated,
   - stale execution prompt being rejected before execution.

This aligns execution readiness with causal input freshness rather than artifact existence.

## Acceptance criteria

- A new active epic cannot run with specs or execution prompt generated for another epic.
- Execution readiness can be resumed only when derived artifact provenance matches current inputs.
- Regeneration is automatic for stale derived artifacts and blocked only for corrupted or unverifiable artifacts.
