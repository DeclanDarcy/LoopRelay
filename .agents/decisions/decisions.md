# Decisions: 2026-06-26 M0.4 Enforcement Ordering

These decisions capture only newly authorized direction from the user response following M0.4 Governance Definition Slice 0050.

## Authorized Decisions

1. Accept the opening M0.4 slice as the correct governance foundation.
   - The accepted posture is durable governance artifacts first, then mechanical verification of their existence, then broader enforcement.
   - The initial M0.4 tests are intentionally metadata-existence guards rather than complete governance enforcement.

2. Treat the core M0.4 architectural outcome as explicit governance over architecture-affecting implementation.
   - Architecture-affecting implementation cannot rely solely on implementation changes for acceptance.
   - Decision records must connect implementation changes to evidence, compatibility impact, regression impact, rollback, and baseline updates.

3. Continue M0.4 enforcement in descending architectural risk.
   - First: disabled or weakened architectural regression detection.
   - Second: new shell response mirror detection.
   - Third: compatibility fields without decision records.
   - Fourth: active decision and evidence schema validation.

4. Distinguish governance failure classes in reporting.
   - Missing governance means an architecture-affecting change lacks a required decision or evidence package.
   - Invalid governance means a decision or evidence package exists but lacks required metadata, evidence, rollback, compatibility, regression, or baseline fields.
   - These categories should remain separate because they imply different remediation paths.

## Next Authorized Sequence

1. Stage the current M0.4 slice, handoff rotation, and this decision checkpoint.
2. Commit and push to `origin/dev`.
3. Stop executing after the push.
