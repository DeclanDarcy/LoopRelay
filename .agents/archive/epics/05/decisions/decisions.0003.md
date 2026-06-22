# Decisions

## Newly Authorized

- M0C is accepted as complete; no projection authority leakage or lifecycle boundary violation is apparent from the completed slice.
- M0 remains in progress and should not advance to M1 until M0D is complete and certified.
- M0D should focus on JSON-authority recovery: structured records may regenerate missing markdown projections, but markdown must not reconstruct lifecycle authority unless explicitly designed later.
- M0D implementation order is: structured-record discovery, projection regeneration service, missing-markdown recovery, repository restart recovery, projection equivalence verification, full M0 regression suite, and M0 certification review.
- M0D certification should prove that creating structured artifacts, generating projections, deleting projections, restarting services, regenerating projections, and verifying equivalence succeeds.
- Regeneration determinism is foundational for later governance and lifecycle certification work.
