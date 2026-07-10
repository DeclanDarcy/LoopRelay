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

## Detail Requirements

### Freeze Scope

M3 freezes repository resolution behavior. Later milestones consume this model rather than redefining storage verification, repository observation, workflow identity resolution, workflow state resolution, stage and transition eligibility, blocker handling, ambiguity handling, human interaction requirements, repository classification, or explanation semantics.

### Resolution Boundary

M3 determines what can execute and why. It should not execute workflows, chain workflows, migrate workflows, redesign persistence, redesign prompts, or redesign recovery.

### Repository Observation Categories

Repository observation should produce an immutable snapshot with no interpretation. It should observe:

- storage
- workflow artifacts
- lifecycle
- evidence
- journals
- Git
- prompt contracts
- projection manifests
- completion artifacts
- decision state
- operational context
- repository metadata
- environment

### Storage Verification Result

Storage verification should include authority, usable authority, confidence qualifier, blocking conditions, observed conflicts, stale exports, corruption, unsupported schema, unresolved references, and partial workflow transactions.

Verification is automatic and read-only. Repair is never automatic.

### Workflow Identity Resolution Output

Workflow identity resolution should produce identity, evidence, authority, and reasoning. Explicit CLI mode overrides automatic detection; otherwise `.agents/evals/*.md` selects EvalRoadmap and absence selects TraditionalRoadmap. Single-workflow invocations retain explicit identity.

### Eligibility Is Not Selection

Workflow selection determines which workflow is under consideration. Workflow eligibility independently determines whether that workflow may execute. Eligibility states are `Eligible`, `Blocked`, `Waiting`, `Completed`, `Cancelled`, `Failed`, `Invalid`, and `Ambiguous`.

### Workflow State Resolution

Workflow state resolution should reconstruct current workflow state, completed stages, incomplete stages, blocked stages, recovery state, and workflow outcome from repository evidence.

### Transition Eligibility Output

Transition eligibility should output eligible transitions, blocked transitions, waiting transitions, and invalid transitions. It should not choose a transition.

### Blocker Model

Blocker categories include storage, workflow, stage, transition, validation, human, permission, recovery, and repository. Every blocker should include evidence, authority, required action, and recovery possibility.

### Ambiguity Model

Ambiguity categories include workflow ambiguity, stage ambiguity, authority ambiguity, repository ambiguity, storage ambiguity, recovery ambiguity, and completion ambiguity. Ambiguity must never silently resolve.

### Explainability Model

Every resolution decision should record decision, evidence, authority, supporting facts, ignored facts, conflicting facts, confidence qualifier, and remaining uncertainty.

### Repository Classification

M3 should produce a canonical repository classification independent of workflow implementation:

- Fresh
- In Progress
- Blocked
- Waiting
- Completed
- Cancelled
- Failed
- Ambiguous
- Corrupt
- Unsupported

### Human Interaction Requirement

Resolution should explicitly represent required human interaction with reason, authority, and blocking scope. Categories include approval, review, roadmap revision, strategic investigation, permission, evidence repair, and completion decisions.

## Acceptance

- [ ] Resolution tests cover fresh, partial, blocked, cancelled, failed, completed, legacy, SQLite, filesystem, mixed, corrupt, and ambiguous repositories.
- [ ] Resolution is deterministic and non-mutating.
- [ ] Default eval/traditional selection is covered.
