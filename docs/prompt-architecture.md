# Prompt Architecture

Every agent turn in the Plan Authoring → Execution → Decision orchestration loop (milestones m0–m10, `next` branch) is issued from a compile-time class generated from a `.prompt` template. This document is the authoritative catalog of those prompts, the generated signatures they expose, and the mechanisms that keep prompt text out of production code. It is the prompt-authority companion to the loop overview in `docs/architecture.md` (Orchestration Loop Architecture) and the evidence register in `docs/orchestration-loop-governance.md` (evidence package `LOOP-4`).

## Generated Prompt Authority

Prompts are the single source of agent instructions. The canonical prompt bodies live as `.prompt` templates under `src/CommandCenter.Core/Prompts/`; the Lib.Prompts source generator turns each template into a strongly-typed `static partial class` in the `CommandCenter.Core.Prompts` namespace at build time. Production code calls the generated class (`.Text` or `.Render(...)`) and never composes a prompt body by hand.

This is a structural invariant, not a convention:

- `CommandCenter.Core` is the base layer and the prompt authority. It takes no dependency on the `CommandCenter.Agents` runtime that sits above it (enforced by `ArchitectureLayeringTests`).
- The catalog is the *only* place a canonical prompt body appears in source. `ExecutionPromptBuilder` — the live execution path's prompt builder — previously hand-composed literals; that was deleted in favor of `ContinueExecution.Render(...)` / `StartExecution.Render(...)`, and the regression is locked out by `PromptAuthorityTests` (see *No-Literal-Prompt Enforcement*).
- Every rendered turn records a `PromptProvenance` (see *Prompt Provenance*) so a turn is auditable back to the exact template that produced it.

## The 11 Canonical Prompts

There are 11 canonical `.prompt` templates. A template with no `{placeholder}` emits a `const string Text` (plus a parameterless `Render()`); a template with placeholders emits `Render(string? …)` with one `string?` parameter per distinct placeholder, in first-appearance order. The "Generated signature" column below is derived from each template's placeholders and verified against the `.prompt` file; the "Session role" column is the `PromptSessionRole` recorded by the call site's provenance. Loop steps reference the lifecycle in `docs/architecture.md`.

| Prompt | Generated signature | Session role | Where used in the loop |
| --- | --- | --- | --- |
| `WritePlanForNewCodebase` | `.Text` | `Planning` | Write plan, `newCodebase == true` branch of `BeginWritePlanAsync` (held-open Operational planning session). |
| `WritePlanAgainstCodebase` | `.Text` | `Planning` | Write plan, `newCodebase == false` branch of `BeginWritePlanAsync` (held-open planning session). |
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
- The two `WritePlan*` and the three Transfer-phase context prompts (`ProduceOperationalDelta`, `UpdateOperationalContext`) are placeholder-free; their full instruction text is the artifact handed to the agent, and the artifact contents the agent reads (plan, operational context, delta) are supplied by the repository filesystem the turn runs against, not interpolated into the prompt.

## Source Generation

The generator is `Lib.Prompts.PromptSourceGenerator` (an `IIncrementalGenerator` at `dotnet-libraries/Lib.Prompts/src/Lib.Prompts/PromptSourceGenerator.cs`), wired into `CommandCenter.Core` as an analyzer-only reference (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`) with the package's `Lib.Prompts.props`/`Lib.Prompts.targets` supplying every `.prompt` as an `AdditionalFiles` input.

For each `*.prompt` additional file the generator:

- Parses `{name}` holes in a single pass (`{{` / `}}` escape to literal braces; a name must be a valid C# identifier; reused names map to one parameter). Malformed templates become **build errors** (`PROMPT001`–`PROMPT004`), so a broken placeholder fails the build rather than shipping.
- Emits a `public static partial class` named after the file, in the folder-derived namespace (`CommandCenter.Core.Prompts`).
- Always emits `public const string SourceHash` — the SHA-256 (lowercase hex) of the raw template text at build time — and `public const string Template` (the raw text).
- When the template has **no** parameters, emits `public const string Text` (the fully rendered text) and a parameterless `Render()` returning it.
- When the template **has** parameters, emits `Render(string? a, string? b, …)`. A null argument renders as the empty string; rendering is a single `string.Concat` of the literal and hole parts (or the lone hole, for a single-placeholder template). There is no runtime parse and no file I/O.

`SourceHash` is the build-time content pin: the orchestrator and `ExecutionPromptBuilder` copy it into each turn's `PromptProvenance`, so the exact template text behind any turn is recoverable.

## Prompt Provenance

`PromptProvenance` (`src/CommandCenter.Core/Prompts/PromptProvenance.cs`) is the per-turn record captured at every render site. It is a `sealed record` with seven fields:

1. `PromptName` — the canonical prompt's name, captured as `nameof(<Prompt>)`.
2. `PromptType` — the generated catalog type's full name (`typeof(<Prompt>).FullName`).
3. `SourceHash` — the generated `<Prompt>.SourceHash`, pinning the exact template text.
4. `SessionRole` — the `PromptSessionRole` the turn ran under.
5. `WorkflowPhase` — the phase within the role's lifecycle (e.g. operational `Start` vs `Continue`).
6. `InputArtifactIdentities` — repository-relative paths of the artifacts rendered into the prompt.
7. `OutputArtifactIdentities` — repository-relative paths of the artifacts the turn is directed to produce.

Artifacts are identified by repository-relative path — the only stable identity on `LoadedArtifact`/`Artifact` today (neither carries an id or content hash). Canonical paths are centralized in `OrchestrationArtifactPaths`.

`PromptSessionRole` is an enum with members `Planning`, `OperationalExecution`, `Decision`, `Transfer`, `ContextUpdate`. It **mirrors** `CommandCenter.Agents.Models.SessionRole` (same members, same order) but is **redeclared in Core** on purpose: Core is the base layer and prompt authority and must not reference the Agents runtime above it. A higher layer that already references both can map between the two enums; Core never does.

Provenance is recorded at the two render sites:

- `ExecutionPromptBuilder.Build` captures it for the operational `StartExecution` / `ContinueExecution` turns (`WorkflowPhase` = `Start` / `Continue`) and surfaces it on the additive nullable `ExecutionPromptManifest.Provenance` wire field.
- `RepositoryOrchestrator` captures it for every other loop turn (plan write/revise, milestone extraction, decision seed/propose, and the transfer-route delta/context-update turns).

## No-Literal-Prompt Enforcement

`PromptAuthorityTests` (defined in `tests/CommandCenter.Backend.Tests/ArchitectureLayeringTests.cs`) is the regression that protects prompt authority. Its single fact, `Production_source_does_not_duplicate_canonical_prompt_text`:

- Enumerates every `*.cs` under `src/` recursively.
- Excludes the catalog directory `src/CommandCenter.Core/Prompts` (which legitimately authors the text), any path under an `obj/` directory, and any generated `*.g.cs` file (the generator's output legitimately contains the text).
- Fails if any remaining production source file contains one of the `CanonicalPromptMarkers` — distinctive verbatim fragments of the canonical prompt bodies. A match means canonical prompt text was duplicated outside the generated catalog.

This guard is scoped to production `src/`; test-tree assertion literals are out of scope. It is the live enforcement of evidence package `LOOP-4` in `docs/orchestration-loop-governance.md`; the protective-mechanism entry is catalogued in `docs/architectural-mechanisms.md`.

## The `.prompt` Templates Are Authoritative

The `.prompt` files under `src/CommandCenter.Core/Prompts/` are the source of truth for agent instructions, and editing one changes its `SourceHash` and every turn's recorded provenance. They **must not be hand-edited as part of documentation work**. This document describes the catalog; it does not author it. Prompt-body changes are application changes that travel through the normal change and certification path, not through doc updates.

## See Also

- `docs/architecture.md` — Orchestration Loop Architecture (the loop lifecycle these prompts drive; the "Generated prompt authority" subsection).
- `docs/orchestration-loop-governance.md` — evidence package `LOOP-4` (generated prompt authority, provenance, and no-literal-prompt enforcement) and its guard map.
- `docs/architectural-mechanisms.md` — the protective-mechanism catalog home for the prompt-authority guard.
