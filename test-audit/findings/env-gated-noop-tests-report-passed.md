# Env-gated opt-in tests report "Passed" when they run nothing

## Classification

- Category: Safeguard without decision consumer
- Severity: Low
- Confidence: High
- Evidence level: Observed
- Scope: [CodexAppServerCertificationTests.cs:18,153](../../tests/LoopRelay.Agents.Compatibility.Tests/CodexAppServerCertificationTests.cs) (`LOOPRELAY_CODEX_CERT_BINARY`, `..._IDENTITY_PROBE`), [WorkspaceMagnitudeHarness.cs:67-71](../../tests/LoopRelay.Orchestration.Primitives.Tests/Measurement/WorkspaceMagnitudeHarness.cs) (`LOOPRELAY_MEASUREMENT_OUTPUT`)
- Affected tests: ~5 opt-in cases
- Affected production paths: none
- Primary cost: assurance-signal integrity (no runtime cost)
- Aggregate cost: ~0
- Wall-clock impact: none
- Invocation frequency: every run (as no-ops)
- Recommended disposition: Replace with stronger mechanism

## Summary

Live-certification and measurement tests gate on environment variables by early-returning, so a normal pass reports them as **Passed** although zero assertions executed. The suite's green summary silently includes coverage that never ran; a reader of results cannot distinguish "certified against a live codex binary" from "binary not configured". The repo has no `Skip=` usage at all, so this is the pattern everywhere gating exists.

## Evidence

Early returns at the cited lines; run evidence: Agents.Compatibility.Tests completes 6 cases in ~50–76 ms with the gated paths inert; trx records them as Passed. No CI exists, so the sole consumer of the pass/fail signal is a human reading local output — precisely the consumer misled by a no-op Pass.

## Current Execution Path

Test body checks env var → returns → xUnit records Pass.

## Intended Purpose

Keep expensive/environment-dependent live paths out of the routine gate while keeping them compilable and one env var away from running — a sound goal; only the reporting is wrong.

## Necessity Analysis

The gating itself is correct and should stay (live codex certification and the magnitude harness do not belong in the routine pass). The failure protected against — misreading absent live certification as performed — is reachable today and has a consumer (the operator reading results, and release evidence per docs/certification.md).

## Redundancy Analysis

Not applicable — reporting-semantics issue, no overlap.

## Recommended Remediation

Report non-execution as skipped rather than passed (owner: the gated test classes). Mechanisms available without new dependencies: `Assert.Skip` (xUnit 2.9 dynamic skip via `Xunit.SkippableFact`-equivalent is not built into 2.9.3 — verify; if unavailable, the conventional pattern is a `[Fact(Skip=...)]` pair plus an env-triggered live twin, or adopting `Xunit.SkippableFact`). Choose the lightest mechanism that makes trx/console show Skipped with the reason string naming the env var.

## Coverage-Preservation Plan

No behavioral coverage changes; the signal becomes truthful. Release-evidence readers gain an explicit skip count instead of inflated passes.

## Validation Plan

Run without env vars: cases report Skipped with reason; with env vars set: identical live behavior as today. trx diff confirms outcome classification only.

## Risks and Tradeoffs

If a package (SkippableFact) is needed, that is one new test dependency — weigh against the repo's minimal-dependency style; the paired-fact pattern avoids it at the cost of slight duplication. No other risks.

## Final Disposition

Replace with stronger mechanism
