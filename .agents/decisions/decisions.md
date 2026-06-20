# Decisions

## Newly Authorized Decisions

- Epic 2 appears functionally complete pending final certification.
- M8.3 is authorized as Epic 2 Final Certification.
- M8.3 must not expand Epic 2 functionality.
- M8.3 should run an Epic 2 exit review before starting Epic 3.
- M8.3 should include exactly one realistic smoke test, not exhaustive additional coverage.
- The real smoke test should exercise:
  - Real provider invocation.
  - Real repository filesystem behavior.
  - Real Git status, commit, and push assumptions.
- M8.3 should verify the six exit-review questions:
  - Valid execution can be launched.
  - Execution can be observed.
  - Execution output can be reviewed through handoff.
  - Acceptance remains independent from provider completion.
  - Reviewed work can safely become repository history.
  - The cycle can run again without manual reconstruction.
- If final certification passes cleanly, Epic 2 should be considered certified and closed.

## Explicitly Deferred

- Starting Epic 3 before Epic 2 final certification.
- Expanding the execution system during final certification.
- Exhaustive real-provider testing beyond one realistic smoke test.
