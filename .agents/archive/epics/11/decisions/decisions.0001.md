# Decisions: 2026-06-26 Slice Response

These decisions capture only the newly authorized direction from the response to Milestone 0.1 Slice 0001.

## Authorized Decisions

1. The `primarySurfaceReachability` timeout repair is accepted as verifier strengthening, not weakening.
   - Rationale: the test consistently exercises a full app mount and measured near the default 5s timeout; a bounded 15s budget preserves the reachability assertion without hiding failures.

2. The `CS2012` .NET output contention must be treated as a managed verifier dependency/quarantine, not permanent tribal knowledge.
   - Required evidence: why contention exists, whether it is architectural or incidental, retirement condition, and whether isolated output paths would remove the restriction.
   - Current operating rule remains serialized `.NET` build/test execution until a verified isolation mechanism exists.

3. Rust build and Rust tests must be classified separately.
   - Rust build: healthy structural verifier.
   - Rust tests: command executes, but has no behavioral coverage because it discovers zero tests.

4. Missing CI must be recorded as absence of a CI verification baseline.
   - Preferred wording: local verification baseline exists; CI verification baseline does not yet exist.

5. Milestone 0.1 should continue through documentation and evidence slices before certification.
   - Slice 0002: Verification Inventory.
   - Slice 0003: Verification Governance.
   - Slice 0004: Certification.

6. Add a Verification Capability Matrix to the Milestone 0.1 evidence set even though it is not explicitly listed in the plan.
   - Purpose: map architectural concerns to current verifier protection, strength, and gaps so Milestones 0.2, 0.3, and later phases can show measurable protection gains.

## Explicit Non-Decisions

- No architectural migration is authorized by this response.
- No CI implementation is authorized yet.
- No shell behavioral test scope is authorized yet beyond recognizing the coverage gap.
