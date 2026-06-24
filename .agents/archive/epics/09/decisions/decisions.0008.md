# Decisions

## Newly Authorized

- Accept the Milestone 3B transfer eligibility implementation as the correct result.
- Treat Milestone 3B eligibility as complete.
- Treat Milestone 3C continuity artifact as ready to begin in a future slice.
- Preserve the lifecycle boundary: policy answers whether the session should transfer; eligibility answers whether policy-directed transfer can safely happen now.
- Keep the lifecycle chain as analysis, then policy, then eligibility.
- Continuity artifacts must be treated as canonical governance-continuity transfer payloads.
- Continuity artifacts must not represent operational context ownership.
- Decision Sessions should produce and validate continuity artifacts.
- Continuity infrastructure may later consume continuity artifacts.
- Continuity artifacts should include artifact id, repository id, source session id, optional target session id, created timestamp, policy evaluation, metrics, economics, coherence, cache, decision references, reasoning references, operational context references, continuity fingerprint, and diagnostics.
- Continuity artifact tests should cover repository mismatch rejection, source session mismatch rejection, fingerprint validation, required references, deterministic artifact id format, schema version rejection, and read endpoint artifact projection.
