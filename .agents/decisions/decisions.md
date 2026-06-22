# Decisions

## Newly Authorized

- M1 is complete and its exit criteria are satisfied.
- M1 closure is based on deterministic context, snapshots, diagnostics, validation, fingerprints, snapshot persistence, and proven downstream consumption through `DecisionDiscoveryService -> DecisionContext -> Repository Artifacts`.
- M2 remains in progress and should be closed before beginning M3.
- The implemented M2 discovery slice is aligned with the roadmap: candidates, signals, evidence, diagnostics, classification, persistence, lifecycle endpoints, promotion boundary, duplicate suppression, and self-artifact suppression all belong in M2.
- Promotion must remain only a candidate boundary transition during M2; it must not generate proposals.
- Candidate source fingerprints should remain source-item/excerpt scoped rather than whole-context scoped.
- Discovery should continue suppressing lifecycle self-artifacts as discovery inputs to prevent candidate feedback loops.
- Remaining M2 work is lifecycle hygiene: expiration hardening, duplicate lifecycle verification, dismissal validation, endpoint success-path coverage, terminal-state validation, and M2 certification review.
- The next slice should focus exclusively on closing M2 in this priority order: expiration tests, duplicate-marking tests, dismissal tests, endpoint success-path coverage, terminal candidate accumulation validation, then M2 certification review.
