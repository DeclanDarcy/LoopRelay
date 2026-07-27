# Process-global environment mutation inside a parallel assembly

## Classification

- Category: Parallelization contention
- Severity: Medium (latent)
- Confidence: High
- Evidence level: Observed (mechanism); no observed failure yet
- Scope: [SessionTelemetryRecorderTests.cs:195-216](../../tests/LoopRelay.Cli.Tests/Services/Telemetry/SessionTelemetryRecorderTests.cs) in Cli.Tests
- Affected tests: the mutating tests plus any concurrently running test that reads `LOOPRELAY_CERTIFICATION_INVOCATION_ID`/`_ROLE`
- Affected production paths: certification-invocation env detection
- Primary cost: flakiness risk under parallelism
- Aggregate cost: none
- Wall-clock impact: none
- Invocation frequency: every run
- Recommended disposition: Relocate

## Summary

`SessionTelemetryRecorderTests` sets process-global environment variables (with try/finally restore) inside Cli.Tests, which runs with default xUnit parallelism. Any concurrently scheduled test that reads those variables can observe the mutation. Agents.Tests solved the identical problem with the `"ProcessEnvironment"` serialized collection; Cli.Tests has no equivalent. The hazard becomes more probable after the recommended Cli.Tests class split increases concurrency.

## Evidence

`Environment.SetEnvironmentVariable("LOOPRELAY_CERTIFICATION_INVOCATION_ID"/"_ROLE", …)` at `:199-200,215-216` with capture/restore at `:195-196`; Cli.Tests has no runner.json and the class is in no serialized collection. Precedent: `ProcessEnvironmentCollection.cs:10` (Agents.Tests) exists for exactly this class of state. No observed failure in the two audited runs — latent, not active.

## Current Execution Path

xUnit schedules the class concurrently with other Cli.Tests collections; env mutation is process-wide for the test's duration.

## Intended Purpose

Prove the telemetry recorder honors certification-invocation environment context.

## Necessity Analysis

The env mutation is intrinsic to the behavior under test (production reads the process environment); the *exposure* is not — exclusivity during the mutation is the missing piece. Reachable failure: cross-test contamination misclassifying a concurrent test's telemetry context; consequence today is a confusing intermittent failure.

## Redundancy Analysis

None — no other mechanism serializes these tests.

## Recommended Remediation

Add a `[CollectionDefinition("CliProcessEnvironment", DisableParallelization = true)]` in Cli.Tests and place this class in it (owner: Cli.Tests), mirroring the Agents.Tests pattern and its rationale comment. Alternative (better long-term, larger change): a production seam for environment reading — not warranted for one class at pre-MVP.

## Coverage-Preservation Plan

No assertion changes; the class merely runs exclusively. No coverage or localization change.

## Validation Plan

Repeated parallel full runs of Cli.Tests (≥10) before the class-split remediation lands; then again after. Success: zero env-related intermittents.

## Risks and Tradeoffs

Trivial: the class runs exclusively (its ~1 s cost is negligible). This is a prerequisite ordering constraint for [cli-monolith-classes-serialize-suite-critical-path.md](cli-monolith-classes-serialize-suite-critical-path.md).

## Final Disposition

Relocate
