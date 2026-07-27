# The certification coverage-ledger canary fails deterministically at HEAD

## Classification

- Category: Safeguard without decision consumer, Historical residue
- Severity: Medium (assurance integrity, not runtime)
- Confidence: High
- Evidence level: Observed
- Scope: `StatusCanaryTests.CoverageLedgerIsProductionDerivedAndKeepsUncoveredSetVisible` (tests/LoopRelay.Certification.Tests/StatusCanaryTests.cs)
- Affected tests: 1 case; the credibility of the full-suite green gate
- Affected production paths: certification coverage-ledger derivation
- Primary cost: gate integrity (451 ms runtime)
- Aggregate cost: negligible
- Wall-clock impact: none
- Invocation frequency: every run — failing every time
- Recommended disposition: Retain with explanation

## Summary

The suite is red at the frozen revision: this canary fails identically in two full runs and in isolation (451 ms, `Assert.Contains` filter not matched — an expected `catalog-obligation` entry, EffectCoordinator/EvalRoadmap family, is missing from the derived coverage ledger). A permanently red canary inverts its purpose: with no CI, the only gate is a human reading local output, and a known-red suite trains that reader to ignore failures — the canary currently has no effective decision consumer.

## Evidence

Run 1 and run 2 full passes: `Failed: 1`, same test. Isolated rerun (`dotnet test --filter`, 451 ms): same `Assert.Contains() Failure: Filter not matched in collection`, listing Uncovered obligations for `EffectCoordinator/effect/EvalRoadmap/...` identities. Deterministic — not flaky, not environment-dependent. Whether the drift is in the ledger, the catalog, or the test's expectation is **not established by this audit** (that determination is repair work, out of audit scope).

## Current Execution Path

Canary derives the coverage ledger from production catalog/coverage machinery and asserts specific obligations appear with expected levels; the derivation and expectation have drifted apart at some commit not identified here.

## Intended Purpose

Keep certification coverage honest: the ledger must be production-derived and must keep uncovered obligations visible rather than silently dropping them — a guard against certification evidence overstating coverage.

## Necessity Analysis

The protected failure (silent shrinkage of the visible-uncovered set) is reachable and consequential for release evidence. The canary is the right mechanism — but only if green-by-default. Its current red state is the defect to resolve; deleting it would remove real assurance.

## Redundancy Analysis

None identified — no other test asserts ledger completeness/visibility.

## Recommended Remediation

Retain the canary; repair the drift as ordinary (non-audit) work: determine whether the EvalRoadmap effect obligations were legitimately retired (update the expectation) or the derivation dropped them (fix the ledger). Until repaired, the suite's exit code cannot serve as a gate; that repair should precede or accompany any remediation from this audit so before/after validation runs have a green baseline.

## Coverage-Preservation Plan

No removal. Repair restores the gate; the audit's other validation plans depend on a green baseline and list this as a sequencing dependency.

## Validation Plan

After repair: full suite green twice; deliberately remove one covered obligation locally (throwaway perturbation) and confirm the canary fails — proving it still detects its failure class.

## Risks and Tradeoffs

Only the sequencing cost of repairing before other remediations are validated. Leaving it red is the risky option (gate erosion).

## Final Disposition

Retain with explanation
