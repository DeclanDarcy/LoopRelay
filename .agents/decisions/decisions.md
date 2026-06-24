# Decisions

## Newly Authorized

- Accept Milestone 1 as complete.
- Treat the global header workflow-projection change as closing the remaining workflow authority gap.
- Preserve the current authority boundary:
  - workflow projection is the canonical operational lifecycle timeline
  - execution, git, decision, reasoning, and continuity statuses are scoped domain evidence
  - repository, dashboard, header, and workspace surfaces consume lifecycle state but do not own it
- Treat shell command test infeasibility documentation as acceptable for Milestone 1 because backend endpoint coverage plus TypeScript client/UI coverage protect the meaningful workflow path.
- Enter Milestone 2 next.
- Start Milestone 2 with decision-session transfer execution and persisted recovery integration.
- Milestone 2 implementation order should be:
  - add decision-session transfer execution endpoint
  - add persisted recovery endpoint
  - add shell commands
  - add TypeScript client and hooks
  - add Governance UI controls
  - test endpoint behavior, action availability, and UI refresh
- Milestone 2 must consume workflow gates and required actions for operational linkage while keeping `CommandCenter.DecisionSessions` as the governance lifecycle authority.
