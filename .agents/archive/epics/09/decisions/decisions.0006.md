# Decisions

## Newly Authorized

- Treat Milestone 2 as complete and structurally aligned with the intended authority model.
- Treat Milestone 3 as ready to begin.
- Enforce lifecycle policy as the first true decision layer after analysis.
- Lifecycle policy may consume metrics, economics, coherence, and transfer pressure.
- Lifecycle policy may produce only a `Continue` or `Transfer` governance decision with explanation.
- Lifecycle policy owns reuse score, transfer score, decision, reason, and contributing factors.
- Lifecycle policy must not own eligibility, transfer execution, registry mutation, session retirement, replacement creation, continuity artifacts, hosted services, or recovery changes.
- Shape the first Milestone 3A slice around `DecisionSessionLifecycleEvaluation`, reuse score, transfer score, decision, reason, contributing factors, and `EvaluatedAt`.
- Add `IDecisionSessionLifecyclePolicy` and `DecisionSessionLifecyclePolicy` for Milestone 3A.
- Persist policy evaluation snapshots at `.agents/decision-sessions/lifecycle/policy/snapshot.json`.
- Add read-only `GET /lifecycle/policy` for Milestone 3A.
- Persist both reuse score and transfer score in policy diagnostics even when one clearly wins.
- Do not collapse transfer pressure into the final lifecycle decision; transfer pressure remains an input signal.
- Do not add eligibility, transfer execution, mutation, hosted services, or recovery changes in the first policy slice.
- No roadmap corrections are indicated.
