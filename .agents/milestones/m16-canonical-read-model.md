# M16 — Canonical Read Model


### Model

Replace the narrow `CanonicalPersistenceReadModel`/`CanonicalCliStatusSnapshot` assembly path with one immutable `CanonicalWorkspaceSnapshot` composed from typed owner projections. Include:

- [ ] workspace/schema/storage health, migration/import state, canonical-only marker, and compatibility uncertainty;
- [ ] invocation mode, chain alternatives, selection conflicts, selected workflow/stage/transition, root/workflow/transition/attempt lineage, and terminal classification;
- [ ] products, gates, warnings, read receipts, freshness, and exact evidence identities;
- [ ] raw configuration identity/provenance summary, resolved policy, recommendation evaluation, effective runtime profile, exact provider profile/capabilities, and prerequisite findings;
- [ ] rendered prompt/dispatch/session/turn evidence without exposing secret/provider payload content;
- [ ] effect plans, states, receipts, unknowns, dependencies, and pending required pushes;
- [ ] recovery classification/plan/action and allowed next operation;
- [ ] outstanding interaction request identity, category, response schema, policy, deadline, and state;
- [ ] completion decision, closure plan, pending operations, certificate, and terminal fact; and
- [ ] certification obligations with exact credited evidence tier/version or explicit uncredited status.

- [ ] Every claim carries an evidence/source identity or an explicit `Unknown` reason. Conflicting owner projections surface ambiguity; the composer does not choose a winner.

### Implementation

- [ ] Define one projection interface per owner; each reads only its canonical store/contracts. The aggregate composer may join projections but may not reinterpret domain semantics.
- [ ] Remove raw store/table queries from `CanonicalCliApplicationService`, `LedgerEvidenceRetrieval`, `RepositoryObserver`, and formatters.
- [ ] Make status/export renderers pure functions of the snapshot and test them with no repository/database/provider dependencies.
- [ ] Add stable source watermarks/snapshot identity so consumers can detect staleness without treating the snapshot as authority.
- [ ] Extend certification to emit stable obligation evidence links keyed by catalog/schema/profile/asset version. A campaign-level pass never silently credits every obligation.
- [ ] Implement D9 and D10's evidence/profile lifecycle in the release subprojection.

### Verification and exit gate

- [ ] Query a fixture containing a pending push, unknown effect, recovery plan, interaction, migration-required storage, import conflict, and partial completion closure. One snapshot must expose all identities/actions. Render twice and prove byte/storage non-mutation. Trace every displayed claim to a fact. Read-model rebuild after restart must be semantically identical and must not repair state.

