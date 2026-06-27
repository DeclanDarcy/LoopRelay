# Decisions: 2026-06-26 M0.3 Shell Classification Direction

These decisions capture only newly authorized direction from the user response following Slice 0045.

## Authorized Decisions

1. Accept the M0.3 frontend regression skeleton as the correct point to introduce frontend-side framework structure.
   - The frontend slice should be treated as a framework slice, not a broad frontend rule-enforcement slice.
   - The accepted scope is discoverability, location, and ownership.

2. Preserve a shared governance model across backend and frontend while allowing different implementation mechanisms.
   - Backend and frontend architecture regressions should use the same metadata language.
   - Implementation mechanisms may differ by ecosystem.

3. Require future frontend regressions to participate in the M0.3 metadata model.
   - Future frontend rules must name invariant, mechanism, owner, severity, drift, confidence, lifecycle, evidence, and certification use.
   - Do not create a separate frontend governance language.

4. Treat the backend discoverability guard for the frontend skeleton as appropriate framework protection.
   - The regression framework itself is an architectural object whose integrity should be protected.

5. Proceed next with shell regression classification.
   - Inventory shell command families.
   - Inventory Rust domain mirrors.
   - Classify responsibilities.
   - Add a minimal metadata/discoverability guard.
   - Do not change shell behavior during this slice.

6. Use four shell command-family responsibility categories during classification.
   - Passive transport: forwards backend requests and responses.
   - Shell-owned operations: native dialogs, lifecycle, filesystem, and other shell responsibilities.
   - Transitional compatibility: temporary mirrors and legacy adapters.
   - Unknown / requires review: inventory result that needs later classification.

7. Record two orthogonal properties for Rust mirrors.
   - Current state: Passive, Mirror, Compatibility, or Unknown.
   - Target state: Passive, Shell-owned, Retired, or Quarantined.
   - The mirror inventory should support future migration planning without becoming an implementation plan.

8. Treat M0.3 as spanning the three major architectural surfaces after the shell classification slice.
   - Backend framework and frontend framework skeleton are in place.
   - Shell classification is the remaining major surface before broader executable protection and certification.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0045 plus this decision checkpoint.
2. Stop executing after the push.
