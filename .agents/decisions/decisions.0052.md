# Decisions: 2026-06-26 M0.4 Shell Mirror Governance Direction

These decisions capture only newly authorized direction from the user response following M0.4 Regression Weakening Guard Slice 0051.

## Authorized Decisions

1. Accept Slice 0051 as a correct M0.4 enforcement step.
   - Skipped or focused architecture regressions are regression weakening, not harmless test-runner behavior.
   - The protected governance chain is architectural invariant -> regression -> lifecycle -> governance guard.

2. Continue M0.4 with shell response mirror governance.
   - New Rust backend-shaped response mirrors are the next highest-risk architectural surface.
   - The guard should protect the M0.2 conclusion that backend serialized JSON is authoritative.

3. Make shell mirror detection inventory-aware.
   - The guard should classify shell response mirrors through `docs/shell-transport-classification.md`.
   - Approved shell-owned, transitional compatibility, and quarantined mirrors may be allowed when governed.
   - Unclassified mirrors should be treated as governance failures.

4. Validate consistency between Rust code and the shell transport inventory.
   - New mirrors appearing without inventory classification should fail governance.
   - Documented mirrors disappearing from code without inventory updates should also fail governance.

## Next Authorized Sequence

1. Stage the current M0.4 Slice 0051 changes, handoff rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
