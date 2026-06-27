# Decisions: 2026-06-26 M0.3 Invariant Catalog Direction

These decisions capture only newly authorized direction from the user response following Slice 0036.

## Authorized Decisions

1. Accept the M0.3 opening skeleton as correctly scoped.
   - The first M0.3 slice should remain a framework skeleton rather than immediately expanding the regression catalog.
   - The current checks are accepted as framework checks: mechanism discoverability, mechanism metadata, and fixture output wiring.

2. Treat architectural mechanisms as regression targets.
   - M0.3 should protect the existence and integrity of mechanisms introduced by earlier milestones.
   - Mechanism disappearance, unregistered mechanisms, or broken wiring are architectural drift.

3. Require architectural regressions to carry intent and remediation.
   - Regression failures should explain the invariant that no longer holds.
   - Regression failures should also explain the expected restoration path.

4. Make the invariant catalog the canonical mapping for M0.3.
   - The catalog should map invariant, protecting mechanism, owner, severity, evidence, drift model, current coverage, and enforcement strength.
   - Later regression implementations should consume or align to this catalog rather than inventing disconnected classifications.

5. Add enforcement strength to the invariant catalog.
   - Enforcement strength distinguishes documentation, inventory, executable regression, runtime enforcement, and multiple-mechanism protection.
   - This column should expose which architectural principles remain convention-based and which are mechanically enforced.

6. Protect the catalog with a catalog regression.
   - The regression should verify required columns exist.
   - It should verify required metadata is populated.
   - Every invariant should have an owner.
   - Every invariant should have an intended protecting mechanism.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0036 plus this decision checkpoint.
2. Stop executing after the push.
