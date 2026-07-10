# Execution Model and Effort Implementation Plan

**Status:** Implemented
**Plan date:** 2026-07-10  
**Repository revision verified:** `777d30cb07bbd65c968e13a0a0b4ac488bdee765`  
**Primary evidence:** `execution-model-and-effort-audit.md`, verified against the current source tree

## Implementation Outcome

Implemented on 2026-07-10 against the active unified CLI path.

- `config/settings.default.json` is the default Brain authority (`gpt-5.6-sol`, `xhigh`); settings loading validates the closed model and effort vocabularies and supplies one immutable `BrainConfiguration` per unified composition.
- `AgentSessionSpec` requires explicit typed model, effort, and configuration authority. Generic startup options cannot supply `model`, `effort`, or `model_reasoning_effort`.
- Codex one-shot and persistent launches project the canonical values. The installed Codex 0.142.5 schema was verified to accept `model` on `thread/start`, `thread/resume`, and `turn/start`, and `effort` on `turn/start`.
- First and Next decision proposals are followed immediately by a recommendation turn in the same Brain-configured session. Strict parsing accepts exactly `Model` and `Effort` and persists `.agents/decisions/decisions.recommendation.json` with the exact prompt hash.
- `DecisionSet` validation and repository observation require a valid matching live pair. Active implementation opens only from a `ValidatedExecutionRecommendation`; the held-open session remains unchanged through handoff.
- Restart-safe execution attempts, recommendation lifecycle infrastructure, transaction redesign, settings relocation, and legacy executor removal remain deferred as planned.

## Executive Summary

LoopRelay currently has no repository-owned model selection. Production launches omit model, leaving Codex configuration to choose it externally. Effort is repository-owned but distributed among factories and launch sites. The active implementation session uses `medium`; most non-execution agents use the literal `xhigh`.

This plan introduces two explicit and non-overlapping configuration authorities:

| Domain | Consumers | Exclusive authority |
|---|---|---|
| Brain configuration | Every non-execution agent | `BrainModel` and `BrainEffort`, loaded once per composition |
| Execution configuration | Implementation work and its same-session handoff | A validated execution recommendation containing `Model` and `Effort`, correlated to the generated execution prompt |

The initial Brain configuration is:

- `BrainModel`: `gpt-5.6-sol`
- `BrainEffort`: `xhigh`

After either the First or Next execution-system-prompt turn succeeds, the same Brain-configured decision session generates a strict structured recommendation. LoopRelay validates it, persists it beside the generated execution prompt, and verifies their correlation before launch. The active implementation session receives model and effort only from that recommendation. The existing held-open implementation session continues to generate the handoff with the same configuration.

The plan deliberately does not redesign the execution lifecycle. Restart-safe execution attempts, thread-identity persistence, lifecycle state machines, transaction redesign, new workflow product types, legacy executor removal, and settings-subsystem relocation are deferred unless implementation proves that one of them is a direct blocker.

No Brain fallback is permitted for execution. Missing, malformed, unsupported, unpersisted, or prompt-mismatched execution configuration blocks launch.

## Scope Decision

### Required for this feature

1. Add `BrainModel` and `BrainEffort` to application settings and load them once per composition.
2. Make every active non-execution launch use the loaded Brain configuration.
3. Add explicit model and effort to the transport-neutral session contract.
4. Require active Codex transports to faithfully project the session contract.
5. Generate and strictly validate `Model` and `Effort` immediately after each First or Next execution prompt.
6. Persist the recommendation with a hash of the exact execution prompt.
7. Require the active implementation launch to load the matching recommendation and use it exclusively.
8. Preserve the same session configuration for the existing implementation-to-handoff continuation.
9. Add focused tests for authority, parsing, correlation, propagation, and fail-closed behavior.

### Opportunistic improvements, deferred

The following work may be valuable, but it is not on the critical path for execution recommendations:

- a restart-safe execution-attempt state machine;
- execution lifecycle and thread-identity persistence;
- retry, cancellation, and uncertain-turn recovery redesign;
- a new SQLite execution-decision aggregate or transaction model;
- recommendation identities, lifecycle states, retirement, and active-recommendation queries;
- a new recommendation workflow product type;
- removal of `ExecutionStep`, `LoopRunner`, or `RoadmapExecutionBridge`;
- relocation of general settings ownership out of `LoopRelay.Permissions`;
- a general transport-capability framework or parity work beyond the active Codex launch paths;
- broad telemetry or canonical-evidence redesign.

These items should be recorded as follow-up work. A deferred item may enter the implementation only if a concrete repository constraint demonstrates that the minimum design cannot satisfy an acceptance criterion. That decision must be documented with the blocking evidence and the smallest proposed addition.

## Goals

1. Establish exactly two model-and-effort authority domains: Brain and Execution.
2. Make `BrainModel` and `BrainEffort` the exclusive source for every active non-execution agent.
3. Generate a structured execution recommendation immediately after every successful First or Next execution-system-prompt generation.
4. Restrict recommendations to:

   | Property | Allowed values |
   |---|---|
   | `Model` | `gpt-5.5`, `gpt-5.6-luna`, `gpt-5.6-terra`, `gpt-5.6-sol` |
   | `Effort` | `low`, `medium`, `high`, `xhigh` |

5. Correlate the recommendation with the exact execution prompt it configures.
6. Make a validated, persisted, prompt-matching recommendation the exclusive model-and-effort source for the active implementation launch.
7. Make model and effort explicit typed session inputs and project them faithfully through active transports.
8. Preserve transport neutrality above the Codex adapter layer.
9. Fail closed without substituting Brain settings, Codex defaults, or historical hardcoded effort.

## Non-Goals

- Defining the policy used to choose among allowed execution models and efforts beyond the structured prompt contract.
- Redesigning execution restart, resume, retry, cancellation, or recovery.
- Making the current implementation-to-handoff flow restart-safe.
- Introducing execution-attempt persistence or an execution lifecycle state machine.
- Replacing existing decision persistence with a new transactional store.
- Introducing a new workflow product unless the existing `DecisionSet` path is proven insufficient.
- Removing unused or retired execution implementations as part of the feature.
- Moving application settings between assemblies or subsystems.
- Redesigning sandbox, network, approval, permission, planning, roadmap, completion, or artifact-mutation policy.
- Using `SessionRole` as the configuration authority classifier.
- Coupling domain contracts to Codex CLI option names or app-server message shapes.
- Falling back to Brain configuration, external Codex defaults, or a fixed effort for execution.

## Current Architecture Summary

### Decision flow

`DecisionSession` selects the First or Next prompt template, runs the decision-agent turn, and persists the free-form output as numbered decision history and live `.agents/decisions/decisions.md`. Both variants converge on the same successful `AgentTurnResult`, giving the feature one common insertion point after prompt generation and before persistence.

There is currently no structured recommendation or prompt-to-configuration correlation.

### Execution flow

The active `ExecuteImplementationSlice` path reads the current decisions, builds `ContinueExecution`, opens a persistent danger-full-access session with effort `medium` and no explicit model, and runs the implementation turn. `GenerateHandoff` later uses that same in-memory session before closing it.

This existing same-process continuation already guarantees that work and handoff share one session specification. Restart between those transitions is not currently supported and is outside this feature.

### Non-execution launches

Non-execution session construction is distributed across unified CLI, Plan CLI, Roadmap CLI, completion, review, projection, evaluation, and scoped artifact-operation code. These launches normally select `xhigh` independently and omit model.

### Transport behavior

`AgentSessionSpec` has no explicit model, requires an `EffortProfile`, permits a free-string effort identifier, and exposes untyped startup options. One-shot Codex launches can emit a raw model startup option. Persistent app-server launches currently propagate effort but omit model.

The session contract must become authoritative; transport implementations must faithfully project its canonical model and effort.

## Target Architecture

| Launch purpose | Authority | Model and effort source |
|---|---|---|
| Decision and recommendation generation | Brain | Loaded Brain configuration |
| Plan, roadmap, projection, evaluation, completion, and review | Brain | Loaded Brain configuration |
| Scoped artifact operations | Brain | Loaded Brain configuration |
| Active implementation work | Execution | Validated persisted recommendation matching the current execution prompt |
| Handoff after implementation | Execution | The already-open implementation session and its unchanged session configuration |

The architecture has three small canonical inputs:

1. `BrainConfiguration`, containing `Model` and `Effort`.
2. `ExecutionRecommendation`, containing the agent-produced `Model` and `Effort`.
3. `AgentSessionSpec`, containing explicit canonical model, effort, and authority domain for transport projection.

The persisted recommendation adds only the correlation data needed at the later launch boundary: a cryptographic hash of the exact generated execution prompt. It does not require a recommendation identity, aggregate lifecycle, execution attempt, or retirement state.

## Authority Model

### Brain authority

Brain configuration is the only authority for non-execution launches.

The default values appear once in `config/settings.default.json`. The selected settings document is validated during composition, and one immutable `BrainConfiguration` instance is supplied to every component that constructs non-execution sessions.

Production factories do not define fallback copies. Test compositions provide Brain configuration explicitly or through a shared fixture.

Sandbox, approval, network, working directory, session lifetime, and permission profile remain selected by workflow purpose. Brain configuration centralizes only model and effort.

### Execution authority

The active implementation launch is legal only when it receives a validated recommendation whose stored prompt hash matches the exact execution prompt being loaded.

Execution construction must not:

- read Brain configuration;
- accept caller-selected model or effort alongside a recommendation;
- consult Codex defaults;
- derive configuration from `SessionRole`;
- parse configuration out of `decisions.md`;
- substitute the current `medium` value when recommendation loading fails.

The execution-session factory accepts a validated prompt-and-recommendation pair and exposes no separate model or effort parameters.

### Enforcement boundary

Authority is enforced by construction and checked immediately before transport launch:

- Brain session construction accepts `BrainConfiguration`.
- Execution session construction accepts a validated `ExecutionRecommendation` bound to the current prompt.
- `AgentSessionSpec` contains explicit model, effort, and authority domain.
- Generic startup options cannot override model or effort.
- A transport never selects, defaults, or reinterprets either value.

`SessionRole` may continue to describe behavioral purpose, but it does not select configuration authority.

## Brain Configuration Architecture

### Settings

Add the following required fields to the existing application settings shape:

- `BrainModel`
- `BrainEffort`

The default settings file supplies `gpt-5.6-sol` and `xhigh`. The existing settings-loading path may be extended in place for this feature. Moving settings ownership to a neutral subsystem is a separate cleanup.

Missing, blank, or unsupported values fail startup with a clear configuration error. Production source contains no fallback literals.

### Composition

`UnifiedCliComposition.CreateProduction` loads and validates Brain configuration once. The resulting immutable value is injected into shared non-execution session-spec construction and any direct launch components.

Separate CLI compositions that remain supported follow the same rule: load once at their composition boundary and pass the value inward.

### Consumer migration

Migrate the active and supported non-execution launch families identified by the audit:

| Launch family | Current location |
|---|---|
| Unified plan authoring and review | `LoopRelay.Cli/Services/Agents/AgentSpecs.cs` and `UnifiedCliComposition` |
| First and Next decision generation | `DecisionSession` |
| Recommendation generation | New post-proposal turn in `DecisionSession` |
| Decision projections | `ProjectionPromptRunner` |
| Traditional, evaluation, and milestone generation | `UnifiedCliComposition.ExecuteOneShotAgentPromptAsync` |
| Plan and decision-transfer scoped operations | `UnifiedCliComposition` and `DecisionSession.RunArtifactOperationAsync` |
| Plan CLI non-execution agents | `LoopRelay.Plan.Cli` |
| Roadmap planning and projection agents | `LoopRelay.Roadmap.Cli` |
| Completion and synthesis agents | `AgentCompletionPromptRunner` |
| Semantic and non-implementation review agents | `AgentNonImplementationReviewRunner` |

The implementation inventory should distinguish supported non-execution launches from compiled but unused execution paths. It does not need to delete those paths.

## Execution Recommendation Design

### Generation sequence

Both First and Next variants use the same sequence:

1. The Brain-configured decision session generates the execution system prompt.
2. LoopRelay requires a completed, non-empty proposal result.
3. The same decision session immediately runs the structured recommendation prompt against that generated output.
4. LoopRelay parses and validates the recommendation.
5. LoopRelay computes the prompt hash and persists the prompt and matching recommendation artifacts.
6. Only a complete, validated, hash-matching pair can satisfy execution launch validation.

No routing, transfer, or execution launch intervenes between prompt generation and recommendation generation. The recommendation turn is non-execution work and therefore uses Brain configuration.

### Agent-facing structured output

The recommendation output contains exactly two properties:

| Property | Requirements |
|---|---|
| `Model` | Required string; exact case-sensitive member of the four-model allowlist |
| `Effort` | Required string; exact case-sensitive member of `low`, `medium`, `high`, `xhigh` |

The parser rejects:

- Markdown fences or surrounding commentary;
- missing, duplicate, null, or non-string properties;
- unknown properties;
- multiple objects or trailing content;
- unsupported casing or values.

The prompt renderer, parser, and validator use one canonical allowed-value definition. Native constrained-output support is optional; repository validation remains authoritative.

### Minimal persisted representation

Persist one small recommendation sidecar associated with the live decision artifact. Its required data is:

| Field | Purpose |
|---|---|
| `Model` | Validated execution model |
| `Effort` | Validated execution effort |
| `PromptHash` | SHA-256 hash of the exact execution system prompt |

The live prompt remains `.agents/decisions/decisions.md`. The recommendation is stored through `LoopArtifacts` at a deterministic adjacent path. If numbered decision history must remain self-describing, write the same sidecar beside the numbered prompt history entry; history storage is evidence, not the launch authority.

This sidecar is deliberately not an aggregate root. It has no lifecycle state, attempt correlation, active query, retirement operation, or independent identity. A later First or Next decision replaces the live prompt and live sidecar as a pair under the existing decision semantics.

Persist the validated sidecar and prompt before reporting the decision transition successful. If either write fails, the transition fails. At execution time, hash validation prevents a partially updated or stale pair from launching. A new database transaction model is unnecessary for this invariant.

### Existing workflow representation

Use the existing `DecisionSet` workflow product first.

The implementation transition already resolves the current decision artifact. Extend its readiness or launch validation to require the adjacent recommendation sidecar and a matching prompt hash. The `DecisionSet` remains the workflow representation of a launchable decision; recommendation completeness becomes part of its validation rather than a new product.

Introduce a distinct recommendation product only if implementation demonstrates that the existing product-validation boundary cannot:

1. locate the sidecar deterministically;
2. reject missing or invalid recommendation data; or
3. carry the validated pair to the execution launch without reintroducing an authority bypass.

The burden is to document that concrete limitation before expanding the workflow model.

### Consumption

Immediately before opening the active implementation session:

1. Read the exact decision prompt that will be rendered into `ContinueExecution`.
2. Read the adjacent recommendation sidecar.
3. Parse and validate model and effort using the canonical contract.
4. Recompute the prompt hash and require an exact match.
5. Build the execution session specification exclusively from that validated pair.

Missing, malformed, unsupported, or mismatched data blocks execution. Raw decision text, Brain settings, external Codex configuration, and the old `medium` literal are never fallback sources.

The existing held-open session then performs implementation work and handoff. Because the session specification is immutable, no second recommendation lookup or configuration choice occurs at handoff.

## Session and Transport Contract

### Canonical session contract

`AgentSessionSpec` owns explicit transport-neutral model and effort values. Effort uses a closed typed vocabulary that includes `xhigh`; arbitrary identifiers are not accepted as canonical effort.

The contract also identifies whether configuration came from the Brain or Execution authority. This is used for validation and diagnostics, not for selecting values.

Raw startup options cannot contain reserved model or effort keys. This prevents a second configuration channel from overriding the canonical fields.

### Transport projection

The session contract owns model and effort. Transport implementations must faithfully project the canonical session contract.

For the active Codex paths, this means:

- one-shot launches transmit the explicit model and effort;
- persistent app-server sessions transmit the explicit model and effort at the supported process, thread, or turn boundary;
- the same persistent session cannot silently change either value between implementation and handoff;
- adapters fail clearly if the installed Codex boundary cannot express the requested value;
- adapters do not select defaults.

Codex CLI quoting, app-server fields, and version-specific capability details remain adapter concerns. The implementation should verify the narrow active paths it needs, not introduce a general transport capability architecture.

## Validation and Failure Behavior

| Failure | Required behavior |
|---|---|
| Brain setting missing, blank, or unsupported | Fail composition startup |
| Recommendation output malformed | Fail the decision transition; do not persist a launchable pair |
| Recommendation model or effort unsupported | Same as malformed output |
| Prompt persistence fails | Fail the decision transition |
| Recommendation persistence fails | Fail the decision transition |
| Recommendation sidecar missing at launch | Block execution |
| Prompt hash mismatch | Block execution as stale or partial state |
| Generic startup option tries to set model or effort | Reject the session specification |
| Active transport cannot express model or effort | Fail prerequisite or launch validation |
| Runtime rejects model or effort | Report launch failure; do not substitute another value |
| Handoff is requested without the held implementation session | Preserve current failure behavior; restart recovery is deferred |

Bounded raw output may be retained for diagnosing recommendation parse failures, but it never becomes configuration authority.

## Migration Strategy

Migration proceeds from the smallest shared contracts to active consumers:

1. Confirm the supported launch inventory and the First/Next common seam.
2. Add typed model, effort, and authority to the session contract.
3. Add Brain settings and load them once per composition.
4. Make the active Codex transports faithfully project the canonical session contract.
5. Migrate supported non-execution consumers to Brain configuration.
6. Add recommendation generation, strict validation, and the minimal sidecar.
7. Gate the active implementation launch on the validated prompt/recommendation pair.
8. Remove only the hardcoded and raw-override paths superseded by this feature.

No milestone depends on removing unused execution implementations, redesigning persistence, or making execution restart-safe.

If shared contract changes touch compiled legacy paths, make only the mechanical adaptation needed to compile and test them. Do not expand the feature into lifecycle migration or path removal; record that as follow-up cleanup.

## Dependency Analysis

| Dependency | Required before |
|---|---|
| Supported launch inventory | Consumer migration and regression checks |
| Typed session model and effort | Transport projection and all launches |
| Brain settings loading | Decision/recommendation and non-execution migration |
| Active transport projection | Production use of explicit Brain or Execution model |
| Recommendation parser and validator | Sidecar persistence and execution gating |
| Sidecar persistence and prompt correlation | Active implementation migration |

Critical path:

1. inventory;
2. session and Brain contracts;
3. active transport projection;
4. Brain consumer migration;
5. recommendation generation and persistence;
6. execution launch integration;
7. focused cleanup and verification.

## Milestone Plan

Effort estimates are focused engineering days for one implementation agent and include milestone-level tests. They are planning ranges, not delivery commitments.

### Milestone 0 — Confirm the narrow integration surface

**Effort:** 1–2 days

- Confirm every supported production session construction and classify it as Brain or Execution.
- Pin the common post-proposal seam used by First and Next decision generation.
- Confirm the active implementation-to-handoff session ownership.
- Identify compiled legacy paths without treating their removal as a dependency.

**Exit criteria:**

- The supported launch inventory is explicit.
- The insertion and launch boundaries are covered by characterization tests.
- No unrelated lifecycle work is required to begin implementation.

### Milestone 1 — Establish canonical session and Brain configuration

**Effort:** 2–3 days

- Add explicit typed model, effort, and authority fields to `AgentSessionSpec`.
- Add canonical `xhigh` effort support and close the arbitrary effort escape used by supported paths.
- Reserve model and effort against generic startup-option overrides.
- Add `BrainModel` and `BrainEffort` to settings with the required defaults.
- Load and validate one immutable Brain configuration per composition.

**Exit criteria:**

- Supported session specifications cannot omit model or effort.
- Default Brain literals occur only in `config/settings.default.json`.
- Missing or invalid Brain settings fail startup.
- No settings-subsystem relocation was introduced.

### Milestone 2 — Project the canonical session contract through active transports

**Effort:** 2–4 days

- Update one-shot and persistent Codex adapters to transmit explicit session model and effort.
- Verify the installed app-server boundary used by the active persistent sessions.
- Centralize Codex effort serialization.
- Reject reserved startup-option conflicts.
- Fail clearly when the transport cannot express a selected value.

**Exit criteria:**

- Adapter tests prove exact model and effort projection for active one-shot and persistent paths.
- No active launch relies on an external model default.
- No adapter selects or substitutes configuration.
- No general transport capability redesign was added.

### Milestone 3 — Migrate Brain consumers

**Effort:** 3–5 days

- Migrate supported decision, planning, review, projection, evaluation, roadmap, milestone, completion, and scoped-operation launches.
- Use the same loaded Brain configuration in recommendation generation.
- Preserve existing sandbox, approval, network, working-directory, and lifetime behavior.
- Replace only model-and-effort literals and factory parameters made obsolete by Brain authority.

**Exit criteria:**

- Every supported non-execution launch receives the composition's Brain configuration.
- Non-execution factories do not independently select model or effort.
- Default behavior is explicitly `gpt-5.6-sol` and `xhigh`.
- Brain configuration is loaded once per composition.

### Milestone 4 — Generate, validate, and persist recommendations

**Effort:** 3–5 days

- Define the two-property recommendation contract and canonical allowlists.
- Implement one strict parser and semantic validator.
- Render the recommendation prompt from the same allowed-value definitions.
- Run the recommendation turn immediately after successful First and Next proposal turns in the same Brain-configured session.
- Persist the minimal `Model`, `Effort`, and `PromptHash` sidecar through `LoopArtifacts`.
- Fail the decision transition on invalid output or incomplete persistence.

**Exit criteria:**

- First and Next both produce proposal then recommendation in deterministic order.
- Valid recommendations persist with the exact prompt hash.
- Invalid recommendations produce no launchable decision pair.
- No recommendation identity, lifecycle store, execution attempt, or new workflow product was introduced.

### Milestone 5 — Gate and configure active execution

**Effort:** 2–4 days

- Extend existing `DecisionSet` readiness or launch validation to require the recommendation sidecar.
- Load and validate the prompt/recommendation pair immediately before execution.
- Remove the active implementation path's hardcoded `medium` effort.
- Build the execution session only from the validated recommendation.
- Keep the existing held-open session for handoff with unchanged configuration.
- Confirm Brain configuration is unavailable to execution construction.

**Exit criteria:**

- Initial and Next execution prompts use their own matching recommendations.
- Missing, malformed, unsupported, or mismatched recommendation data blocks launch.
- Work and handoff use the same immutable model and effort.
- The existing workflow product is retained unless a documented blocker proves it insufficient.
- Restart-safe execution lifecycle work remains deferred.

### Milestone 6 — Focused cleanup and verification

**Effort:** 1–2 days

- Remove superseded supported-path effort literals and raw model overrides.
- Update focused architecture documentation.
- Add repository scans for new supported launch sites that omit authority or choose independent model/effort values.
- Run targeted and full regression tests.
- Record deferred cleanup and lifecycle ideas separately without implementing them.

**Exit criteria:**

- No supported production launch omits explicit model or effort.
- No active execution launch accepts independent configuration or uses Brain fallback.
- No supported non-execution launch owns independent model or effort.
- Full build and relevant regression suites pass.

### Estimated total

**14–25 focused engineering days**

The largest required uncertainty is the installed Codex app-server boundary for explicit model propagation. Resolve that at the adapter seam. It does not, by itself, justify execution-attempt persistence or a broader lifecycle redesign.

## Testing Strategy

Testing should prove the feature invariants without presupposing deferred infrastructure.

### Contract and settings tests

- Explicit model, effort, and authority are required for supported session construction.
- `xhigh` is canonical and invalid effort strings fail.
- Reserved startup-option overrides are rejected.
- Default, custom, missing, blank, and invalid Brain settings behave as specified.
- Brain settings are loaded once and supplied to all supported non-execution factories.

### Recommendation tests

- Every allowed model and effort combination parses and validates.
- Malformed JSON, fences, commentary, missing/duplicate/unknown properties, wrong types, wrong casing, and unsupported values fail.
- First and Next proposals are each followed immediately by recommendation generation.
- Both turns use Brain configuration.
- Prompt hash is computed from the exact persisted execution prompt.
- Persistence failure leaves no launchable matching pair.

### Launch and transport tests

- One-shot and persistent adapters project the exact canonical model and effort.
- Model or effort cannot be overridden through generic startup options.
- Active implementation launch uses only a validated matching recommendation.
- Missing sidecar, invalid sidecar, and prompt-hash mismatch block before opening a session.
- Brain configuration is never consulted as execution fallback.
- Work and handoff retain one session specification.

### Consumer and regression tests

- Capture every supported non-execution launch and assert the shared Brain model and effort.
- Assert that posture settings remain unchanged during migration.
- Cover the active unified execution path and its First and Next variants.
- Keep existing legacy-path tests unless cleanup is separately authorized.
- Run the full solution build and regression suite.

Do not add recommendation retirement, active-query, attempt-state, thread-resume, cancellation-recovery, or transaction-migration tests unless the corresponding deferred design is separately approved.

## Risks

| Risk | Mitigation |
|---|---|
| Persistent Codex cannot express explicit model | Verify the narrow supported boundary early; fail clearly instead of relying on external defaults |
| A support agent is mistaken for execution because of `SessionRole` | Classify authority explicitly at session construction |
| Brain values leak into execution | Execution factory accepts only a validated prompt/recommendation pair |
| Recommendation and prompt become mismatched | Persist and verify the exact prompt hash at launch |
| Two-file persistence is partially updated | Treat only a valid hash-matching pair as launchable; fail the generating transition on either write failure |
| Existing workflow cannot validate the sidecar cleanly | Document the concrete limitation before adding a new workflow product |
| Raw options override canonical values | Reserve and reject model and effort keys |
| Migration changes posture | Test authority separately from sandbox, approval, network, and permission behavior |
| Legacy compiled paths distract or expand scope | Adapt mechanically only if shared contract compilation requires it; defer removal and redesign |
| Broader lifecycle work re-enters the critical path | Require direct evidence that a deferred item blocks a stated acceptance criterion |

## Acceptance Criteria

The implementation is accepted when all of the following are true:

- `BrainModel` and `BrainEffort` are loaded once per composition and are the exclusive model-and-effort authority for every supported non-execution agent.
- Default Brain literals exist only in `config/settings.default.json` and are `gpt-5.6-sol` and `xhigh`.
- First and Next execution-system-prompt generation are each followed immediately by structured recommendation generation.
- The agent-facing output contains exactly `Model` and `Effort` and accepts only the four specified models and four specified efforts.
- The persisted recommendation contains the validated pair and a hash of the exact generated execution prompt.
- The existing workflow rejects a decision that lacks a valid matching recommendation.
- Every active implementation launch derives model and effort exclusively from the validated persisted recommendation.
- Handoff uses the same held-open session and unchanged configuration as implementation work.
- Missing, malformed, unsupported, unpersisted, or prompt-mismatched recommendation state blocks execution.
- Brain configuration, external Codex defaults, and the historical `medium` value are never execution fallbacks.
- The canonical session contract owns explicit model and effort, and active transports faithfully project them.
- Raw startup options cannot override model or effort.
- Existing posture and permission behavior is preserved.
- Focused and full regression tests pass.

The feature does not require:

- restart-safe implementation or handoff recovery;
- execution-attempt or thread-identity persistence;
- recommendation lifecycle or retirement infrastructure;
- a new workflow product;
- transaction or settings-ownership redesign;
- removal of legacy execution implementations.

## Definition of Complete

The work is complete when the active architecture has two—and only two—model-and-effort authorities:

1. Brain configuration supplies `BrainModel` and `BrainEffort` to supported non-execution agents.
2. A validated, persisted, prompt-matching execution recommendation supplies `Model` and `Effort` to the active implementation session and its same-session handoff.

Completion additionally requires strict recommendation validation, minimal durable prompt correlation, explicit transport-neutral session fields, faithful active-transport projection, fail-closed launch behavior, and focused regression protection.

Broader execution-lifecycle, persistence, workflow-product, settings-ownership, and dead-code cleanup remain separate follow-up decisions.
