# Decisions: 2026-06-26 M0.3 Certification Direction

These decisions capture only newly authorized direction from the user response following Slice 0046.

## Authorized Decisions

1. Accept Slice 0046 as the final framework-building slice for M0.3.
   - The shell classification slice completes governance coverage across backend, frontend, and shell.
   - Do not add more framework concepts before certification unless certification exposes a concrete gap.

2. Treat M0.3 as framework-complete but not enforcement-complete.
   - The governance model exists across the major architectural surfaces.
   - Broad architectural enforcement remains later milestone work.

3. Preserve the current shell classification boundaries.
   - Shell-owned responsibilities stay narrowly scoped to sidecar lifecycle, backend metadata/health, and native repository selection.
   - Rust mirrors remain transitional compatibility with target state `Retired`.

4. Preserve `ErrorResponse` as quarantined compatibility.
   - Keep the quarantine until typed transport error preservation exists.
   - Do not normalize `ErrorResponse` as a permanent shell architectural object.

5. Proceed next with M0.3 certification.
   - The certification slice should map required outputs, implemented framework, executable guards, accepted limitations, and remaining blockers against M0.3 exit criteria.
   - The certification package should mirror the M0.2 closeout pattern.

6. Organize M0.3 certification around framework completeness rather than document completeness.
   - Certification should show whether each framework area is coherent and protected.
   - At minimum, map invariants, taxonomy, ownership, severity, drift model, confidence, lifecycle, failure UX, backend discoverability, frontend discoverability, and shell discoverability to status and evidence.

7. Maintain the distinction between framework completeness and enforcement completeness in certification language.
   - M0.3 primarily certifies the governance/regression framework.
   - Later milestones expand enforcement breadth and implementation migrations.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0046 plus this decision checkpoint.
2. Stop executing after the push.
