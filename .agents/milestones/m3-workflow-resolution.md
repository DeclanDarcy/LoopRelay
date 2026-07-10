# Milestone 3: Workflow and Stage Resolution

Objective: determine repository state, selected workflow, current stage, eligible transitions, blockers, and ambiguity without mutating the repository.

## Work

- [ ] Add repository observation under `src/LoopRelay.Orchestration.Primitives/Resolution`:
  - [ ] `RepositoryObservation`
  - [ ] `RepositoryObserver`
  - [ ] `StorageAuthoritySnapshot`
  - [ ] Observed workflow states.
  - [ ] Observed products.
  - [ ] Observed lifecycle rows.
  - [ ] Observed evidence.
  - [ ] Observed transition runs.
  - [ ] Observed Git facts.
  - [ ] Observed human interaction requirements.
- [ ] Add `StorageVerificationResult` with authority, usable authority, stale exports, conflicts, corruption, unsupported schema, unresolved references, partial transactions, and blocking conditions.
- [ ] Adapt current `WorkspaceVerificationService` into the resolution path.
- [ ] Keep all verification non-mutating.
- [ ] Add invocation-mode resolution:
  - [ ] Default chained mode.
  - [ ] Forced eval chain.
  - [ ] Forced traditional chain.
  - [ ] Bounded eval.
  - [ ] Bounded traditional.
  - [ ] Bounded plan.
  - [ ] Bounded execute.
- [ ] Implement default roadmap selection:
  - [ ] If one or more `.agents/evals/*.md` files exist, select `EvalRoadmap`.
  - [ ] Otherwise select `TraditionalRoadmap`.
  - [ ] Explicit flags override this rule.
- [ ] Implement workflow state resolution for each identity:
  - [ ] Absent.
  - [ ] Eligible to start.
  - [ ] Active.
  - [ ] Resumable.
  - [ ] Completed.
  - [ ] Blocked.
  - [ ] Waiting.
  - [ ] Cancelled.
  - [ ] Failed.
  - [ ] Invalid.
  - [ ] Ambiguous.
- [ ] Implement stage and transition eligibility from products, gate results, transition evidence, recovery state, and blockers.
- [ ] Ensure artifact existence alone never implies completion.
- [ ] Add an explanation model that records selected workflow, selected stage, eligible transitions, satisfied gates, unsatisfied gates, blockers, evidence, authority, ignored evidence, conflicts, and uncertainty.

## Acceptance

- [ ] Resolution tests cover fresh, partial, blocked, cancelled, failed, completed, legacy, SQLite, filesystem, mixed, corrupt, and ambiguous repositories.
- [ ] Resolution is deterministic and non-mutating.
- [ ] Default eval/traditional selection is covered.
