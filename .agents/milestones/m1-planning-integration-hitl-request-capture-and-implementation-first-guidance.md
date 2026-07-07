# Milestone 1 - Planning Integration, HITL Request Capture, And Implementation-First Guidance

## Objective

inject centralized implementation-first guidance into planning, execution, decision, roadmap, and completion prompts while capturing explicit HITL non-implementation requests early.

## Work
- [ ] Extend `ImplementationFirstPromptPolicyComposer` with concise text for:
  - [ ] default implementation-first mode
  - [ ] HITL-request-enabled mode
  - [ ] HITL-requested documentation exception
  - [ ] prohibition against autonomous freeze/certification/governance/authority documentation milestones
  - [ ] warning against Architecture Tests, Golden Tests, and theory-protection artifacts unless backed by executable evaluation or explicitly requested
- [ ] Add `ExplicitHitlNonImplementationRequestCaptureService`.
  - [ ] Capture only explicit structured HITL requests, not inferred intent.
  - [ ] Generated plans or decisions may carry a stable section for HITL-requested non-implementation deliverables, but only when the source request is explicit.
  - [ ] Persist request entries with deliverable path or path pattern, source artifact path, source hash, HITL provenance kind, and rationale.
  - [ ] Attach request evidence to ledger entries when a changed candidate path matches an explicit HITL request entry.
  - [ ] Do not treat request evidence as a deletion/retention decision; it explains why a non-implementation file may be legitimate.
- [ ] Wire settings and request capture into:
  - [ ] `LoopCliComposition`
  - [ ] `PlanCliComposition`
  - [ ] `RoadmapCliComposition`
  - [ ] completion prompt runner composition where needed
- [ ] Prompt rendering changes:
  - [ ] `WritePlan.prompt`: include policy guidance and require plans to avoid autonomous non-implementation deliverables; if the setting is enabled and the HITL explicitly requested a non-implementation deliverable, list it in a structured HITL-request section.
  - [ ] `RevisePlan.prompt`: carry the same policy and preserve/update only explicitly grounded HITL-request entries.
  - [ ] `StartExecution.prompt` and `ContinueExecution.prompt`: include policy guidance before the execution command.
  - [ ] `GenerateSystemPromptForFirstExecutionAgent.prompt` and `GenerateSystemPromptForNextExecutionAgent.prompt`: instruct the decision agent not to prescribe autonomous non-implementation artifacts and to preserve structured HITL-request markers only when grounded in explicit HITL text.
  - [ ] roadmap planning prompts in `src/LoopRelay.Core/Prompts/Planning`: include policy guidance through `RoadmapPromptCatalog.RenderRuntime` or prompt context sections, not ad hoc duplicated text.
  - [ ] milestone generation should avoid documentation-centric milestones unless the setting is enabled and the HITL explicitly requested that deliverable.
  - [ ] completion prompts should receive review summaries when present.
- [ ] Prefer adding a `{promptPolicy}` parameter to generated prompt templates. If a prompt has many call sites, wrap policy appending in a small helper so policy text still comes from one composer.
- [ ] Add tests:
  - [ ] default settings inject implementation-first guidance into plan, execution, decision, and roadmap prompt paths
  - [ ] enabled setting still forbids non-implementation documentation without an explicit HITL request
  - [ ] prompt text preserves the HITL-requested documentation exception
  - [ ] plan prompt discourages documentation-centric milestones and unsupported Architecture/Golden Tests
  - [ ] explicit non-implementation deliverables are captured only from structured HITL-request markers
  - [ ] ledger entries can attach captured HITL request evidence
  - [ ] no prompt has a separately hard-coded copy of the policy body outside the composer

## Acceptance
- [ ] Relevant prompts receive implementation-first guidance from one composition point.
- [ ] Default planning and execution steer away from autonomous non-implementation files.
- [ ] Enabled mode allows non-implementation deliverables only when the HITL specifically requested them.
- [ ] HITL request evidence is captured when granted, not reconstructed only at completion.
