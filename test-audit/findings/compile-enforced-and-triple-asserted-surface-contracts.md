# Surface contracts asserted redundantly: compile-enforced references and triple-asserted retirement

## Classification

- Category: Redundant verification, Impossible-state safeguard
- Severity: Low
- Confidence: Medium-High
- Evidence level: Strongly inferred
- Scope: `CliSurfaceDependencyTests` reference-purity case (tests/LoopRelay.Cli.Tests/Services/Cli/CliSurfaceDependencyTests.cs:12); retired-Plan/Roadmap assertions in `ConvergenceArchitectureVerifierTests`, `LoopRelayCompositionRootTests` (~L1894), and docs/orchestration-baseline.md
- Affected tests: 2–3 cases (cheap ones)
- Affected production paths: `ConvergenceArchitectureVerifier` (production reflection verifier)
- Primary cost: maintenance and duplicate authority (runtime negligible)
- Aggregate cost: negligible
- Wall-clock impact: none
- Invocation frequency: every run
- Recommended disposition: Narrow

## Summary

Two small redundancy clusters in the architecture-test surface. (1) `Parser_and_renderer_assembly_references_only_application_and_framework_contracts` asserts by reflection what the build already enforces: `LoopRelay.Cli.Surface.csproj` references only `LoopRelay.Application`, and an assembly cannot reference what its project does not — the failure the test guards is unreachable without a csproj edit, which the test would then simply mirror. (2) "Retired Plan/Roadmap compositions are absent/unreachable" is asserted by the production `ConvergenceArchitectureVerifier` (via its certification test), again inside `LoopRelayCompositionRootTests`, and again as prose in the baseline doc — three authorities for one invariant.

By contrast, the raw source-text scans (no `Console.*` in orchestration namespaces; no `SqliteCommand` in `UnifiedCliRunner`/`RepositoryObserver`) are **not** compiler-enforceable and are the real, load-bearing enforcement — explicitly not part of this finding.

## Evidence

- csproj graph: `LoopRelay.Cli.Surface → LoopRelay.Application` only (project references); the reflection test at `CliSurfaceDependencyTests.cs:12`.
- Triple assertion sites: `ConvergenceArchitectureVerifierTests.cs:14-16` (delegates to production verifier: Plan.Cli/Roadmap.Cli absent, retired-only-reachable = 0), `LoopRelayCompositionRootTests` retirement guards (~L1873/L1894/L2412), `docs/orchestration-baseline.md` L47-52.
- History: retirement landed at `1bd7797d` (deleted projects + slnx lines); the guards were added during convergence.

## Current Execution Path

Reflection over loaded assemblies (cheap) inside three different suites each run.

## Intended Purpose

(1) Keep the CLI surface dependency-pure. (2) Prevent retired Plan/Roadmap composition paths from regrowing during convergence.

## Necessity Analysis

(1) The reference-purity failure is unreachable at the assembly level given the project graph; the only reachable variant is someone editing the csproj — a change the reflection test cannot distinguish from an intended one. Assurance value ≈ 0; the test is a mirror, not a guard. (2) Retirement regression is reachable (code could be re-added), so *one* executable guard is justified; the production verifier is the strongest and already certification-owned. The composition-root duplicate adds only earlier localization during convergence work — modest, arguably expired now that retirement is months old; the doc line is descriptive, not enforcement.

## Redundancy Analysis

(1) Fully redundant with the build system. (2) Duplicate authority: production verifier = governing mechanism; composition-root assertion = partially redundant layering; doc = not a mechanism.

## Recommended Remediation

Owner: Cli.Tests / Certification.Tests. Remove the reference-purity reflection case (keep the parser/renderer behavioral cases in the same class). Keep the production verifier as the single retirement authority; retire the composition-root retirement guards or fold them into the verifier test if any assert something the verifier does not (verify by reading the ~3 assertions before deletion).

## Coverage-Preservation Plan

(1) Protection transfers to the compiler/project graph (stronger: fails at build). (2) Retirement remains guarded by the production verifier each run. Localization: a retirement regression now fails in one certification test instead of two places — acceptable. Accepted risk: none identified beyond that.

## Validation Plan

Delete-candidate dry run: temporarily re-introduce a fake retired-composition symbol locally; confirm the verifier test fails without the composition-root duplicate (throwaway perturbation, reverted). Build-graph guard needs no validation (compiler-owned).

## Risks and Tradeoffs

Minimal; main cost is convergence-era comfort. If active convergence work resumes on retired surfaces, the earlier-localization argument for the duplicate briefly returns.

## Final Disposition

Narrow
