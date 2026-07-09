# Runtime Persistence Consolidation Plan

## Epic Objective

Migrate LoopRelay's remaining ad-hoc runtime persistence under `.LoopRelay` onto the existing SQLite persistence architecture, making SQLite the canonical substrate for structured runtime state, runtime history, validation, and recovery while preserving externally observable behavior.

Filesystem storage remains only where behavior is fundamentally filesystem-defined: the `.LoopRelay` directory, `.LoopRelay/.gitignore`, the SQLite database file, SQLite engine sidecars, and any intentionally retained compatibility export for telemetry tooling.

## Architectural Theme

The central architectural objective is to make `.LoopRelay` a SQLite-backed runtime persistence boundary instead of a mixed filesystem persistence area.

The current architecture already treats SQLite as canonical for workflow state, history, evidence, synchronization metadata, validation, and recovery markers. Decision-session resumability and telemetry are architectural outliers because they persist structured runtime data through direct file writes and JSONL append logic. This epic brings those outliers under the same persistence authority, validation posture, recovery model, and composition pattern as the rest of runtime persistence.

The theme is not "move two files into a database." The theme is "make runtime persistence have one canonical owner."

## Existing Infrastructure Reuse

The implementation should reuse the existing SQLite persistence architecture wherever practical:

- Database location and workspace bootstrapping under `.LoopRelay/persistence/looprelay.sqlite3`.
- Existing schema versioning and database integrity validation patterns.
- Existing connection lifetime and SQLite locking assumptions.
- Existing runtime composition patterns for selecting SQLite-backed persistence.
- Existing fail-open behavior for non-critical runtime persistence.
- Existing serialization conventions for structured runtime documents.
- Existing workflow recovery and verification concepts for detecting partial or corrupt runtime state.
- Existing storage command behavior for validation and operational inspection.
- Existing dependency injection boundaries rather than a parallel persistence stack.
- Existing telemetry recorder separation between event capture and event persistence.
- Existing decision-session resume abstraction rather than coupling callers to storage details.

The plan intentionally avoids new persistence stacks. Any new runtime persistence capability should appear as an extension of the current SQLite substrate.

## Architectural Simplification Opportunities

The migration creates a natural point to simplify runtime persistence ownership:

- Move decision-session resume from a direct JSON file to the database as canonical latest-state runtime data.
- Move telemetry from JSONL append as canonical storage to database-backed event persistence.
- Keep JSONL telemetry only as an intentional compatibility/export surface if required by external tooling.
- Reduce direct `.LoopRelay` path knowledge in runtime consumers.
- Centralize database readiness, validation, and startup behavior for runtime persistence.
- Consolidate duplicate serialization and corruption handling into the SQLite validation/recovery model.
- Preserve `.LoopRelay/.gitignore` as the only application-authored non-database coordination file under the root.
- Make database-backed runtime persistence observable through existing validation and operational commands.

The epic should not refactor unrelated workflow artifacts, prompt artifacts, archive layout, or non-runtime persistence.

## Compatibility Surface

The following externally observable behavior must remain unchanged unless a compatibility decision is explicit and justified:

- `.LoopRelay` remains the local runtime root.
- Source-defined path casing remains `.LoopRelay`.
- `.LoopRelay/.gitignore` is created with `*` when absent.
- An existing `.LoopRelay/.gitignore` is not overwritten.
- Runtime state under `.LoopRelay` remains ignored by git.
- `.LoopRelay/persistence/looprelay.sqlite3` remains the database path.
- SQLite validation keeps distinguishable missing, valid, incompatible, unsupported, and corrupt states.
- Existing SQLite-backed runtime domains remain readable.
- `LoopRelay_DECISION_RESUME=0` and `LoopRelay_DECISION_RESUME=false` disable resume attempts.
- Decision resume failures remain fail-open.
- Invalid legacy decision resume state is ignored and cleaned up with the same user-facing warning posture.
- Decision resume clear semantics are preserved on failed turns, transfer recycle, stale projections, and failed resume.
- `LoopRelay_SESSION_LOG=0` and `LoopRelay_SESSION_LOG=false` disable telemetry.
- Telemetry remains enabled by default.
- Telemetry failures remain fail-open, with cancellation still treated as caller intent.
- Telemetry record contents preserve the existing observed fields.
- Telemetry ordering remains deterministic.
- Existing JSONL telemetry filename and line format remain available if compatibility export is retained.
- CLI storage and verification workflows continue to provide operational visibility without requiring users to inspect internals.

## Migration Risks

The milestone order is shaped by these risks:

- Fresh workspaces currently tolerate a missing database; SQLite-canonical runtime state needs a clear bootstrap path.
- Decision resume is intentionally fail-open; database failures must not turn resumability into a hard dependency.
- Telemetry is a hot path; database writes must preserve low operational risk and fail-open behavior.
- JSONL telemetry may have external visualizer or pruning consumers.
- Mixed canonical stores can produce split-brain state if legacy files and SQLite rows are both treated as authoritative.
- Startup validation changes can affect runtime store selection.
- Database schema evolution can affect all existing SQLite-backed domains.
- Recovery behavior can regress if invalid legacy files, missing databases, or corrupt databases are handled differently.
- Rollback is harder once runtime writes are canonical in SQLite unless compatibility export/import behavior is clear.

## Milestones

### Milestone 1: Establish Runtime SQLite Readiness

**Objective**

Make the existing SQLite persistence substrate ready to own the remaining `.LoopRelay` runtime state without changing the decision resume or telemetry consumers yet.

**Architectural Rationale**

SQLite must be a reliable runtime substrate before hot consumers are moved onto it. This milestone creates the architectural footing for canonical runtime persistence: database readiness, validation, composition, and compatibility decisions are handled first.

**Implementation Scope**

- Extend the existing SQLite persistence architecture to recognize the remaining runtime persistence domains at roadmap granularity.
- Define startup behavior for fresh repositories where the database is missing.
- Preserve `.LoopRelay` root protection and `.gitignore` behavior.
- Ensure runtime SQLite availability does not break existing validation or store selection.
- Decide whether JSONL telemetry remains a compatibility export and record that decision in operational documentation.

**Major Implementation Areas**

- SQLite database readiness and initialization path.
- Runtime persistence composition.
- Storage validation integration.
- Operational documentation for `.LoopRelay` runtime persistence.
- Tests for startup behavior and validation classification.

**Dependencies**

- Current audit observations in `audit.md`.
- Existing SQLite database path and validation behavior.
- Existing runtime composition boundaries.

**Architectural Invariants**

- SQLite is the canonical substrate for structured runtime state.
- The database path remains stable.
- Filesystem coordination artifacts remain filesystem-owned.
- Existing SQLite domains are not regressed.
- Missing runtime state remains recoverable.

**Compatibility Requirements**

- `.LoopRelay/.gitignore` creation remains unchanged.
- Missing database behavior remains observable and recoverable.
- Existing storage validation statuses remain distinguishable.
- Existing runtime domains in SQLite remain readable.

**Acceptance Criteria**

- A fresh workspace can enter runtime execution without requiring direct user manipulation of `.LoopRelay`.
- Existing SQLite-backed runtime domains still pass validation and use the same database location.
- Storage validation reports runtime persistence readiness without mutating unrelated state.
- `.LoopRelay/.gitignore` behavior remains unchanged.
- No decision resume or telemetry caller has changed canonical storage yet.

**Risks**

- Startup behavior may accidentally make SQLite mandatory for flows that previously tolerated absence.
- Validation may become noisy if runtime domains are treated as required before migration.
- Database readiness changes may interact with existing store-selection fallbacks.

**Deferred Work**

- Moving decision resume data.
- Moving telemetry records.
- Retiring legacy filesystem persistence.

### Milestone 2: Make Decision Resume SQLite-Canonical

**Objective**

Move decision-session resumability from `decision-session.json` to SQLite as canonical latest-state runtime persistence.

**Architectural Rationale**

Decision resume is structured singleton runtime state with clear overwrite, clear, invalidation, and fail-open behavior. It fits the existing SQLite persistence model and should no longer be an ad-hoc JSON file beside the database.

**Implementation Scope**

- Persist decision resume state canonically in SQLite.
- Preserve the existing decision resume abstraction and caller behavior.
- Preserve all disable, clear, invalidation, stale-state, and fail-open semantics.
- Provide one-time legacy compatibility for existing `decision-session.json` state.
- Remove filesystem resume as an ongoing canonical store after migration behavior is covered.

**Major Implementation Areas**

- SQLite-backed decision resume persistence.
- Startup/resume compatibility with existing legacy file state.
- Failure and corruption handling.
- Runtime composition update.
- Decision resume tests across fresh, valid, invalid, disabled, and clear scenarios.

**Dependencies**

- Milestone 1 runtime SQLite readiness.
- Existing decision resume model and environment variable behavior.

**Architectural Invariants**

- Resume is an optimization, not a correctness dependency.
- Resume failures remain fail-open.
- Disabling resume affects resume attempts, not unrelated persistence behavior.
- Latest-state semantics are preserved.
- Legacy filesystem state is not allowed to compete with SQLite as a long-term authority.

**Compatibility Requirements**

- `LoopRelay_DECISION_RESUME=0` disables resume attempts.
- `LoopRelay_DECISION_RESUME=false` disables resume attempts.
- Missing resume state returns no resume.
- Invalid resume state is ignored and cleaned up.
- Clear behavior remains tied to the same runtime events as today.
- User-facing warnings remain consistent in severity and intent.

**Acceptance Criteria**

- A successful proposal persists resumability into SQLite.
- A subsequent process can resume from SQLite state.
- Disabled resume mode does not attempt resume.
- Invalid legacy filesystem resume state is handled without failing the turn.
- Clear events remove the canonical resume state.
- No normal runtime path writes `decision-session.json` as canonical state.

**Risks**

- Resume may become too tightly coupled to database availability.
- Legacy file import can create split-brain behavior if not made one-time and subordinate.
- Warning behavior can drift and make fail-open recovery harder to diagnose.

**Deferred Work**

- Telemetry migration.
- Broader runtime verification beyond decision resume scenarios.

### Milestone 3: Make Telemetry SQLite-Canonical

**Objective**

Move telemetry record persistence from JSONL append files to SQLite as the canonical telemetry event store.

**Architectural Rationale**

Telemetry records are structured runtime events with ordering, filtering, retention, and diagnostics value. SQLite provides stronger query, integrity, and recovery properties than JSONL append while allowing JSONL to remain an intentional compatibility export if needed.

**Implementation Scope**

- Persist telemetry records canonically in SQLite.
- Preserve telemetry enable/disable behavior.
- Preserve fail-open behavior and cancellation semantics.
- Preserve record content and deterministic ordering.
- Retain or replace JSONL output only as an explicit compatibility/export surface.
- Ensure telemetry persistence remains safe on hot paths.

**Major Implementation Areas**

- SQLite-backed telemetry event persistence.
- Runtime telemetry composition.
- Optional JSONL compatibility export behavior.
- Telemetry validation and diagnostics.
- Performance and failure-mode tests for telemetry recording.

**Dependencies**

- Milestone 1 runtime SQLite readiness.
- Existing telemetry recorder and record model.
- Compatibility decision for JSONL consumers.

**Architectural Invariants**

- Telemetry must not affect turn success.
- Telemetry remains enabled by default.
- Disabled telemetry produces no telemetry writes.
- Event ordering remains deterministic.
- SQLite is canonical when telemetry is enabled.
- JSONL, if retained, is not a competing canonical store.

**Compatibility Requirements**

- `LoopRelay_SESSION_LOG=0` disables telemetry.
- `LoopRelay_SESSION_LOG=false` disables telemetry.
- Existing telemetry fields remain represented.
- Telemetry persistence failures warn and do not fail the turn.
- Caller cancellation still propagates.
- JSONL filename and line format remain available if retained for compatibility.

**Acceptance Criteria**

- A recorded turn produces one canonical SQLite telemetry event.
- Telemetry ordering can be reconstructed deterministically from SQLite.
- Disabled telemetry produces no canonical telemetry event.
- Simulated database telemetry failures preserve fail-open behavior.
- JSONL compatibility behavior, if retained, is explicitly non-canonical and covered by tests.
- Runtime code no longer depends on JSONL files for canonical telemetry persistence.

**Risks**

- Telemetry writes can add latency to hot runtime paths.
- Database lock contention can turn diagnostics into operational risk.
- External JSONL tooling can regress if export compatibility is incomplete.
- Retaining JSONL incorrectly can preserve duplicate persistence ownership.

**Deferred Work**

- Long-term telemetry retention and pruning policy beyond preserving existing behavior.
- New analytics surfaces beyond existing telemetry fields.

### Milestone 4: Integrate Runtime Persistence Verification and Recovery

**Objective**

Make the migrated runtime persistence domains visible through the existing SQLite validation and recovery posture.

**Architectural Rationale**

Moving data into SQLite is not enough. Runtime persistence becomes architecturally simpler only when validation, recovery classification, and operational inspection use the same database-centered model as the rest of the system.

**Implementation Scope**

- Extend verification to cover decision resume and telemetry persistence at an appropriate runtime-domain level.
- Ensure validation distinguishes missing, disabled, empty, valid, incompatible, and corrupt runtime persistence states where those distinctions are observable.
- Preserve mutation-free verification behavior.
- Surface runtime persistence findings through existing operational command patterns.
- Cover compatibility export health if JSONL telemetry export remains.

**Major Implementation Areas**

- Storage verification.
- Recovery classification.
- Operational reporting.
- Runtime persistence tests.
- Documentation of recovery behavior.

**Dependencies**

- Milestone 2 decision resume migration.
- Milestone 3 telemetry migration.
- Existing storage verification and database validation behavior.

**Architectural Invariants**

- Verification must not mutate runtime state.
- Validation output must remain actionable and deterministic.
- Runtime persistence findings must not obscure existing SQLite domain findings.
- Fail-open runtime features remain fail-open even when verification can report problems.

**Compatibility Requirements**

- Existing storage validation categories remain usable.
- Existing database path and version checks remain intact.
- Existing workflow transaction recovery classification remains unchanged.
- Operational command behavior remains stable except for additional runtime persistence visibility.

**Acceptance Criteria**

- Verification reports migrated decision resume state health without requiring a filesystem file.
- Verification reports telemetry persistence health at the database level.
- Corrupt or incompatible runtime persistence is distinguishable from missing or disabled runtime persistence.
- Verification does not create, delete, or rewrite runtime state.
- Existing verification tests for current SQLite domains continue to pass.

**Risks**

- Over-validating optional runtime data can create false failures.
- Verification may accidentally mutate state during readiness checks.
- Additional findings can make operational output harder to interpret.

**Deferred Work**

- Advanced telemetry querying or reporting beyond health/validation.
- New repair commands unless already required by existing recovery behavior.

### Milestone 5: Retire Ad-Hoc Runtime Filesystem Persistence

**Objective**

Remove the remaining ad-hoc filesystem persistence paths for structured runtime data and leave `.LoopRelay` with a clean ownership model.

**Architectural Rationale**

The migration only reduces complexity after the legacy filesystem writers and duplicate canonical paths are retired. This milestone makes the end state explicit: SQLite owns structured runtime persistence; filesystem artifacts exist only for coordination, engine storage, and intentional compatibility exports.

**Implementation Scope**

- Remove ongoing canonical writes to `decision-session.json`.
- Remove JSONL telemetry as canonical storage.
- Keep `.LoopRelay/.gitignore` behavior.
- Keep the SQLite database file and sidecars filesystem-backed.
- Keep JSONL telemetry export only if explicitly retained as compatibility.
- Clean up obsolete persistence composition and tests.
- Update documentation to describe the final runtime persistence ownership model.

**Major Implementation Areas**

- Legacy persistence removal.
- Runtime composition cleanup.
- Documentation cleanup.
- Compatibility tests.
- Final validation pass.

**Dependencies**

- Milestone 2 decision resume migration.
- Milestone 3 telemetry migration.
- Milestone 4 verification coverage.

**Architectural Invariants**

- No structured runtime state has two canonical stores.
- Filesystem persistence under `.LoopRelay` is limited to filesystem-defined artifacts and explicit exports.
- SQLite remains the source of truth for migrated runtime data.
- Existing observable behavior remains preserved.

**Compatibility Requirements**

- `.LoopRelay/.gitignore` remains create-only and non-overwriting.
- Existing database path remains unchanged.
- Environment variables keep their behavior.
- Fail-open behavior remains intact.
- JSONL compatibility, if retained, is documented as export behavior.

**Acceptance Criteria**

- Structured decision resume state is canonical only in SQLite.
- Structured telemetry events are canonical only in SQLite.
- No runtime path treats `decision-session.json` as the primary store.
- No runtime path treats telemetry JSONL as the primary store.
- Validation covers the final ownership model.
- Tests demonstrate fresh workspace, migrated workspace, disabled-feature, failure, and compatibility-export behavior.

**Risks**

- Removing legacy code too early can hide compatibility regressions.
- Compatibility exports can be mistaken for canonical state if ownership is not explicit.
- Documentation drift can confuse operators inspecting `.LoopRelay`.

**Deferred Work**

- Non-runtime persistence changes.
- New telemetry dashboards or analytics features.
- Retention policy changes beyond compatibility-preserving behavior.
- Any broader refactor unrelated to `.LoopRelay` runtime persistence.

## Final Target State

At the end of this epic:

- SQLite is the canonical runtime persistence substrate for structured `.LoopRelay` state.
- Decision-session resumability is stored and recovered through SQLite.
- Telemetry events are stored and queried through SQLite.
- Existing SQLite-backed runtime domains continue using the same database.
- `.LoopRelay/.gitignore` remains filesystem-owned because it coordinates git behavior.
- The SQLite database file and sidecars remain filesystem-owned because they are database engine files.
- JSONL telemetry exists only if retained as an explicit compatibility export.
- Runtime validation and recovery operate through the existing SQLite-centered architecture.
- There is no duplicate canonical persistence path for migrated runtime data.
