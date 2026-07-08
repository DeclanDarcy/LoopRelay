**Transition Audited**

`SelectNextEpic`, implemented mainly by [RoadmapStateMachine.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:578). Scope is the transition from `RoadmapCompletionContextReady` to `SelectNextStrategicInitiative`, ending after the selection is materialized, parsed, recorded, and returned. Downstream `EpicPreparationAudit`, `CreateNewEpic`, `SplitEpic`, and `GenerateMilestoneDeepDivesForEpic` are out of scope.

**1. Transition Narrative**

Current state: roadmap completion context is ready and roadmap source files exist.

Goal: choose the next strategic initiative and persist that choice as the active selection.

Major steps: ensure the `SelectNextEpic` projection, build selection context, capture transition inputs, run the runtime prompt, persist the selection output, record evidence/provenance/lifecycle/ledger state, parse the decision, and return it to the router.

Completion: `.agents/selection.md` exists, selection evidence/provenance/lifecycle/decision ledger are updated, transition state and journal reflect completion, and the parsed `SelectionDecision` is available for routing.

**2. Current Execution Trace**

1. `RunSelectionAndFollowingAsync` calls `SelectNextInitiativeAsync`.
2. Emit console phase: `Select next strategic initiative`.
3. Load prompt contract for `SelectNextEpic`.
4. `ProjectionCache.EnsureAsync` gets projection definition, reads `.agents/projections/select-next-epic.md`, optionally runs `ProjectionForSelectNextEpic`, validates content, updates projection manifest, writes generated projection, or writes blocker evidence and throws on invalid/stale projection.
5. Load current roadmap state for retired epic context.
6. Build selection context: read roadmap completion context, list/read `.agents/roadmap/*.md`, render source references, render retired epics, reject raw project-context markers.
7. Resolve transition input snapshot: projection, roadmap completion context, roadmap sources, prompt context hash, empty secondary-input hash, snapshot hash.
8. Create correlation id, timestamp start, start stopwatch.
9. Save state as `SelectNextStrategicInitiative`, `Started`, decision `Pending`.
10. Append `TransitionStarted` journal row.
11. Render `SelectNextEpic` runtime prompt, append prompt policy, run read-only planning agent turn, stream/echo output.
12. On success, stop stopwatch, timestamp completion.
13. Append `TransitionCompleted` journal row.
14. Save state as `SelectNextStrategicInitiative`, `Completed`, decision `Completed`.
15. Write prompt output to `.agents/selection.md`.
16. Optionally capture HITL-request markers from selection output.
17. Write numbered evidence under `.agents/evidence/selection/selection.NNNN.md`.
18. Record active selection provenance in `.agents/selection-provenance-manifest.json`.
19. Mark `.agents/selection.md` lifecycle `Ready` with evidence path.
20. Parse `## Recommendation Summary` into `SelectionDecision`.
21. Append decision ledger entry.
22. Return `SelectionDecision`.
23. `ContinueAfterSelectionAsync` routes the parsed decision; downstream transitions are separate. Terminal outcomes currently append another decision and save terminal state.

**3. Concern Inventory**

Projection ensure: projection cache, prompt execution, validation, manifest persistence, blocker recovery.

Context build: artifact reads, input resolution, context formatting, retired-epic state inclusion.

Shared transition runner: input snapshotting, state mutation, journaling, prompt execution, timing, failure persistence.

Selection materialization: artifact write, optional HITL capture, evidence write, provenance write, lifecycle write.

Decision handling: parsing, validation, decision ledger append, routing handoff.

Mixing begins most strongly in `RunPromptTransitionWithCompletionAsync` at [RoadmapStateMachine.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1404) and in the post-output block at [RoadmapStateMachine.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:595).

**4. Hidden Steps**

Hidden but real steps are: projection freshness admission, prompt contract lookup, transition input snapshot capture, state-start persistence, journal-start persistence, runtime prompt rendering, prompt-policy append, silent-output echo, state-complete persistence, numbered evidence allocation, selection provenance hashing, lifecycle upsert, decision-id allocation, and downstream resume-safety compatibility.

**5. Natural Step Boundaries**

Step 1: Ensure projection. Inputs: `ProjectContext`, contract. Outputs: valid projection content, manifest update, possible blocker.

Step 2: Build context. Inputs: projection, completion context, roadmap files, retired epics. Output: rendered selection prompt context.

Step 3: Start transition. Inputs: context, projection, output path. Outputs: input snapshot, started state, started journal.

Step 4: Run prompt. Inputs: runtime prompt name, context, empty secondary input. Output: raw markdown selection.

Step 5: Complete transition. Inputs: raw output, snapshot, timing. Outputs: completed journal/state.

Step 6: Materialize selection. Inputs: raw output. Outputs: selection artifact, evidence artifact, provenance manifest, lifecycle entry, optional HITL ledger updates.

Step 7: Interpret selection. Inputs: raw output. Outputs: parsed `SelectionDecision`, decision ledger row.

**6. Mixed-Concern Analysis**

Projection ensure both prepares an input and mutates durable projection state; a reader must understand caching before understanding selection.

The shared prompt runner both executes the agent and owns state/journal/failure persistence; prompt behavior and recovery behavior are interleaved.

Selection materialization writes four different persistence surfaces before parsing validates the selection format. If parse fails, the selection artifact/evidence/provenance/lifecycle may already exist while the decision ledger append has not happened.

`ContinueAfterSelectionAsync` mixes routing with terminal state persistence and duplicate decision-ledger appends for non-initiative outcomes.

**7. Data Flow**

`ProjectContext` comes from preflight and feeds projection provenance plus prompt rendering.

Projection content comes from cache/generation and feeds context building plus input snapshot hashing.

Roadmap completion context and roadmap source files come from `.agents/core` and `.agents/roadmap`; they feed prompt context and transition input snapshot.

Retired epics come from persisted state; they feed prompt context and selection provenance.

Raw prompt output becomes `.agents/selection.md`, numbered evidence, optional HITL capture source, parser input, provenance hash input, and lifecycle target.

Parsed `SelectionDecision` feeds decision ledger and downstream router.

**8. Human Navigation Audit**

Minimum path:

- [RoadmapStateMachine.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:494): entry, selection, routing, shared transition runner, state save.
- [ProjectionCache.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/ProjectionCache.cs:10): projection admission and blocker behavior.
- [RoadmapPromptContextBuilder.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs:12): selection context.
- [TransitionInputs.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/TransitionInputs.cs:9): input snapshot.
- [RoadmapPromptRunner.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs:25): agent execution.
- [SelectionParser.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/SelectionParser.cs:24): parse/validation.
- [SelectionProvenance.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/SelectionProvenance.cs:130), [DecisionLedgerStore.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/DecisionLedgerStore.cs:15), [ArtifactLifecycleStore.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs:40), [TransitionJournalStore.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs:9), [RoadmapStateStore.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs:15).

**9. Extraction Boundary**

Smallest useful extraction: a named `SelectNextEpic` transition handler that starts at the body of `SelectNextInitiativeAsync` and ends after `AppendDecisionAsync` returns the parsed `SelectionDecision`.

Keep `ContinueAfterSelectionAsync` outside, except preserve its current terminal-outcome behavior separately. Do not pull epic audit, epic creation, split handling, or milestone generation into this handler.

**10. Required Inputs**

Required: `ProjectContext`, `CancellationToken`, contract lookup, projection cache, context builder, transition input resolver, prompt runner, artifact store, state store, manifest store, journal store, selection provenance service, lifecycle store, decision ledger, console phase sink.

Required artifacts: roadmap completion context, roadmap source files, `SelectNextEpic` projection or ability to generate it.

Optional: HITL request capture service.

Incidental to selection logic but currently needed for identical state snapshots: active artifact status lookup, split-family count, existing blockers, existing transition intent, retained retired epics.

**11. Required Outputs**

Returned parsed `SelectionDecision`.

Persisted outputs: projection file when generated, projection manifest, state JSON, transition journal JSONL, `.agents/selection.md`, numbered selection evidence, selection provenance manifest, lifecycle JSON, decision ledger JSON.

Console output: selection phase plus agent turn stream/echo.

Failure outputs: projection blocker evidence for invalid/stale projection; `EvidenceBlocked` state and `TransitionFailed` journal for prompt failures; cancellation state for `OperationCanceledException`.

**12. Behavioral Equivalence Contract**

Preserve the same input artifacts, prompt names, projection path, prompt context shape, empty secondary input, prompt policy append, read-only planning agent spec, output path `.agents/selection.md`, journal event names, state transition fields, decision ledger field values, lifecycle state `Ready`, selection provenance hash inputs, numbered evidence naming, optional HITL capture behavior, and exception routing.

Also preserve the current ordering: completed state is saved before selection artifact materialization and before decision ledger append.

Do not treat helper names, local variable names, or an extracted class name as externally observable.

**13. Transition Handler Shape**

`Execute(projectContext, cancellationToken)`

↓ announce phase

↓ load contract and ensure projection

↓ load existing state/retired epics

↓ build selection context

↓ resolve input snapshot

↓ persist started state and journal

↓ run `SelectNextEpic`

↓ persist completed journal and state

↓ write selection artifact

↓ capture optional HITL requests

↓ write selection evidence

↓ record selection provenance

↓ update lifecycle

↓ parse selection

↓ append decision ledger

↓ return decision

**14. Readability Improvements**

Extraction would turn the transition into one linear reading path instead of requiring jumps between orchestration, shared prompt execution, state persistence, evidence writing, provenance, parser, and router code.

The failure path becomes easier to see because prompt failure persistence and projection-blocker behavior can be named steps.

The post-output ordering becomes explicit, especially the current “write/provenance/lifecycle before parse” behavior.

Downstream routing becomes easier to reason about because `SelectNextEpic` would return one decision instead of being visually glued to epic audit/create/split/milestone work.