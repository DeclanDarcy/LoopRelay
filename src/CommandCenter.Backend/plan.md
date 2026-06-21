# Cognitive Architecture Reconstruction - Roadmap (`plan.md`)

**Subject:** `src/CommandCenter.Backend` (single-assembly modular monolith, .NET 10, 135 `.cs` files, 7 flat feature folders)
**Informed by:** `COGNITIVE_AUDIT.md` (findings C1-C9), `ARCHITECTURE_AUDIT.md` (findings A1-A13), and roadmap review feedback dated 2026-06-20
**Date:** 2026-06-20

> **Purpose.** This is not a code-quality, test-coverage, or runtime-architecture plan. Its single goal is to transform the repository from incidental organization into intentional cognitive architecture: the repository should teach itself, so an engineer can understand the major subsystems from the filesystem without reading large quantities of code.
>
> **Method.** Reveal architecture that already exists; do not invent it. Proposed folders, state homes, and extracted concepts remain hypotheses until the discovery milestones prove they match how engineers navigate, modify, and review the system. Behavior is preserved; the 192-test suite is the safety net that guarantees it.

---

## Executive Summary

The backend is excellent at location and poor at orientation (`COGNITIVE_AUDIT.md` headline). A named type is found instantly: naming is consistent, one type per file, namespace mirrors folder. But a subsystem cannot be discovered: two of seven folders (`Execution` 58 files, `Continuity` 50 files) are flat file-seas, each hiding several real subsystems; behavior is invisible from routes (all 35 endpoints inline in one `Program.cs` method); and the load-bearing concepts - the execution state machine, the git-porcelain parser, the information-tier classifier - have no structural home.

The fix is still expected to be mostly re-foldering and promotion, not rewriting. The important revision is sequencing: the roadmap no longer jumps from audit findings directly to a committed target tree. The early milestones must prove the cognitive topology first:

1. Ratify the constitutional rules and vocabulary.
2. Inventory what exists: candidate concepts, authority claims, state progression, workflow touchpoints, and hidden concepts.
3. Measure how humans interact with that inventory: what files are traversed, searched, modified, and reviewed together.
4. Only then ratify the target structure and begin moves.

The likely high-leverage moves remain the same candidates: expose `Execution` and `Continuity` subsystems, split the monolithic composition root, give each state machine one named home, and promote hidden concepts into named files. They are now treated as hypotheses until M1.5/M3 proves them, not as answers smuggled into the plan before discovery.

**What is explicitly preserved:** the feature-folder + namespace-mirrors-folder scheme, one-concept-per-file granularity, the `IFoo -> Foo` convention, the clean `IProcessRunner`/`IGitService` shell-out boundary, centralized path containment in `ArtifactPath`, the 192-test architecture, and the five already-legible focused folders. Those are evidence for the target model, not problems to solve.

---

## Architectural Principles

These govern every milestone. They are constraints, not aspirations.

1. **The repository teaches itself.** The desired end state is not merely "organized code." It is a filesystem, namespace, and documentation surface that answers a newcomer's first architectural questions before they grep.
2. **Reveal, don't invent.** Every folder, file, and named type introduced must correspond to a concept proved to already exist in code, workflow, co-change history, or review behavior. No `Core`, `Shared`, `Common`, `Infrastructure`, or `Utilities` buckets.
3. **Do not approve the answer before discovery.** Target structures are hypotheses until the topology discovery gate certifies them. M1 and M1.5 exist to prevent "justify the tree we already chose" thinking.
4. **Optimize for engineer reasoning, not aesthetics.** Each milestone must measurably improve at least one of: discoverability, navigability, traceability, ownership clarity, workflow visibility, state visibility, or predictability.
5. **Legibility beats granularity.** Architecture must become more legible, not more fragmented. Do not create tiny nested folders such as `Sessions/Mutations`, `Sessions/Validation`, or `Sessions/Persistence` unless engineers demonstrably reason that way. Folder count and file count are heuristics, not goals.
6. **State progression is foundational.** State-machine ownership is determined before folder ownership is finalized. Folder moves cannot obscure or split the authority that governs lifecycle transitions.
7. **Promote hidden concepts as architecture, not cleanup.** Load-bearing concepts such as parsers, classifiers, mutation models, and transition rules are architectural facts. They must become named, navigable concepts early enough to shape the topology.
8. **Structure before rewrite.** Prefer file moves, folder restructuring, subsystem promotion, and state-machine surfacing over implementation rewrites. When a behavioral change and a structural change both achieve visibility, choose the structural one.
9. **Preserve behavior.** Behavioral changes are permitted only where required to expose structure, and only with characterization tests that prove equivalence.
10. **Prove every boundary.** A subsystem earns a folder only when evidence shows its files change together, are searched together, reviewed together, or form an authority boundary. Single-file pseudo-subsystems are colocated, not enshrined.
11. **Preserve history.** All moves use `git mv` so blame and history survive the reconstruction.

---

## Current-State Assessment

This section records evidence and candidate topology. It does not approve the target tree.

### How engineers currently reason about the system

Engineers navigate by three distinct modes, with sharply different success rates:

- **Structure for location works.** Namespace = folder and one-type-per-file mean a known type name resolves instantly. This is the repository's genuine strength.
- **Search for behavior is forced.** Route names carry little behavioral signal and services expose one-method public surfaces, so learning what anything does requires grep/go-to-definition into a service body (`COGNITIVE_AUDIT.md` C2, C9).
- **Luck for system context is fragile.** The 3-stack relationship (React UI / Rust-Tauri shell / .NET sidecar) is documented only in `docs/architecture.md` at the repo root; there is no in-backend README and zero `<summary>` comments (C8).

The decisive gap: engineers reason in subsystems and workflows (sessions, git, providers, proposals, compression, promotion, recovery), but the filesystem mostly offers only domain-level buckets (`Execution`, `Continuity`). The structure is one level coarser than human reasoning in the two largest folders.

### Candidate latent subsystems

Every item below is a candidate cluster the audits located in existing files. M1 records what exists; M1.5 tests how engineers interact with those concepts; M3 decides which candidates deserve folders, which deserve named files, and which should remain colocated.

**Inside `Execution/` (58 flat files):**

| Candidate concept | Representative files | Evidence to prove |
|---|---|---|
| Session lifecycle | `ExecutionSession*`, `ExecutionSessionService`, recovery hosted service | Owns lifecycle state progression? |
| Commit / push | `Commit*`, `Push*` | Peer subsystem or nested session phase? |
| Git integration | `GitService`, `RepositoryGitStatus`, `*DirtyState`, snapshots | Git output parsing and status ownership |
| Process control | `ProcessRunner`, `ProcessRun`/`StartResult` | Shell-out authority boundary |
| Providers | `*ExecutionProvider`, Codex resolver, `Fake`/`Noop` | Agent backend substitution boundary |
| Execution context | `ExecutionContext*` | Context assembly authority |
| Prompt building | `ExecutionPrompt*` | Prompt shape authority |
| Monitoring / events | `ExecutionMonitoringService`, `ExecutionEvent*` | SSE/event ownership |
| Persistence | `FileSystemExecutionSessionStore` | Store concern or session concern? |
| Handoff | `HandoffService` | Bridge file, not necessarily a subsystem |

**Inside `Continuity/` (50 flat files):**

| Candidate concept | Representative files | Evidence to prove |
|---|---|---|
| Proposals & lifecycle | `OperationalContextProposal*`, review/lifecycle services, proposal store | Owns proposal state progression? |
| Generation & document model | `*GenerationService`, `OperationalContextDocument/Item/Section/Tier` | Producer-owned model or shared model? |
| Understanding / compression | `UnderstandingCompressionService`, diff service, ledger, snapshots | Hidden tier classifier and compression workflow |
| Parsing | `MarkdownOperationalContextParser`, semantic-change types | Parser/model authority |
| Decisions | `DecisionAnalysisService`, `Decision*` taxonomy | Decision taxonomy ownership |
| Diagnostics / reporting | `ContinuityDiagnosticsService`, `ContinuityReportService` | Reporting concern or colocated service? |

**Already legible candidates to leave unchanged unless M1.5 disproves them:** `Artifacts` (10), `Projections` (6), `Repositories` (4), `Configuration` (3), `Planning` (3). Visible = hidden = actual in these folders; their file counts are descriptive, not the reason they are good.

### Boundary hypotheses to test

The following are hypotheses, not approvals:

- `Process` likely deserves a visible unit because `IProcessRunner` isolates spawning mechanics.
- `Git` likely deserves a visible unit, and the porcelain parser likely deserves a named file.
- `Providers` likely deserves a visible unit because agent backend substitutions change together.
- `Sessions` likely owns execution lifecycle and execution state progression, but M2 must certify every transition site before foldering locks this in.
- `Commit`/`Push` may be a session sub-phase rather than a peer execution subsystem because their valid operations are session-state dependent.
- `HandoffService` is probably colocated with session lifecycle rather than promoted to a one-file folder.
- `Proposals` likely owns operational-context lifecycle and the dual status/review-state coordination, but M2 must prove this before the folder tree is ratified.
- The document model is unresolved: it may belong with `Generation`, or it may deserve a named `Model` unit if topology discovery shows equal-weight consumers.
- `Parsing`, `Decisions`, and `Diagnostics` may deserve visible units only if M1.5 shows they are searched, reviewed, changed, or predicted as distinct concerns.

### Cognitive hotspots

1. **`Execution/ExecutionSessionService.cs` (787 lines, 18 public methods)** - workflow hub and hidden home of execution-state mutation (`ExecutionSessionMutation`, a `file static class` at `:515`), behind a `SemaphoreSlim` gate, with transition guards scattered across multiple sites.
2. **`Program.cs` (713 lines, one `CreateApp` method)** - composition root, full routing table, interface-to-implementation catalog, and repeated exception mapping.
3. **`Execution/GitService.cs` (414 lines)** - git integration and unnamed git-porcelain parser/model sharing one file.

Honorable mentions: `UnderstandingCompressionService` (tier classifier behind one method), `OperationalContextGenerationService` (large helper cluster), `ExecutionMonitoringService` (nested provider-observer).

---

## Discovery Questions

M1 answers "what exists?" by inventorying files, concepts, guards, workflows, and unresolved ownership claims. M1.5 answers "how do humans interact with what exists?" by measuring navigation, search, co-change, review, and prediction behavior before M3 approves the tree.

- What files are traversed together for real questions such as "Where does commit happen?", "Where does recovery happen?", "Where is execution state updated?", and "Where does promotion happen?"
- What concepts are searched together?
- What concepts are modified together?
- What concepts are reviewed together?
- Where would five engineers look first for commit lifecycle logic, recovery lifecycle logic, proposal state changes, git status parsing, and information-tier classification?
- Which landing zones minimize navigation hops without creating fake abstraction buckets?

The output is not a prettier folder tree. The output is an evidence-backed cognitive topology.

---

## Provisional Architecture Hypothesis

This tree is intentionally provisional. It is a candidate map used to guide discovery, not a target approved for implementation. M3 may change it.

```text
CommandCenter.Backend/
  README.md
  Program.cs
  Composition/
    RepositoryEndpoints.cs
    ExecutionEndpoints.cs
    ContinuityEndpoints.cs
    ServiceRegistration.cs

  Execution/
    Sessions/
      ExecutionSession*.cs
      ExecutionSessionState.cs
      RepositoryExecutionState.cs
      ExecutionStateMachine.cs
      ExecutionSessionMutation.cs
      Commit/
      HandoffService.cs
    Git/
      GitService.cs
      GitStatusParser.cs
      RepositoryGitStatus.cs
    Process/
    Providers/
    Context/
    Prompting/
    Monitoring/
    Stores/

  Continuity/
    Proposals/
      OperationalContextProposal*.cs
      OperationalContextProposalStatus.cs
      OperationalContextReviewState.cs
      OperationalContextStateMachine.cs
    Generation/
      InformationTierClassifier.cs
    Understanding/
    Parsing/
    Decisions/
    Diagnostics/

  Artifacts/
  Projections/
  Repositories/
  Configuration/
  Planning/
```

The provisional tree should be rejected or changed if topology discovery shows better landing zones, hidden coupling, weak engineer consensus, or fragmentation that makes a cognitive unit harder to understand.

---

## Milestone Roadmap

Milestones are dependency-ordered. No folder moves occur before M3 ratifies the topology. State-machine ownership and hidden-concept visibility are intentionally moved earlier than folder reconstruction.

### M0 - Cognitive Architecture Ratification

- **Objective:** Establish the shared vocabulary, organizational principles, discovery questions, and success metrics that govern all later milestones.
- **Rationale:** Reconstruction without an agreed rubric reverts to taste. Ratifying the principles and metrics up front makes every later PR reviewable against evidence.
- **Scope:** Create `COGNITIVE_ARCHITECTURE.md` with the Architectural Principles, candidate subsystem glossary, no-junk-bucket rule, anti-fragmentation rule, discovery-question list, M3 rejection criteria, and Success Metrics with current baselines plus target-derivation rules. Ratify that the provisional architecture is not approved until M3.
- **Non-goals:** No file moves. No folder creation. No code edits.
- **Validation:** The constitution enumerates every candidate latent subsystem from the audits and records the evidence required to prove or reject it.
- **Certification:** A reviewer can tell what evidence is required before any folder or hidden concept is approved.

### M1 - Concept, Authority, Workflow, and State Inventory

- **Objective:** Answer "what exists?" by inventorying current concepts, files, authority claims, workflow touchpoints, state-transition sites, hidden concepts, and unresolved ownership questions.
- **Rationale:** M1 is descriptive. It records the architectural facts already present in code before M1.5 studies how engineers interact with those facts.
- **Scope:** For each candidate subsystem, record current files, responsibilities, dependencies, candidate ownership, colocated concepts, and unresolved questions. Inventory every state-transition guard site from C3/A2 and map each to a candidate lifecycle owner. Inventory hidden concepts (`ExecutionSessionMutation`, git parser, tier classifier, fingerprint/decision-signal helpers) and their current host files. Produce a provisional file-assignment hypothesis, clearly labeled as unapproved.
- **Non-goals:** No moves. No renames. No new types. No target-tree approval.
- **Validation:** Every file, transition site, workflow touchpoint, and hidden concept is accounted for. No claim about engineer navigation, consensus, or optimal folder topology is made unless explicitly marked as a raw observation to be tested in M1.5.
- **Certification:** The team can answer what concepts exist and where they currently live without being forced to accept the provisional tree.

### M1.5 - Cognitive Topology Discovery

- **Objective:** Answer "how do humans interact with what exists?" by measuring navigation, search, co-change, review, and prediction behavior against the M1 inventory.
- **Rationale:** The original roadmap's largest risk was knowing the answer too early. M1.5 keeps discovery explicit: the structure must be derived from how engineers actually navigate and how the system actually changes.
- **Scope:** Run workflow topology mapping and engineer navigation analysis:
  - Measure files traversed, searches required, and navigation hops for real questions: commit, recovery, execution-state updates, proposal promotion, git status parsing, tier classification.
  - Review recent change/review history where available: which files were modified or reviewed together?
  - Ask engineers "Where would you look first?" for the same questions and record consensus.
  - Identify optimal landing zones and revise the provisional tree where evidence disagrees with assumptions.
- **Non-goals:** No folder moves. No implementation changes except optional measurement notes/scripts.
- **Validation:** Each proposed landing zone has evidence from traversal, search, co-change, review, or prediction behavior. Weakly supported or fragmenting landing zones are collapsed, revised, or marked unresolved.
- **Certification:** The target topology is discoverable from evidence, not from preference. If engineers disagree on where a concept belongs and no stronger authority evidence explains the disagreement, the plan does not proceed to M3 for that concept.

### M2 - State-Machine Ownership and Characterization

- **Objective:** Determine lifecycle-state ownership before folder ownership is finalized, and characterize the existing behavior.
- **Rationale:** The audits identify state progression as the hardest thing to reason about. State authority should shape folders, not be retrofitted after folders exist.
- **Scope:** For execution lifecycle and operational-context lifecycle:
  - Map every legal transition, guard, and dual-enum consistency rule.
  - Add characterization tests for the current transition behavior.
  - Certify the owning lifecycle concept and candidate state-machine home.
  - Decide whether state-machine files should be created before or during folder moves; if created before moves, they start in the current domain folder and are moved later.
- **Non-goals:** No new states. No transition changes. No folder moves.
- **Validation:** Every transition rule is documented and covered by characterization tests. Every guard site has one future owner.
- **Certification:** An engineer can answer "which concept owns state progression?" before looking at the future folder tree.

### M3 - Target Topology Ratification

- **Objective:** Convert M1/M1.5/M2 evidence into an approved or explicitly rejected target structure and file-assignment manifest.
- **Rationale:** This is the approval gate the previous roadmap was missing. Folder reconstruction starts only after the cognitive topology and state ownership have been proven.
- **Scope:** Produce the final target tree, file-assignment manifest for all 135 files, workflow-owner map, state-machine home map, hidden-concept promotion list, and rejection log. Resolve the document-model boundary using M1.5 evidence.
- **Non-goals:** No moves. No code edits.
- **Validation:** Every file has exactly one approved destination or an explicit decision to remain colocated. Every approved folder has a written authority/topology justification. No folder is justified primarily by file count. No `Core`/`Shared`/`Misc` unit exists.
- **Rejection conditions:** Reject or revise a proposed folder when engineer consensus is weak without stronger ownership evidence, co-change/search/review evidence is weak, the folder is justified only by file count, the structure splits a lifecycle/state authority, or additional nesting increases navigation hops for the questions M1.5 measured.
- **Certification:** Reviewers approve only the topology the evidence supports, and rejected hypotheses are recorded so they do not reappear as assumptions in implementation.

### M4 - Hidden Concept Promotion

- **Objective:** Promote load-bearing concepts hidden inside implementation files into named, navigable files early enough to shape the reconstruction.
- **Rationale:** The git parser, execution session mutation model, tier classifier, and similar concepts are architectural revelation tasks, not late cleanup. Naming them reduces cognitive cost before the big folder moves.
- **Scope:** Promote approved concepts from M3 with move-and-name discipline:
  - `ExecutionSessionMutation.cs` out of `ExecutionSessionService`.
  - `GitStatusParser.cs` plus parsed status model out of `GitService`.
  - `InformationTierClassifier.cs` out of `UnderstandingCompressionService`.
  - Generation fingerprint/decision-signal helpers where M3 proves they form named concepts.
  - If M2 approved immediate state-machine files, introduce `ExecutionStateMachine.cs` and `OperationalContextStateMachine.cs` here with behavior-preserving transition APIs.
- **Non-goals:** No new abstraction layers. No interfaces unless a test already fakes the boundary. No behavior changes.
- **Validation:** Each promoted concept is a top-level type in a like-named file; characterization tests and full suite pass; host hotspot files shrink.
- **Certification:** Previously hidden architectural concepts are findable by name before engineers navigate the final folder tree.

### M5 - Execution Cognitive Reconstruction

- **Objective:** Expose the architecture proven inside `Execution/` by sub-foldering its flat files into the approved units.
- **Rationale:** After topology approval, file moves turn the largest opaque folder into a legible subsystem map while preserving behavior.
- **Scope:** Use `git mv` to move files into the M3-approved execution topology. The provisional expectation is `Execution/{Sessions, Git, Process, Providers, Context, Prompting, Monitoring, Stores}` with `Commit` nested under `Sessions` if M3 confirms it. Update namespaces/usings. Move any M4-promoted concepts into their approved homes.
- **Non-goals:** No behavioral changes. No unapproved renames. No hidden concept extraction not already approved by M3/M4.
- **Validation:** `Execution/` has no loose `.cs` files except an approved README/stub; any large leaf folder has an explicit M3 legibility rationale; any tiny nested folder has anti-fragmentation evidence; build and tests pass.
- **Certification:** An engineer opening `Execution/` can name its subsystems from the folder list without opening a file.

### M6 - Continuity Cognitive Reconstruction

- **Objective:** Expose the architecture proven inside `Continuity/` by sub-foldering its flat files into the approved units.
- **Rationale:** This resolves the second flat mega-folder and makes proposal/generation/understanding/parsing/decision concerns visible.
- **Scope:** Use `git mv` into the M3-approved continuity topology. The provisional expectation is `Continuity/{Proposals, Generation, Understanding, Parsing, Decisions, Diagnostics}` with the document model placed according to M3 evidence. Update namespaces/usings. Move M4-promoted concepts into their approved homes.
- **Non-goals:** No behavior changes. No unapproved enum changes. No late creation of unsupported folders.
- **Validation:** `Continuity/` root holds no loose `.cs` files except an approved README/stub; any large leaf folder has an explicit M3 legibility rationale; any tiny nested folder has anti-fragmentation evidence; build and tests pass.
- **Certification:** An engineer can identify proposal lifecycle, generation, compression/understanding, parsing, and decision concerns from the tree alone.

### M7 - Workflow Visibility

- **Objective:** Make execution lifecycle, proposal lifecycle, promotion lifecycle, and recovery lifecycle structurally identifiable.
- **Rationale:** Subsystems may be visible after M5/M6, but workflows span files and sometimes folders. Engineers must be able to locate the owning coordinator without reading implementation bodies.
- **Scope:** Per the M3 workflow-owner map, ensure each workflow has an obvious coordinating type in an obvious place. If a workflow is still coordinated by a method buried in a large service, promote or rename the coordinator only where M3/M4 approved it. Add the workflow index content that will later land in the backend README.
- **Non-goals:** No broad service redesign. No new orchestration pattern. No behavior changes.
- **Validation:** Each named workflow resolves to a single owning folder and named coordinating type.
- **Certification:** Given a workflow name, an engineer locates its owner from structure in one hop, without grep.

### M8 - Composition Root Decomposition

- **Objective:** Improve intent -> route -> owner -> implementation traceability by splitting the monolithic `Program.cs` into per-domain endpoint and registration modules.
- **Rationale:** All routes and registrations currently share one method. Per-domain modules make routes discoverable by file and shrink the root to host bootstrap.
- **Scope:** Extract `Map*Endpoints(this WebApplication)` extension files into `Composition/` mirroring the approved domains, and `AddCommandCenter()` registration grouping. Move SSE framing out of endpoint lambdas into a named writer if M3/M7 identify it as a visible monitoring concern. Centralize repeated exception-to-HTTP mapping with response-shape characterization.
- **Non-goals:** No route changes, status-code changes, or response-shape changes. No service-internal refactors.
- **Validation:** `Program.cs` is reduced to host bootstrap + registration + endpoint mapping; endpoint integration tests pass with identical HTTP responses.
- **Certification:** Given a route, an engineer finds its handler file from the domain name in one hop; the interface-to-implementation catalog is grouped by domain.

### M9 - Repository Wayfinding

- **Objective:** Add the navigation aids that let the repository teach engineers how to move through it.
- **Rationale:** Structure teaches what exists; wayfinding teaches how to start and how the backend relates to the React UI and Rust shell.
- **Scope:** Add a one-page `CommandCenter.Backend/README.md` mapping folder to responsibility, indexing the major workflows, naming the state-machine homes, naming promoted hidden concepts, and linking `docs/architecture.md` + `docs/operational-context-schema.md`. Add `<summary>` comments to the load-bearing coordinator/state/parser/classifier types.
- **Non-goals:** No exhaustive doc-comment sweep. No code behavior change.
- **Validation:** README lists every top-level folder with a one-line responsibility and links the system docs; the weightiest types carry useful `<summary>` comments.
- **Certification:** A newcomer opening the backend cold can describe the subsystems, locate workflows and state homes, and explain the 3-stack relationship without grep.

### M10 - Cognitive Certification

- **Objective:** Measure final outcomes against the M0 baselines and certify the reconstruction.
- **Rationale:** The roadmap's success is defined by cognitive outcomes, not by completion of moves.
- **Scope:** Re-run the Success Metrics; record current-vs-target deltas; run a structured cold-navigation exercise; run an engineer-predictability exercise; file a short certification note alongside `COGNITIVE_AUDIT.md`.
- **Non-goals:** No new structural change except small gaps certification surfaces.
- **Validation:** All Success Metrics meet target or have documented exceptions with follow-up issues.
- **Certification:** Each final assessment question in `COGNITIVE_AUDIT.md` now has a structural answer; remaining hotspots are documented with rationale.

---

## Dependency Graph

```text
M0 (ratify)
  -> M1 (concept/authority/workflow/state inventory)
      -> M1.5 (topology discovery)
          -> M2 (state-machine ownership + characterization)
              -> M3 (target topology ratification)
                  -> M4 (hidden concept promotion)
                      -> M5 (Execution reconstruction)
                      -> M6 (Continuity reconstruction)
                          -> M7 (workflow visibility)
                              -> M8 (composition root)
                                  -> M9 (wayfinding)
                                      -> M10 (certification)
```

- **M1.5 is the key approval gate.** Folder moves do not begin until topology discovery has tested the provisional tree.
- **M2 precedes M3** because state-machine ownership must shape folder ownership.
- **M4 precedes M5/M6** because hidden concepts are architectural facts that should be visible before they are placed in final folders.
- **M5 and M6** may proceed in parallel after M4 if the approved topology does not create cross-folder move conflicts.
- **M8/M9** depend on the final structure so endpoint modules and README content reflect reality.

---

## Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Topology discovery invalidates the provisional tree | Medium | Medium | Treat this as success, not churn; M3 exists specifically to revise the tree before moves. |
| M1.5 becomes performative and merely justifies the assumed structure | Medium | High | Require traversal/search/co-change/review evidence and engineer consensus for each approved folder. |
| Folder-size goals override legibility | Medium | Medium | Treat file count as a diagnostic smell, not certification; M3 must approve large folders when they remain predictable and reject small folders when they fragment a cognitive unit. |
| State-machine centralization silently changes a transition | Medium | High | M2 characterization tests before any routing through new state-machine homes; same enums and same legal transitions. |
| Hidden-concept promotion tempts redesign | Medium | Medium | Strict move-and-name rule; no new interfaces unless an existing test seam proves one. |
| Namespace churn breaks builds during M5/M6 moves | High | Low | Use IDE move-refactor or scripted namespace updates; build + full test suite gate every PR; move one subsystem per PR. |
| Large merge-conflict window during re-foldering | Medium | Medium | Small, subsystem-scoped PRs merged quickly; freeze unrelated `Execution`/`Continuity` work during M5/M6. |
| Composition-root split drifts HTTP responses | Medium | Medium | Snapshot endpoint responses (status + envelope) before/after; centralize exception mapping with integration tests. |
| Git blame/history lost in moves | Medium | Low | Mandate `git mv`; avoid combining move + edit in one commit where possible. |
| Over-foldering fragments cognitive units | Medium | High | Anti-fragmentation principle; M3 rejection conditions; single-file pseudo-subsystems colocated unless human-interaction evidence says otherwise. |
| Reconstruction deprioritized mid-flight | Medium | Medium | M4-M6 each deliver standalone value; certification records remaining gaps if the roadmap pauses. |

**Behavioral-debt note:** the architecture audit's runtime items (A1 non-atomic writes, A7 coarse lock, A10 process probe) are out of scope here. Reconstruction makes them easier to fix later by giving each a visible home, but this roadmap does not directly change runtime behavior.

---

## Success Metrics

Baselines begin with estimates from the current audit and are corrected during M1.5. Numeric targets are derived during M1.5/M3 unless the audit already provides a defensible threshold; the roadmap does not preselect attractive numbers without evidence.

| Metric | Current | Target | Certified in |
|---|---|---|---|
| **Mean files traversed to reach intent** (for example, "where does commit happen?") | Audit estimate: ~3.5 files + heavy scrolling; M1.5 measures actual baseline | M1.5/M3-derived target; default goal is >=50% reduction for multi-file intents unless evidence shows a lower target is more meaningful | M1.5, M10 |
| **Predictability / engineer consensus** ("Where would you look first?") | Unknown; M1.5 measures baseline for state, commit, git parser, tier classifier, recovery, and promotion | M1.5/M3-derived consensus target; certification requires demonstrable convergence over baseline, not a preselected percentage | M1.5, M10 |
| **Folder legibility / fragmentation** | 58 files (`Execution`), 50 (`Continuity`), with opaque internal topology | Leaf folders are predictable landing zones; file count is a smell, not pass/fail. Large folders are accepted when legible; tiny folders are rejected when fragmenting. | M3, M5, M6, M10 |
| **Subsystem visibility** (subsystems nameable from the tree) | 5 of 7 domains legible; the 2 biggest are opaque | 100% of approved subsystems visible as folders or named files | M5, M6 |
| **Workflow discoverability** (owner identifiable without code reading) | No; workflows are implicit | Each major workflow -> one owning folder + named coordinator | M7 |
| **State-machine discoverability** (transitions locatable from structure) | No; transition rules scattered | Two named state-machine homes; transition rules centralized | M2, M4 |
| **Hidden concept visibility** | Parser/classifier/mutation concepts buried in large files | Approved hidden concepts are top-level named files | M4 |
| **Composition-root size** | 713 lines, one method, 35 inline handlers | Host bootstrap only; routes in per-domain files; exact line target derived during M8 after extraction | M8 |
| **In-backend wayfinding** | No README, 0 `<summary>` comments | README + `<summary>` on load-bearing types | M9 |

---

## Final Target State

What an engineer experiences after the roadmap is complete:

They open `CommandCenter.Backend/` and the README tells them, in one page, what each top-level folder owns, where the major workflows are coordinated, where the state machines live, which formerly hidden concepts now have names, and how the backend relates to the React UI and Rust-Tauri shell.

They open `Execution/` and read its approved subsystems from the folder list instead of scanning 58 filenames. Asking "where does git output get parsed?" they navigate to the named parser. Asking "what states can a session be in, and how does it move between them?" they open the state-machine home rather than reconstructing it from scattered guards.

They open `Continuity/` and find proposal lifecycle, document/generation ownership, understanding/compression, parsing, decisions, diagnostics, and the operational-context state rules in places that match the evidence gathered in M1.5.

They need a route handler and find it by domain name in `Composition/`, not by scrolling a 713-line method. They do not discover load-bearing concepts by accident while reading unrelated service code; the concepts the system depends on have names in the tree.

The repository now teaches itself. Crucially, it does so because the roadmap proved the cognitive topology before committing to a target structure.
