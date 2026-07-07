# Projection Generation and Injection Plan

## Goal

Make the existing Project Context projection prompts actually feed the two consumers that currently need them:

- `LoopRelay.Plan.Cli` `AdversarialPlanReview`
- `LoopRelay.Cli` `DecisionSession`

The CLIs stay separate. Roadmap does not call Plan or Main, and Roadmap still stops at milestone specs. Projection generation becomes shared infrastructure because Plan and Main cannot reference Roadmap-local projection types.

## Current Gap

- `ProjectionForAdversarialPlanReview.prompt` exists, but `ReviewStep` calls `AdversarialPlanReview.Render(plan)` with no generated projection.
- `AdversarialPlanReview.prompt` contains a literal `north_star_projection` input body instead of a render parameter.
- `ProjectionForDecisionSession.prompt` exists, but `DecisionSession.BuildProposalPromptAsync(...)` injects only operational context plus `GenerateSystemPromptForFirstExecutionAgent` or `GenerateSystemPromptForNextExecutionAgent`.
- Roadmap owns reusable projection mechanics today (`ProjectContextLoader`, `ProjectionCache`, manifest/provenance/freshness/validator), but those are internal to `LoopRelay.Roadmap.Cli`.

## Design

Introduce shared projection infrastructure outside all three CLI executables, with no dependency on `LoopRelay.Roadmap.Cli`, `LoopRelay.Plan.Cli`, or `LoopRelay.Cli`.

Recommended shape:

- Add a shared project, for example `LoopRelay.Projections`.
- Reference shared dependencies only: `LoopRelay.Agents`, `LoopRelay.Core`, and the artifact/repository abstractions already used by the CLIs.
- Move or adapt Roadmap-local projection types into the shared project:
  - `ProjectContextLoader` / `ProjectContext`
  - projection definitions and catalog rendering
  - projection manifest persistence
  - provenance and freshness evaluation
  - projection validation
  - projection cache/generation service
- Keep Roadmap-specific runtime prompt contracts in Roadmap. The shared layer should accept registered projection definitions rather than hard-coding Roadmap workflow states.

Projection artifacts:

- `AdversarialPlanReview` projection: `.agents/projections/adversarial-plan-review.md`
- `DecisionSession` projection: `.agents/projections/decision-session.md`
- Shared manifest: `.agents/projections/manifest.json`

The manifest must merge entries by projection identity and preserve existing Roadmap projection entries.

## Prompt Changes

1. Update `AdversarialPlanReview.prompt`.
   - Replace the literal `north_star_projection` block with a render parameter.
   - Target generated signature: `AdversarialPlanReview.Render(projectContextProjection, planToReview)`.
   - Keep `planToReview` as the plan body.

2. Update `ProjectionForDecisionSession.prompt`.
   - Treat the intended consumer as `DecisionSession`, not only `GenerateSystemPromptForNextExecutionAgent`.
   - Make the downstream use instructions cover both first and next execution-system-prompt generation.

3. Update `GenerateSystemPromptForFirstExecutionAgent.prompt`.
   - Add an explicit projection input block, for example `<EXECUTION_STRATEGIC_MEMORY_PROJECTION>`.
   - Target generated signature: `GenerateSystemPromptForFirstExecutionAgent.Render(decisionSessionProjection)`.

4. Update `GenerateSystemPromptForNextExecutionAgent.prompt`.
   - Add the same projection input block before the handoff input.
   - Target generated signature: `GenerateSystemPromptForNextExecutionAgent.Render(decisionSessionProjection, handoff)`.

No consumer prompt should contain placeholder projection text after this change.

## Plan CLI Wiring

1. Register the shared projection service in `PlanCliComposition`.
2. Add a pipeline phase before `Adversarial Review`: `Generate Adversarial Review Projection`.
3. Generate or reuse the `AdversarialPlanReview` projection from `.agents/ctx/*.md`.
4. Write/update the projection artifact and manifest through the shared service.
5. Publish `.agents` after projection generation because this step writes durable artifacts.
6. Change `ReviewStep` to render `AdversarialPlanReview.Render(projection.Content, plan)`.
7. Keep the review Codex session read-only and one-turn; the host-side projection service owns artifact writes.

## Main CLI Wiring

1. Register the shared projection service in `LoopCliComposition`.
2. Inject it into `DecisionSession`.
3. Before opening or resuming a decision thread, evaluate freshness for the `DecisionSession` projection.
4. If the persisted decision thread was seeded from a stale or missing projection, clear resume state and open a fresh decision session.
5. When `seeded == false`, generate/reuse the fresh `DecisionSession` projection and include it with operational context in the first proposal turn.
6. When `seeded == true`, do not resend the projection; the warm Codex thread already carries it.
7. After a transfer, the new process starts with `seeded == false`, so the next proposal injects the projection again alongside the evolved operational context.

## Tests

Add or update tests for:

- Shared projection generation writes the expected artifact and manifest entry.
- Fresh projection reuse does not call Codex again.
- Project Context or projection prompt template drift marks the projection stale.
- Projection validation accepts the two new consumers and rejects missing required sections.
- `ReviewStep` sends `AdversarialPlanReview.Render(projection, plan)`.
- `PlanPipeline` generates and publishes the adversarial-review projection before review.
- `DecisionSession` fresh first proposal includes the decision projection.
- `DecisionSession` next proposal on a fresh process includes projection plus handoff.
- Warm `DecisionSession` proposals do not resend the projection.
- Resume with a stale/missing decision projection clears resume state and opens fresh.
- Transfer recycle re-injects the decision projection on the next fresh proposal.
- Architecture/layering tests prove Plan/Main do not reference Roadmap.

## Acceptance Criteria

- `AdversarialPlanReview` receives a generated Project Context projection, not literal placeholder text.
- `DecisionSession` receives a generated execution strategic memory projection on every fresh decision process.
- Projection artifacts are provenance-tracked and freshness-gated through `.agents/projections/manifest.json`.
- Existing Roadmap projection behavior still works or is migrated without changing Roadmap's stop-at-milestone-specs boundary.
- No automated `Roadmap -> Plan -> Main` chain is introduced.
