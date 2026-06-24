# Decisions

## Newly Authorized

- Treat Milestone 6B determinism and recovery certification slice as accepted.
- Proceed next with proof that workflow cannot mutate decision-session lifecycle state.
- Prove workflow mutation boundaries through public workflow/backend surfaces.
- Preserve the dependency boundary where `CommandCenter.DecisionSessions` does not reference `CommandCenter.Workflow`.
- After workflow mutation proof, build the end-to-end decision-session lifecycle fixture.
- The end-to-end fixture must prove the lifecycle sequence:
  - Create session.
  - Activate session.
  - Analyze.
  - Evaluate policy.
  - Evaluate eligibility.
  - Create continuity artifact.
  - Execute transfer if policy says `Transfer` and eligibility is `Eligible`.
  - Recover.
  - Project observability.
  - Consume through workflow.
  - Certify.
- The fixture is test evidence only.
- The fixture must not become a production shortcut orchestration API or production control path.
