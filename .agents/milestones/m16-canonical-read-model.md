# M16 — Canonical Read Model


### Model

Replace the narrow `CanonicalPersistenceReadModel`/`CanonicalCliStatusSnapshot` assembly path with one immutable `CanonicalWorkspaceSnapshot` composed from typed owner projections. Include:

- [x] workspace/schema/storage health, migration/import state, canonical-only marker, and compatibility uncertainty;
- [x] invocation mode, chain alternatives, selection conflicts, selected workflow/stage/transition, root/workflow/transition/attempt lineage, and terminal classification;
- [x] products, gates, warnings, read receipts, freshness, and exact evidence identities;
- [x] raw configuration identity/provenance summary, resolved policy, recommendation evaluation, effective runtime profile, exact provider profile/capabilities, and prerequisite findings;
- [x] rendered prompt/dispatch/session/turn evidence without exposing secret/provider payload content;
- [x] effect plans, states, receipts, unknowns, dependencies, and pending required pushes;
- [x] recovery classification/plan/action and allowed next operation;
- [x] outstanding interaction request identity, category, response schema, policy, deadline, and state;
- [x] completion decision, closure plan, pending operations, certificate, and terminal fact; and
- [x] certification obligations with exact credited evidence tier/version or explicit uncredited status.

- [x] Every claim carries an evidence/source identity or an explicit `Unknown` reason. Conflicting owner projections surface ambiguity; the composer does not choose a winner.

### Implementation

- [x] Define one projection interface per owner; each reads only its canonical store/contracts. The aggregate composer may join projections but may not reinterpret domain semantics.
- [x] Remove raw store/table queries from `CanonicalCliApplicationService`, `LedgerEvidenceRetrieval`, `RepositoryObserver`, and formatters.
- [x] Make status/export renderers pure functions of the snapshot and test them with no repository/database/provider dependencies.
- [x] Add stable source watermarks/snapshot identity so consumers can detect staleness without treating the snapshot as authority.
- [x] Extend certification to emit stable obligation evidence links keyed by catalog/schema/profile/asset version. A campaign-level pass never silently credits every obligation.
- [x] Implement D9 and D10's evidence/profile lifecycle in the release subprojection.

### Verification and exit gate

- [x] Query a fixture containing a pending push, unknown effect, recovery plan, interaction, migration-required storage, import conflict, and partial completion closure. One snapshot must expose all identities/actions. Render twice and prove byte/storage non-mutation. Trace every displayed claim to a fact. Read-model rebuild after restart must be semantically identical and must not repair state.

### Consistent snapshot and claim shape

Compose owner projections under one SQLite read transaction where they share the canonical store.
For independent filesystem/Git/provider evidence, capture before/after watermarks; retry a bounded
number of times if they change, then return explicit staleness/conflict rather than combine
incompatible moments. Compute snapshot identity from workspace, schema/catalog identity, ordered
owner watermarks, and external observation identities.

Represent every operational claim as value/status plus owner, source fact/evidence IDs, source
watermark, observed version, and one of `Known`, `Unknown(reason)`, `Conflict(source set)`, or
`Stale(reason)`. The aggregate composer may join and expose conflicts but cannot choose an owner,
fill a default, trigger migration, or repair state.

### Determinism, evidence credit, and profile retirement

All collections exposed to renderers have canonical ordering. Given one snapshot, text/JSON/export
rendering is byte-deterministic and has no repository, database, provider, clock, or environment
dependency.

Evidence credit requires an exact obligation key/content version plus
catalog/schema/asset/profile scope and evidence tier. Evidence for an older content version is
stale/uncredited, not inherited. Exact provider profiles may be retired only after no active root,
attempt, session, recovery plan, or evidence claim references them and D10 replacement evidence
exists.

D9 and D10 remain owner rulings. Until D9 is accepted, release claims sourced only from `.tmp` are
visibly `LocalOnly` and cannot support cross-machine provenance.
