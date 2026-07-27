# Wall-clock-dependent assertions and sleeps in otherwise deterministic tests

## Classification

- Category: Fixed wait, Flakiness compensation
- Severity: Low-Medium
- Confidence: High
- Evidence level: Observed
- Scope: 4 sites in tests
- Affected tests: `CodexUsageProbeTests` ([:111-118](../../tests/LoopRelay.Cli.Tests/Services/Usage/CodexUsageProbeTests.cs)), `CausalUlidTests` ([:44](../../tests/LoopRelay.Core.Tests/Models/Identity/CausalUlidTests.cs)), `CertificationFailureDiagnosisTests` ([:381](../../tests/LoopRelay.Certification.Tests/CertificationFailureDiagnosisTests.cs)), `InputWaitProgressAgentRuntimeTests` ([:97,126](../../tests/LoopRelay.Cli.Tests/Services/Agents/InputWaitProgressAgentRuntimeTests.cs))
- Affected production paths: none (the production code under test already exposes injectable clocks/delay seams elsewhere)
- Primary cost: flakiness risk; minor fixed waits
- Aggregate cost: <1 s of deliberate sleeping per run
- Wall-clock impact: negligible
- Invocation frequency: every run
- Recommended disposition: Replace with stronger mechanism

## Summary

Four tests depend on real elapsed time where the suite otherwise uses injected clocks and recorded delays: a genuine performance-threshold assertion (`sw.Elapsed < 5 s` around a 100 ms scrape timeout), a 50 ms sleep to force a ULID millisecond boundary, a 100 ms real cancellation timer, and 25 ms delays to sequence chunk/completion interleavings. Under machine load these can fail or (worse) silently stop exercising the intended interleaving; the repo has no test-retry machinery (good), so any flake is a red run.

## Evidence

Sites above, verbatim: `Stopwatch.StartNew(); … Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), "read should have timed out quickly")` with `scrapeTimeout: 100 ms`; `await Task.Delay(50)` gating ULID ordering; `new CancellationTokenSource(TimeSpan.FromMilliseconds(100))` driving operator-cancel against an infinitely waiting agent; `await Task.Delay(25)` inside scripted fake runtimes. Contrast: `CodexUsageProbeCachingTests` and `UsageLimitDetectorTests` already use injected `monotonicNow`/recorded delays — the deterministic pattern exists in-repo. No flakiness-driven commits exist in history (no compensation debt yet).

## Current Execution Path

Each test runs real timers/sleeps inside otherwise deterministic in-proc arrangements; two run inside the (currently serialized or default-parallel) assemblies where CPU contention from the 32-thread suite is routine — observed run-to-run duration swings up to 3× on loaded runs.

## Intended Purpose

- Probe test: prove reads time out promptly (regression against a hang).
- ULID test: prove ordering across a timestamp boundary.
- Diagnosis test: prove operator cancellation interrupts a waiting agent.
- InputWait tests: prove chunk/completion ordering handling.

## Necessity Analysis

Each protected behavior is real and reachable; none *requires* wall clock. Timeout-promptness can assert the fake's recorded delay/cancellation rather than elapsed seconds; ULID boundary can be forced via the injectable clock (CausalUlid takes a timestamp source — the production seam exists); cancellation can use an untimed CTS cancelled after a deterministic signal; interleavings can be sequenced by completion sources rather than delays.

## Redundancy Analysis

None identified — each protects a distinct behavior; the issue is mechanism, not overlap.

## Recommended Remediation

Replace timing dependence with the repo's existing seams (owner: respective test classes): injected clock for ULID; scripted signal/`TaskCompletionSource` ordering for interleavings and cancellation; recorded-delay assertions (and generous ceiling only as a deadlock guard, not a threshold) for the probe test.

## Coverage-Preservation Plan

Same behaviors asserted deterministically. The probe's "times out quickly" contract loses its real-time character — acceptable: promptness under real time is an environment property, and the 5 s ceiling remains as a hang guard. No boundary or environment coverage changes.

## Validation Plan

Run each modified test 50× locally under parallel load (`dotnet test --filter`), zero failures; verify each still fails when its production behavior is deliberately broken (temporary working-tree perturbation, reverted).

## Risks and Tradeoffs

Determinism vs realism: the 50 ms/100 ms real waits do prove real-time behavior once; the injected-seam versions prove logic only. Given no CI and frequent loaded local runs, determinism wins. Effort is small and independent per site.

## Final Disposition

Replace with stronger mechanism
