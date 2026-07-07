# Milestone 1 - Planning Integration, HITL Request Capture, And Implementation-First Guidance

## Objective

inject centralized implementation-first guidance into planning, execution, decision, roadmap, and completion prompts while capturing explicit HITL non-implementation requests early.

## Work
- [x] Extend `ImplementationFirstPromptPolicyComposer` with concise text for:
  - [x] default implementation-first mode
  - [x] HITL-request-enabled mode
  - [x] HITL-requested documentation exception
  - [x] prohibition against autonomous freeze/certification/governance/authority documentation milestones
  - [x] warning against Architecture Tests, Golden Tests, and theory-protection artifacts unless backed by executable evaluation or explicitly requested
- [x] Add `ExplicitHitlNonImplementationRequestCaptureService`.
  - [x] Capture only explicit structured HITL requests, not inferred intent.
  - [x] Generated plans or decisions may carry a stable section for HITL-requested non-implementation deliverables, but only when the source request is explicit.
  - [x] Persist request entries with deliverable path or path pattern, source artifact path, source hash, HITL provenance kind, and rationale.
  - [x] Attach request evidence to ledger entries when a changed candidate path matches an explicit HITL request entry.
  - [x] Do not treat request evidence as a deletion/retention decision; it explains why a non-implementation file may be legitimate.
- [ ] Wire settings and request capture into:
  - [ ] `LoopCliComposition`
  - [ ] `PlanCliComposition`
  - [ ] `RoadmapCliComposition`
  - [ ] completion prompt runner composition where needed
- [ ] Prompt rendering changes:
  - [ ] `WritePlan.prompt`: include policy guidance and require plans to avoid autonomous non-implementation deliverables; if the setting is enabled and the HITL explicitly requested a non-implementation deliverable, list it in a structured HITL-request section.
  - [ ] `RevisePlan.prompt`: carry the same policy and preserve/update only explicitly grounded HITL-request entries.
  - [x] `StartExecution.prompt` and `ContinueExecution.prompt`: include policy guidance before the execution command.
  - [x] `GenerateSystemPromptForFirstExecutionAgent.prompt` and `GenerateSystemPromptForNextExecutionAgent.prompt`: instruct the decision agent not to prescribe autonomous non-implementation artifacts and to preserve structured HITL-request markers only when grounded in explicit HITL text.
  - [x] roadmap planning prompts in `src/LoopRelay.Core/Prompts/Planning`: include policy guidance through `RoadmapPromptCatalog.RenderRuntime` or prompt context sections, not ad hoc duplicated text.
  - [x] milestone generation should avoid documentation-centric milestones unless the setting is enabled and the HITL explicitly requested that deliverable.
  - [ ] completion prompts should receive review summaries when present.
- [x] Prefer adding a `{promptPolicy}` parameter to generated prompt templates. If a prompt has many call sites, wrap policy appending in a small helper so policy text still comes from one composer.
- [ ] Add tests:
  - [x] default settings inject implementation-first guidance into plan, execution, decision, and roadmap prompt paths
  - [x] enabled setting still forbids non-implementation documentation without an explicit HITL request
  - [x] prompt text preserves the HITL-requested documentation exception
  - [x] plan prompt discourages documentation-centric milestones and unsupported Architecture/Golden Tests
  - [x] explicit non-implementation deliverables are captured only from structured HITL-request markers
  - [x] ledger entries can attach captured HITL request evidence
  - [ ] no prompt has a separately hard-coded copy of the policy body outside the composer

## Detail Notes

Implement the current plan's stricter prompt-time rule unless `.agents/plan.md` is revised first: non-implementation file generation requires both `artifactPolicy.allowHitlRequestedNonImplementationFiles = true` and explicit captured HITL request evidence. Enabled mode never authorizes autonomous documentation or theory-protection artifacts.

Prompt policy should come only from `ImplementationFirstPromptPolicyComposer`. Prompt templates may accept a `{promptPolicy}` parameter, or a small helper may append composed text, but the policy body itself must not be copied into multiple templates.

Prompt targets include:

- `WritePlan.prompt`
- `RevisePlan.prompt`
- `StartExecution.prompt`
- `ContinueExecution.prompt`
- `GenerateSystemPromptForFirstExecutionAgent.prompt`
- `GenerateSystemPromptForNextExecutionAgent.prompt`
- decision prompts such as `GetNextDecisions.prompt`, `StartDecisionSession.prompt`, and `StartDecisionSessionFromTransfer.prompt` when they can prescribe deliverables
- roadmap planning prompts under `src/LoopRelay.Core/Prompts/Planning` through `RoadmapPromptCatalog.RenderRuntime` or an equivalent centralized context section
- completion evaluation context when review summaries exist

The policy text must discourage freeze, certification, governance, authority documentation milestones, unsupported Architecture Tests, unsupported Golden Tests, and other artifacts whose purpose is protecting theory rather than validating executable behavior.

`ExplicitHitlNonImplementationRequestCaptureService` should capture only explicit HITL request evidence. Do not infer request evidence from plan prose, agent-authored decisions, file names, or the mere existence of a documentation deliverable.

Generated plan and decision artifacts may carry a stable structured section such as:

```markdown
## HITL-Requested Non-Implementation Deliverables

| Path Or Pattern | Source | Source Hash | Rationale |
| --- | --- | --- | --- |
```

The exact section name can differ if the implementation already has a better convention, but it must be stable and parseable. Captured request entries need:

- deliverable path or path pattern
- source artifact path
- source content hash
- HITL provenance kind
- rationale or short excerpt
- first captured timestamp using the repository's existing timestamp convention, or UTC ISO-8601 if no convention exists

Request evidence explains legitimacy. It is not a keep/delete decision and must not bypass completion review.

## Acceptance
- [ ] Relevant prompts receive implementation-first guidance from one composition point.
- [ ] Default planning and execution steer away from autonomous non-implementation files.
- [ ] Enabled mode allows non-implementation deliverables only when the HITL specifically requested them.
- [ ] HITL request evidence is captured when granted, not reconstructed only at completion.
