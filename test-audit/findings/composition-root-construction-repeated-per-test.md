# Full composition-root construction repeated for every test in the class

## Classification

- Category: Repeated setup
- Severity: Medium (bounded by missing measurement)
- Confidence: Medium
- Evidence level: Strongly inferred (repetition observed; per-call cost not measured)
- Scope: `LoopRelayCompositionRoot.CreateForTests` call sites in `LoopRelayCompositionRootTests` (30+ sites across 44 cases); similar per-test composition in `UnifiedCliRunnerTests` and `OperationalRuntimeCompositionTests`
- Affected tests: ~90 cases in Cli.Tests
- Affected production paths: `LoopRelayCompositionRoot.CreateCore` — full DI object graph, workflow-catalog validation, SHA-256 runtime/prompt hashing, capability serialization, `CliSettingsLoader.Load()` via `RequireBrain`
- Primary cost: aggregate runtime (CPU + settings-file read per call)
- Aggregate cost: Not measured; per-call cost × ~90 calls (see gap)
- Wall-clock impact: inside the suite's serial critical-path class
- Invocation frequency: once per test case
- Recommended disposition: Measure first

## Summary

Each composition-root/runner test rebuilds the entire production object graph: dozens of stores and runtimes, full workflow-catalog validation, SHA-256 hashes over runtime/prompt assets, capability serialization, and a settings-file load. The inputs to catalog validation and hashing are identical for every test in a run (same assemblies, same prompt assets), so this is immutable work repeated ~90 times. Its per-call cost has not been measured, so its share of the 5–55 s test durations is unknown — it must be measured before deciding whether fixture-level sharing is worth the isolation tradeoff.

## Evidence

`CreateForTests` → `CreateCore` (`LoopRelayCompositionRoot.cs:212-221`, `:403-500+`): catalog validation, SHA-256 computation (`:433-438`), `RequireBrain` → `CliSettingsLoader.Load()` (`:219`, `:388`, `:391-401`). 30+ call sites in the composition-root class alone. Stores construct lazily (no DB I/O at construction — verified by the lifecycle trace), so this cost is distinct from the database findings.

## Current Execution Path

Test constructor/body → `CreateForTests(tempWorkspace, fakes…)` → full graph construction + validation + hashing + settings read → test drives transitions → dispose.

## Intended Purpose

Each test intends a fresh, fully wired system so cross-test state cannot leak through the graph — a legitimate isolation default given the class mutates workspace state heavily.

## Necessity Analysis

Graph *construction* per test is cheap isolation insurance; the *validation and hashing* of process-constant inputs is immutable work with no per-test information. If measurement shows the immutable share is material (seconds across the class), it can be computed once per process and injected; if it shows milliseconds, no change is justified — hence Measure first, not a remediation claim.

## Redundancy Analysis

Catalog validation also runs in every other test that composes the root (runner/composition tests), and the catalog has its own dedicated validator tests (`WorkflowDefinitionValidatorTests`, 24 attrs) — layered but repeated against identical inputs.

## Recommended Remediation

First measure: wrap one composition-root test run with a stopwatch split at `CreateForTests` return (temporary local instrumentation, or ETW/dotnet-trace sampling on a filtered run) to get per-call cost and its share of test duration. If material: cache the validated-catalog + hash results in a process-level lazy keyed by asset fingerprint (owner: production `CreateForTests` seam or a test-only composition helper), with invalidation = process lifetime (inputs are immutable per run).

## Coverage-Preservation Plan

Catalog-validation failures remain covered by the dedicated validator tests plus the first composition per process. If caching lands, one composition-root test should still compose from scratch to keep the end-to-end validation path exercised every run.

## Validation Plan

Measured per-call cost before; after any caching: identical test outcomes, reduced class chain length, and a deliberate catalog-corruption perturbation still failing the scratch-composition test.

## Risks and Tradeoffs

Caching validated results weakens per-test independence marginally (shared immutable artifact); mis-scoped caching of anything workspace-dependent would be a real hazard — the cacheable set must be strictly process-constant (assemblies, embedded prompts), never workspace state.

## Final Disposition

Measure first
