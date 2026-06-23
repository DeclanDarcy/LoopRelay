# Milestone 0: Baseline Certification and Contract Hardening

## Goal

establish the implementation baseline and lock the authority boundaries before replacing the shallow generation path.

## Work

- [ ] Run the current backend and UI test suites to capture a starting point.
- [ ] Document the current decision lifecycle contract in `docs/` if any public behavior is not already documented by tests.
- [ ] Add characterization tests proving existing behavior that must not regress:
  - [ ] repository-backed structured artifacts reload after service restart
  - [ ] candidates and proposals do not mutate operational context
  - [ ] only human resolution creates authoritative decisions
  - [ ] unresolved proposals do not project to execution
  - [ ] accepted resolved decisions with blocking governance findings do not project
  - [ ] proposal markdown is projection only
- [ ] Add schema migration tests for reading existing candidate, proposal, decision, governance, certification, and projection artifacts after new optional fields are introduced.
- [ ] Add explicit tests that `DecisionGenerationService` no longer recommends by option order once the new pipeline is introduced.
- [ ] Add the first burden-classification tests:
  - [ ] generated recommendation accepted unchanged is `ReviewOnly`
  - [ ] generated recommendation accepted after small structured refinement is `MinorEdit`
  - [ ] generated recommendation replaced through extensive refinement is `MajorRefinement`
  - [ ] human-authored replacement content is `FullRewrite`
  - [ ] manual decision creation outside generation is `GenerationBypassed`

## Exit Criteria

- [ ] Current behavior is covered by regression tests.
- [ ] All authority boundaries are protected by tests.
- [ ] New schema additions have a compatibility strategy.
- [ ] The repository can record human authoring burden without changing lifecycle authority.
- [ ] The implementation can proceed without rebuilding already working lifecycle infrastructure.
