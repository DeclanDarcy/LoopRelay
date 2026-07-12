# Convergence Program Handoff — Pick Up From M8

Context for the agent executing the **Canonical Architecture Convergence Program** in this
repository from **M8 (Effect Coordinator)** onward. The authoritative program document is
`roadmap.md` (v2): §7 holds the per-milestone specs and every encoded decision to date, §10
holds the owner-ruling docket (ratified + open), §2.3 the authority ownership map. The three
root audits (`canonical-architecture-convergence-audit.md`, `orphaned-code-audit.md`,
`architectural-retirement-strategy-audit.md`) are owner-trusted inputs — do not re-litigate
them, the scorecard, or anything in §10's ratified table.

## Status

M0–M7 complete, owner-accepted, committed and pushed on branch `architecture-convergence`:

| Milestone | Commit | One-line |
|---|---|---|
| M0 constitution | `9f6418f5` | ownership map §2.3, debris deletion |
| M1 workspace state | `8c0b11a4` | prefixed-ULID causal spine |
| M2 evaluation | `87c97444` | warnings over obstacles, specific labels, derived-never-latched |
| M3 product | `ab10e06b` | read receipts, input surfaces, filesystem-authoritative collab files |
| M4 history | `b1b9aa8a` | ledger-backed loop history, causal lineage |
| M5 policy | `96d41f44` | one resolved versioned policy per attempt (`pol_v1_`) |
| M6 prompt | `45053775` | template-owned policy, rendered-prompt facts, send-site minting |
| M7 runtime | `10dd9494` | gateway, capability negotiation, D3 wrappers reconnected |

Test baseline after M7: **1,613 passed / 0 failed / 5 skipped** across all ten test projects;
the solution builds with **zero warnings**. Solution file is `LoopRelay.slnx`.

## Working protocol (owner-confirmed cadence)

1. Discover → implement the milestone → **adversarial review** (independent reviewer pass over
   the uncommitted diff; classify BLOCKER / SHOULD-FIX / NOTE) → fix everything confirmed →
   re-verify → encode all decisions in roadmap §7 (and §10 if a ruling landed) → **present to
   the owner for acceptance. Never commit until the owner accepts.**
2. Owner rulings arrive as prose at gating-milestone spec time per the §10 docket — no
   dedicated decision sessions. **Next open ruling: M9 cancellation-salvage semantics** (due at
   M9 spec time). M8 and M10-forward encoded decisions are already in §7.
3. Ask the owner questions in prose only — never multiple-choice.
4. Decisive test verification runs in the **foreground** (`dotnet test LoopRelay.slnx`);
   background test runs die silently on this machine roughly a third of the time.
5. No CI exists; the suite run is the verification. Concurrency is deferred program-wide
   (single-invocation assumptions are acceptable and documented where made).
6. Within-doctrine judgment calls are encouraged — make them, encode them in §7 at
   implementation, and surface anything owner-visible in the acceptance report.

## Standing owner doctrine (violations are blockers)

- **Vocabulary ban (M2):** no status/state/outcome/stop-reason may say generic "Blocked".
  Every cannot-proceed condition carries a specific label naming the actual failure
  (`MissingRequiredInput`, `StorageUnusable`, `DirtyInputSurface`, `UnversionedInputSurface`,
  `MissingRuntimePrerequisite`, …). Blocked-ness is **derived, never latched**; there is no
  unblock verb — satisfying the gate is the only unblock.
- **Warnings over obstacles (M2):** wherever the system can proceed or naturally retry, emit a
  typed warning (`evaluation_warnings`), not a must-resolve state.
- **Facts are append-only.** Migrations are additive (guarded `ColumnExists` ALTERs) and never
  rewrite fact rows (tested per column). Reads open the database read-only, never migrate, and
  order by **rowid** (ledger insertion is the ordering authority; ULIDs are not monotonic
  within a millisecond). Pre-vN databases read back with null fallbacks, never crash.
- **Policy (M5):** a configured value is either demonstrably effective or explicitly rejected.
  Unknown keys reject at every policy-owned settings level. Schema is `policy-v3`; identity is
  `pol_v1_` + sha256[..32] of canonical JSON (SchemaVersion inside; all collections sorted).
  Recognized env vars are resolver-validated ambient invocation inputs; explicit `--policy`
  flags beat them.
- **Prompts (M6):** every production prompt is template-owned; the generator's build-time
  `SourceHash` is the policy-complete prompt version. No post-render appends, no C#-injected
  instructions through payload holes (payloads are pure data). `*.prompt` is LF-pinned in
  `.gitattributes` — keep prompt files pure LF. A 50-asset ownership registry test pins every
  prompt to an owner. Unused prompts are retained deliberately (owner has plans): extensionless
  `Planning/CreateNewRoadmap` (owner-reserved, M17) and three empty Eval stubs
  (UpdateDependencyInventory/UpdateHypothesisInventory/UpdateRoadmap — content owner-pending).
- **Send evidence (M6/M7):** rendered-prompt facts are minted only where text actually goes to
  an agent. Since M7 this is the gateway deposit mechanism: send sites deposit the semantic
  capture; the recording gateway appends the one fact at the transport moment with session/turn
  identity and the exact wire text. A deposit never sent mints nothing. Facts are
  attempted-send evidence.
- **Collaboration files** are filesystem-authoritative, consumed read-at-use with receipts, and
  guarded by the uniform clean-input gate (offer-to-commit is M10's first interaction type).
- **Identity:** prefixed ULIDs via `CausalUlid.NewId(prefix)` — ws, run, wfi, tr, att, ses,
  turn, rcpt, warn, hist, bnd, res, rp, pre. SQLite workspace DB is at
  `.LoopRelay/persistence/looprelay.sqlite3`, currently **schema v9**
  (`LoopRelayWorkspaceDatabase.CurrentSchemaVersion`).

## Architecture as of M7 (what M8 builds on)

- **Nucleus (§6.2 bridge):** `UnifiedCliComposition` (LoopRelay.Cli) composes
  resolver → `TransitionRuntime` → `WorkflowController` → `WorkflowChainRunner`. This canonical
  nucleus is the registered production routing target through M12, retired by M14. The legacy
  bodies (`LoopRunner`/`ExecutionStep`, Plan.Cli `PlanSession`, Roadmap.Cli runners) are
  compiled and tested but not constructed by the unified host; they retire M17–M19
  (`LoopRunner`/`ExecutionStep` removal is parity-gated, M14 era).
- **Effects today (M8's subject):** `UnifiedEffectExecutor` (composition) executes effects with
  `EffectExecutionContext` lineage (tr_/att_/run_/wfi_, threaded since M4). Rotation effects
  write loop history through `LedgerLoopHistoryStore` (ledger row + derived numbered-file
  projection; projection failure after ledger commit does not fail rotation). `CommitGate`
  owns no-changes commit counting (cap from policy). `TransitionRuntime` appends
  `TransitionOutputInterpreted`/`TransitionOutputValidated` events. M8's encoded decisions
  (§7): **kernel-inserted commit effects are blocking at the step boundary; push effects are
  required-but-asynchronous** — journaled, retried, reconciled, visible while pending; closure
  cannot complete while required effects are outstanding. Verification brief: success/failure/
  unknown behave distinctly; restart between ordered effects; duplicate invocation yields one
  semantic mutation; journal receipts match actual repository mutations exactly.
- **Runtime gateway (M7):** all canonical sends flow through the recording gateway inside
  `UnifiedPromptExecutor` (`RecordingAgentRuntime` wrapping
  `OperationalRuntimeComposition.Compose(...)`, production compositions only). Every send
  produces a rendered-prompt fact (session/turn-linked, wire-exact text) and a turn row with
  terminal state, usage, transport sha, and typed diagnosis; thrown turns record too; session
  specs are recorded before launch. Capability negotiation (`AgentCapabilityNegotiation`)
  precedes every launch; capability gaps throw `AgentCapabilityException`
  ("MissingRuntimeCapability: …"). Codex is the sole provider (D5);
  `AgentSpecs` is the code-owned role→effort/sandbox catalog.
- **Prerequisites:** `RuntimePrerequisiteDoctor` runs at the Run verb (production only);
  errors abort with `MissingRuntimePrerequisite` (exit 4) before any transition and every
  inspection is a `canonical_runtime_prerequisites` fact. Test seams: composition's
  `ProductionRuntime` and `RuntimePrerequisiteDoctor` are internally settable.

## Deferred registry (open items by owning milestone)

- **M9:** cancellation-salvage semantics (**open §10 owner ruling — request it in prose at M9
  spec time**); recovery-marker lineage; warm plan/execution session restart-resume
  orchestration (thread ids already recorded as evidence); session read/fork capability bits
  (added with their request shapes).
- **M10 (D4, open at spec):** timeout/default policy per request category; isolation guarantee
  depth; whether trust evidence is an audit product.
- **M8/M11/M15:** archive/export rerouting. **M11:** `StorageVerificationResult`
  warnings-as-authority shape.
- **M14/M16:** dead `WorkflowTransitionDefinition.Validators`.
- **M15:** completion-domain Blocked vocabulary; B4 disposition reuse-key flag.
- **M16:** production readers for the policy-resolution, rendered-prompt, and
  runtime-prerequisite ledgers; `LedgerEvidenceRetrieval` consumer.
- **M17–M19:** legacy Roadmap.Cli config readers; legacy loop prompt-policy fallbacks; per-repo
  policy file; `TransitionRuntime` null-policy hosts; HITL non-implementation request capture
  wiring (`ExplicitHitlNonImplementationRequestCaptureService` is unwired in the nucleus — the
  prompt text's "requires a structured explicit HITL request captured" is evidence-gated and
  becomes fully live when Plan/Roadmap bodies wire capture); M17 full-roadmap generation intent
  (`CreateNewRoadmap`); M18 plan restart semantics under Codex capabilities.

## House style and mechanics

- File-scoped namespaces; explicit types (no `var` for non-obvious); sealed positional records;
  raw string literals for SQL; snake_case SQL; primary-constructor classes with `_name`
  parameters; trailing optional record parameters for additive fields; store JSON helpers
  `Json<T>`/`ReadJson<T>`; `ExecuteAsync` null-coalesces parameters to DBNull.
- Terminal evidence writes use `CancellationToken.None` (the caller's token has typically
  fired); best-effort evidence is `Try*`-swallowed — prompt execution must never fail because
  evidence recording failed.
- Tests: xunit v2, plain `Assert`. Implicit xunit usings everywhere **except**
  `LoopRelay.Cli.Tests` and `LoopRelay.Completion.Tests`, which need explicit `using Xunit;`.
  Schema-version bumps rename `LoopRelayWorkspaceDatabaseSchemaV{N}Tests.cs` via `git mv` and
  add per-column no-rewrite migration tests plus an idempotent second pass.
- Prompt asset generator: `{name}` single-brace = declared hole (= `Render` parameter, hole
  order is parameter order); `{{…}}` = escaped literal; PROMPT001–004 are build errors.
- Milestone commits: one commit per accepted milestone, message style per `git log`.
