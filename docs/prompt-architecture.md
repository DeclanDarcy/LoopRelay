# Prompt Architecture

Every agent turn in the Plan Authoring → Execution → Decision orchestration loop (milestones m0–m10, `next` branch) is issued from a compile-time class generated from a `.prompt` template. This document is the authoritative catalog of those prompts, the generated signatures they expose, and the mechanisms that keep prompt text out of production code. It is the prompt-authority companion to the loop overview in `docs/architecture.md` (Orchestration Loop Architecture) and the evidence register in `docs/orchestration-loop-governance.md` (evidence package `LOOP-4`).

## Generated Prompt Authority

Prompts are the single source of agent instructions. The canonical prompt bodies live as `.prompt` templates under `src/LoopRelay.Core/Prompts/`; the Lib.Prompts source generator turns each template into a strongly-typed `static partial class` in the `LoopRelay.Core.Prompts` namespace at build time. Production code calls the generated class (`.Text` or `.Render(...)`) and never composes a prompt body by hand.

This is a structural invariant, not a convention:

- `LoopRelay.Core` is the base layer and the prompt authority. It takes no dependency on the `LoopRelay.Agents` runtime that sits above it (enforced by `ArchitectureLayeringTests`).
- The catalog is the *only* place a canonical prompt body appears in source. `ExecutionPromptBuilder` — the live execution path's prompt builder — previously hand-composed literals; that was deleted in favor of `ContinueExecution.Render(...)` / `StartExecution.Render(...)`, and the regression is locked out by `PromptAuthorityTests` (see *No-Literal-Prompt Enforcement*).
- Current CLI provenance is recorded at the artifact boundary rather than through a Core prompt-provenance DTO: roadmap projection/derived-artifact provenance, roadmap transition input snapshots and journals, and main CLI session telemetry carry the live audit trail.

## The 10 Canonical Prompts

There are 10 canonical `.prompt` templates in the backend-era loop catalog described here. A template with no `{placeholder}` emits a `const string Text` (plus a parameterless `Render()`); a template with placeholders emits `Render(string? …)` with one `string?` parameter per distinct placeholder, in first-appearance order. The "Generated signature" column below is derived from each template's placeholders and verified against the `.prompt` file. Loop steps reference the lifecycle in `docs/architecture.md`.

| Prompt | Generated signature | Runtime role | Where used in the loop |
| --- | --- | --- | --- |
| `WritePlan` | `.Text` | `Planning` | Write plan (`BeginWritePlanAsync`, held-open Operational planning session). |
| `RevisePlan` | `.Render(feedback)` | `Planning` | Revise plan (`BeginRevisePlanAsync`) — re-runs against the persisted `.agents/plan.md`. |
| `ExtractMilestones` | `.Text` | `OperationalExecution` | Execute plan — splits `.agents/plan.md` into `.agents/milestones/m*.md` (Operational one-shot). |
| `StartExecution` | `.Render(plan)` | `OperationalExecution` | Execute plan — first-milestone start, writes the live handoff (Operational one-shot). No handoff/decisions holes. |
| `ContinueExecution` | `.Render(plan, handoff, decisions)` | `OperationalExecution` | Continuation (`RunContinuationAsync`) — continues the current milestone and writes the next handoff (Operational one-shot). |
| `StartDecisionSession` | `.Render(operationalContext)` | `Decision` | Decision run — lazy one-time seed of the read-only Decision session from `operational_context.md` (off the primary stream). |
| `GetNextDecisions` | `.Render(handoff)` | `Decision` | Decision run (`RunDecisionAsync`) — proposes the next decisions from the current handoff, streamed to the decision stream. |
| `StartDecisionSessionFromTransfer` | `.Render(operationalContext)` | `Transfer` | Transfer route — seeds a fresh Decision session from the rewritten operational context after a recycle. |
| `ProduceOperationalDelta` | `.Text` | `Transfer` | Transfer route — warm Decision turn that writes `.agents/operational_delta.md` before the session is closed. |
| `UpdateOperationalContext` | `.Text` | `ContextUpdate` | Transfer route — Operational one-shot that rewrites `.agents/operational_context.md` from the delta. |

Notes:

- `StartDecisionSessionFromTransfer`'s placeholder is `{operationalContext}` in the template, so its generated parameter is `operationalContext` even though the transfer call site passes a local named `newContext`.
- `WritePlan` and the two Transfer-phase context prompts (`ProduceOperationalDelta`, `UpdateOperationalContext`) are placeholder-free; their full instruction text is the artifact handed to the agent, and the artifact contents the agent reads (plan, operational context, delta) are supplied by the repository filesystem the turn runs against, not interpolated into the prompt.

## Source Generation

The generator is `Lib.Prompts.PromptSourceGenerator` (an `IIncrementalGenerator` vendored at `src/LoopRelay.Prompts.Generator/PromptSourceGenerator.cs`), wired into `LoopRelay.Core` as an analyzer-only project reference (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`) with `LoopRelay.Core.csproj` supplying every `.prompt` as an `AdditionalFiles` input.

For each `*.prompt` additional file the generator:

- Parses `{name}` holes in a single pass (`{{` / `}}` escape to literal braces; a name must be a valid C# identifier; reused names map to one parameter). Malformed templates become **build errors** (`PROMPT001`–`PROMPT004`), so a broken placeholder fails the build rather than shipping.
- Emits a `public static partial class` named after the file, in the folder-derived namespace (`LoopRelay.Core.Prompts`).
- Always emits `public const string SourceHash` — the SHA-256 (lowercase hex) of the raw template text at build time — and `public const string Template` (the raw text).
- When the template has **no** parameters, emits `public const string Text` (the fully rendered text) and a parameterless `Render()` returning it.
- When the template **has** parameters, emits `Render(string? a, string? b, …)`. A null argument renders as the empty string; rendering is a single `string.Concat` of the literal and hole parts (or the lone hole, for a single-placeholder template). There is no runtime parse and no file I/O.

`SourceHash` is the build-time content pin. Current CLIs use generated prompt identities and hashes when writing derived artifact provenance, transition input evidence, and session telemetry; Core no longer exposes a separate prompt-provenance contract.

## Project Context Projections

Projection prompts under `src/LoopRelay.Core/Prompts/Projections/` receive one rendered `{projectContext}` input: the full canonical `.agents/ctx` source set in fixed order, including `09-eval-details.md`. The projection layer records the rendered Project Context hash and all canonical source paths in projection manifests. Runtime prompts receive projection content plus runtime evidence; they do not receive raw Project Context source files or file-boundary markers.

## Current CLI Provenance

The retired backend loop used a Core `PromptProvenance` record to describe every rendered turn. The current CLI-shaped solution does not ship that record. Auditability is carried by the runtime surfaces that still have production owners:

- Roadmap projection and derived-artifact provenance for generated roadmap artifacts.
- Roadmap transition input snapshots and journals for state-machine transitions.
- Main CLI session telemetry for loop/session execution events.

The generated prompt classes remain the prompt authority. Their `SourceHash` values are evidence inputs for those CLI-owned records, not a standalone Core contract.

## No-Literal-Prompt Enforcement

`PromptAuthorityTests` (defined in `tests/LoopRelay.Backend.Tests/ArchitectureLayeringTests.cs`) is the regression that protects prompt authority. Its single fact, `Production_source_does_not_duplicate_canonical_prompt_text`:

- Enumerates every `*.cs` under `src/` recursively.
- Excludes the catalog directory `src/LoopRelay.Core/Prompts` (which legitimately authors the text), any path under an `obj/` directory, and any generated `*.g.cs` file (the generator's output legitimately contains the text).
- Fails if any remaining production source file contains one of the `CanonicalPromptMarkers` — distinctive verbatim fragments of the canonical prompt bodies. A match means canonical prompt text was duplicated outside the generated catalog.

This guard is scoped to production `src/`; test-tree assertion literals are out of scope. It is the live enforcement of evidence package `LOOP-4` in `docs/orchestration-loop-governance.md`; the protective-mechanism entry is catalogued in `docs/architectural-mechanisms.md`.

## The `.prompt` Templates Are Authoritative

The `.prompt` files under `src/LoopRelay.Core/Prompts/` are the source of truth for agent instructions, and editing one changes its `SourceHash` and every turn's recorded provenance. They **must not be hand-edited as part of documentation work**. This document describes the catalog; it does not author it. Prompt-body changes are application changes that travel through the normal change and certification path, not through doc updates.

## See Also

- `docs/architecture.md` — Orchestration Loop Architecture (the loop lifecycle these prompts drive; the "Generated prompt authority" subsection).
- `docs/orchestration-loop-governance.md` — evidence package `LOOP-4` (generated prompt authority, provenance, and no-literal-prompt enforcement) and its guard map.
- `docs/architectural-mechanisms.md` — the protective-mechanism catalog home for the prompt-authority guard.
