# Semantic Architecture Migration Roadmap

## Purpose

This roadmap is the companion migration track for `plan.md`.

`plan.md` answers:

```text
How do we implement the semantic constitution?
```

This roadmap answers:

```text
How do we transform the current LoopRelay implementation into that architecture?
```

The migration track does not replace the constitutional architecture. It builds the bridge from the current Roadmap CLI state-machine implementation to the semantic realization plan while preserving current behavior and making the flow easier for human engineers to inspect, navigate, and safely change.

## Source Authority

- `semantic-constitution.md` remains the authority for target semantics.
- `canonical-semantic-architecture-roadmap.md` remains the authority for semantic dependency order.
- `plan.md` remains the authority for Track A semantic realization.
- `state-machine-refactor-audit.md` is the primary evidence for the current state-machine implementation and its navigation problems.

Migration artifacts are not semantic primitives. They are temporary or transitional structures used to map, wrap, replace, and retire existing implementation paths without losing behavior.

## Migration Posture

The migration should recover before replacing.

Use this order of force:

```text
current behavior recovery
  -> explicit flow and transition map
  -> ownership and concern boundary map
  -> migration seams
  -> one semantic wrapper over one existing path
  -> incremental replacement
  -> legacy retirement
```

Rules:

- Preserve current externally visible behavior until a semantic replacement is executable and evidenced.
- Prefer one recovered transition path before a broad orchestration rewrite.
- Prefer one explicit ownership boundary before a new abstraction layer.
- Prefer a compatibility seam that can be retired over a permanent bridge abstraction.
- Do not treat legacy persisted state, prompt output, report text, or file writes as semantic authority by migration convenience.
- Do not use migration structure to change the semantic constitution.

## Human Navigability Axis

Every migration milestone must improve or preserve human navigability.

The evaluation gate must answer:

- Can an engineer identify the next transition by reading one authoritative place?
- Can an engineer understand a state without reconstructing hidden context from unrelated helpers?
- Can a transition be followed linearly from admission through effects and persistence?
- Can prompt output, parser output, evidence, accepted decision, artifact effect, state effect, and report be distinguished?
- Can a new state or transition be added without touching unrelated capabilities?
- Can a blocked or failed condition be traced to its preserved evidence and recovery rule?

This is not a substitute for executable semantic evidence. It is an additional migration criterion that preserves the original human-centric motivation for the work.

## Migration Phase 0: Current State Machine Recovery

### Goal

Recover the current Roadmap CLI state machine as it actually executes today.

### Smallest Work

Create a current-state-machine recovery artifact from implementation evidence. It should identify:

- command entry points;
- persisted states;
- startup and resume routing;
- transition actions;
- prompt-backed steps;
- parser and route-table decisions;
- artifact, lifecycle, journal, and state-store effects;
- blocker and recovery paths;
- legacy states that are retained but no longer advanced.

### Durable Artifacts

- Current state inventory.
- Current transition inventory.
- Current execution-flow map.
- Legacy-state inventory.
- Hidden-dependency notes for prompt names, parser strings, artifact lifecycle, projections, and persisted state.

### Evaluation Gate

HITL can select a persisted state and identify the next possible transition, required inputs, decision point, effects, and blocker path without reading the entire orchestrator.

### Do Not Do Yet

- Do not change runtime behavior.
- Do not introduce semantic wrappers.
- Do not rename states or prompts.
- Do not remove legacy states.

## Migration Phase 1: Explicit Flow Map and Transition Inventory

### Goal

Turn recovered behavior into a navigable flow map that can guide semantic wrapping.

### Smallest Work

Represent each current transition as a single inventory entry containing:

- source state or command condition;
- admission guard;
- inputs and freshness requirements;
- execution owner;
- observation source;
- parser or policy decision;
- accepted effect;
- artifact, lifecycle, journal, and state persistence effects;
- report output;
- blocker or recovery target.

### Durable Artifacts

- Transition catalog.
- Flow diagram or table.
- State-to-next-transition index.
- Effect inventory.

### Evaluation Gate

An engineer can answer "what happens next?" for every supported current state by reading the transition catalog and state-to-next-transition index.

### Do Not Do Yet

- Do not collapse transition entries into broad categories that hide differences.
- Do not infer target semantics where current behavior has not been recovered.
- Do not move code solely to match the inventory.

## Migration Phase 2: Ownership and Concern Boundary Map

### Goal

Separate current mixed concerns into explicit responsibility boundaries before replacing them.

### Smallest Work

For each transition family, identify the current owner or missing owner for:

- transition admission;
- prompt or command execution;
- observation capture;
- parsing and validation;
- decision acceptance;
- artifact mutation or promotion;
- lifecycle movement;
- state persistence;
- reporting;
- blocker creation and recovery.

### Durable Artifacts

- Ownership map.
- Mixed-concern findings.
- Candidate extraction boundaries.
- Compatibility risks.

### Evaluation Gate

HITL can distinguish which current component owns routing, evidence, artifact mutation, lifecycle movement, state persistence, and recovery for a representative transition.

### Do Not Do Yet

- Do not create permanent owner abstractions before one semantic wrapper proves the boundary.
- Do not move responsibilities if the move would obscure current behavior.
- Do not treat ownership labels as authority unless the semantic plan has made that authority executable.

## Migration Phase 3: Migration Seams

### Goal

Identify the smallest seams where semantic realization can wrap current behavior without forcing a full rewrite.

### Smallest Work

Choose one representative current path and define its migration seam:

- legacy entry point;
- semantic subject and intent adapter;
- protocol admission hook;
- interaction envelope boundary;
- observation and evidence capture boundary;
- decision and effect boundary;
- compatibility persistence boundary;
- rollback or blocker behavior.

### Durable Artifacts

- Seam definition for one current transition path.
- Compatibility contract.
- Retirement condition for the seam.
- Risk notes for persisted-state and artifact compatibility.

### Evaluation Gate

HITL can see where the semantic path begins and ends, what legacy behavior remains inside the seam, and what evidence proves the wrapper did not silently change behavior.

### Do Not Do Yet

- Do not build a generic compatibility platform.
- Do not wrap every transition at once.
- Do not let compatibility persistence become the new semantic authority.

## Migration Phase 4: First Semantic Wrapper

### Goal

Run one existing transition path through the semantic realization model while preserving compatibility with current behavior.

### Smallest Work

Wrap one narrow current path with:

- `RepositoryWork` subject identity;
- subject-bound intent;
- protocol admission;
- interaction envelope;
- observation capture;
- validation and evidence binding;
- accepted decision or report-only classification;
- authorized state or artifact effect;
- compatibility write to existing persistence only where needed.

### Durable Artifacts

- Wrapper admission record.
- Interaction and evidence record.
- Decision or report-only record.
- Compatibility write record.
- Behavior-equivalence evidence against the legacy path.

### Evaluation Gate

The wrapped path produces the same externally visible result as the legacy path while adding explicit semantic lineage and a navigable transition record.

### Do Not Do Yet

- Do not remove the legacy path until rollback and equivalence are proven.
- Do not broaden the wrapper beyond the selected path.
- Do not use wrapper success as evidence that unrelated transitions are migrated.

## Migration Phase 5: Incremental Replacement and Legacy Retirement

### Goal

Replace current state-machine paths incrementally and retire compatibility only when semantic behavior is complete.

### Smallest Work

For each migrated path:

- compare legacy and semantic effects;
- preserve or migrate required persisted state;
- update command admission to prefer the semantic path;
- keep a rollback path until blockers, recovery, and reports are equivalent;
- retire legacy code and compatibility artifacts only after acceptance.

### Durable Artifacts

- Per-transition migration status.
- Equivalence evidence.
- Retired legacy-path record.
- Compatibility removal record.

### Evaluation Gate

HITL can identify which transitions are legacy, wrapped, semantic-primary, or retired, and can trace why any compatibility layer still exists.

### Do Not Do Yet

- Do not remove compatibility before recovery and reporting are represented.
- Do not retire legacy state values until persisted-state migration is explicit.
- Do not let partially migrated transitions share ambiguous ownership.

## Migration Acceptance Baseline

The migration track is sufficient when the system can demonstrate this path:

```text
recover current state-machine behavior
  -> publish explicit state and transition inventory
  -> map ownership and mixed concerns
  -> select one migration seam
  -> wrap one current path semantically
  -> prove behavior equivalence
  -> make semantic path primary
  -> retire the legacy path and compatibility seam
```

The first pass may cover only one representative path. The important property is that the migration path is explicit, reversible while incomplete, and grounded in both current behavior evidence and constitutional target semantics.
