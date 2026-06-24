# Decisions

## Newly Authorized

- Proceed with the final Milestone 10 end-to-end workflow fixture.
- The fixture should prove the full lifecycle:
  - `Execution`.
  - `Handoff`.
  - `Decision`.
  - `OperationalContext`.
  - `Commit`.
  - `Push`.
  - `Completed`.
  - `WorkSelection`.
- The fixture should include:
  - gate halting.
  - continuation progression.
  - preparation artifact creation.
  - restart/recovery.
  - diagnostics present.
  - health evidence present.
  - reports generated.
  - certification passes.
- The fixture should fail if workflow:
  - accepts or rejects handoff.
  - resolves decisions.
  - reviews or promotes context.
  - approves or executes commit.
  - approves or executes push.
  - selects next work.
  - crosses an open gate.
  - duplicates progression after restart.
  - duplicates preparation after restart.
- Preserve the established split:
  - reports summarize evidence.
  - certification proves evidence exists.
  - health describes dimensions.
  - none of them create authority.
- Open human gates remain valid blocked states; a correctly halted authority boundary is not inherently unhealthy.
