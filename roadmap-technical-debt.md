# Roadmap Technical Debt

A register of roadmap CLI-specific deferred changes. Each item states what is
deferred, why it matters, and the path to resolve it.

Newest section first.

> Convention: when an item is resolved, delete it. Git history keeps the record.

---

## 2026-07-06 - CreateRoadmapCompletionContext ignores completed epic history

### RTD-1 - Bootstrap roadmap completion context does not discover completed epics

**Deferred.** `CreateRoadmapCompletionContext` is designed to synthesize the initial
`.agents/core/roadmap-completion-context.md` from:

- the roadmap completion projection, which defines desired strategic state
- completed epic history, which should provide evidence of current strategic state

The prompt template has an explicit `<COMPLETED_EPICS>` input slot:

- `src/CommandCenter.Core/Prompts/Planning/CreateRoadmapCompletionContext.prompt`

However, the roadmap CLI bootstrap path does not populate that slot. In
`RoadmapStateMachine.BootstrapRoadmapCompletionContextAsync`, the runtime prompt
context contains only projection content and the prompt is invoked with an empty
secondary input:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapPromptCatalog.cs`

The prompt contract and input resolver also model `CreateRoadmapCompletionContext`
as having no completed-epic inputs:

- `PromptContractRegistry` registers no required inputs for
  `CreateRoadmapCompletionContext`.
- `TransitionInputResolver` adds no prompt inputs for
  `CreateRoadmapCompletionContext`.
- `TransitionInputResolverTests.Create_completion_context_resolves_projection_as_single_artifact_input`
  asserts that the projection is the only artifact input.

As a result, the initial roadmap completion context is created without scanning
or summarizing completed work from `.agents/archive/epics/*`, lifecycle state,
prior execution evidence, or any other completed-epic source.

**Why deferred:** this is more than a prompt tweak. A safe fix needs a durable
completed-epic evidence contract, provenance hashing, input freshness semantics,
and tests that distinguish historical archived epics from the normal active-epic
completion update path.

**Impact:** the first generated roadmap completion context can claim to reason
from completed epics while receiving none. Future selection then treats that
bootstrap context as current strategic state, so roadmap selection, retirement,
realignment, and drift reasoning may begin from an under-informed state. Existing
archives in `.agents/archive/epics/*` are effectively invisible to bootstrap.

This does not affect the later active-epic closure flow: after execution reports
`Epic Complete`, the CLI runs `EvaluateEpicCompletionAndDrift`, routes `Close Epic`
or `Close With Follow-Up` through `UpdateRoadmapCompletionContext`, and updates the
completion context from the active epic plus completion evaluation. The gap is
specific to initial bootstrap.

**Resolution:** add an explicit completed-epic evidence loader for
`CreateRoadmapCompletionContext`.

Recommended shape:

1. Define canonical completed-epic sources, likely starting with
   `.agents/archive/epics/*/plan.md` plus selected evidence files needed for
   strategic synthesis.
2. Add a compact renderer for completed epic evidence that preserves epic ID,
   title/name, completion evidence quality, relevant implementation evidence,
   and strategic summary without dumping full archive contents.
3. Pass that rendered evidence as the `secondaryInput` to
   `CreateRoadmapCompletionContext`.
4. Teach `PromptContractRegistry` and `TransitionInputResolver` that bootstrap
   depends on the completed-epic evidence set.
5. Record hashes of the selected completed-epic inputs in the transition journal
   so bootstrap output provenance reflects the history it used.
6. Update tests so bootstrap with archived epics includes completed-epic input,
   and bootstrap with no completed epics records an explicit empty evidence set
   rather than silently passing a blank slot.

**Open design questions:**

- Should archived epic directories be treated as complete solely by location, or
  should completion require explicit lifecycle/closure evidence?
- Should old plan CLI archives and roadmap CLI active-epic closures share one
  completed-epic evidence schema?
- How much archived evidence can be included before bootstrap becomes too large
  for a planning prompt?
- Should missing or malformed archive records block bootstrap, degrade evidence
  quality to `Weak`/`Unclear`, or be excluded with a warning artifact?
