# M12 — Import Gateway


### Portfolio and adapters

Implement ingress-only adapters for D8. Distinguish schema convergence from domain import:

- [x] Canonical v8 and recognized partial-v9 shapes use Storage Authority migration/convergence.
- [x] LegacyContinuity v3 maps session scopes, lineage, turns, recovery plans/attempts, and correlation into canonical facts.
- [x] Pre-unification roadmap adapters map roadmap state, decision ledger, artifact lifecycle, split families/order, selection provenance, projection manifests, execution preparation, transition journal, and compatible history/evidence.
- [x] Planning adapters detect incomplete plan, detail, milestone, operational-context, projection, and publication surfaces.
- [x] Execute adapters detect decision sessions, numbered histories, handoffs, evidence, and completion archives.

### Lifecycle

- [x] Detect source kind/version/fingerprint read-only.
- [x] Produce a durable preview with complete domain identity mapping, conflicts, unsupported facts, unknown fields, and semantic delta. A source change invalidates the preview.
- [x] Require explicit approval; ambiguity creates an M10 request and never guesses.
- [x] Persist an import plan and execute all-or-nothing canonical writes/effects.
- [x] Compare logical source and target projections. Preserve source identities when valid; map with durable correspondence when not. Leave unobserved historical fields null.
- [x] Commit a receipt only after semantic verification, then write a monotonic canonical-only marker and mark the source non-authoritative.
- [x] Guard runtime source selection: once canonical-only is set, any legacy reader invocation is a defect and fails tests.
- [x] Track portfolio/adapter exhaustion. Delete an adapter only after every owned fixture imports and runs canonical-only with the adapter disabled.

### Persistence and verification

- [x] Add detection, preview, source-fingerprint, mapping, operation/event, verification, receipt, and canonical-only facts. Reuse existing compatibility operation facts through a migration/projection adapter, not as a second store.

- [x] For each portfolio fixture test no-write detection/preview, ambiguity, conflict, malformed input, rollback, crash/restart, semantic fidelity, receipt idempotency, no dual write, and canonical-only runtime. The acceptance fixture disables/removes the legacy reader after import and proves behavior is unchanged.

### Accepted portfolio registry

M12 consumes M11's canonical export codec and decoder where canonical packages are involved; it
does not redefine their schema, fingerprint, or storage semantics.

For every owner-approved format, register source kind/family/version, unambiguous detector,
read-only reader, source fingerprint algorithm, mapped domains/identity rules, unsupported fields,
conflict rules, semantic comparator, fixture identities, and retirement/exhaustion criteria.
Unknown, mixed, or overlapping detector matches fail closed. D8's list becomes executable only
after owner acceptance and a fixture for each actual owned workspace family.

### Import ordering and partial external work

`All-or-nothing canonical writes/effects` does not imply an atomic transaction across SQLite and
external targets. Use this ordering:

1. detect and preview without writes; bind preview to a complete source fingerprint;
2. validate explicit approval or a resolved M10 interaction and persist an immutable plan;
3. re-hash the source and reject stale preview before any canonical write;
4. map and stage canonical facts in one database transaction, preserving valid identities and
   recording correspondence for remapped IDs;
5. validate the staged target projection and commit canonical facts plus verification evidence;
6. execute outward/projection work as ordered M8 effects and recover it through M9;
7. append the import receipt only after semantic verification and required effects settle, then
   append the monotonic canonical-only/source-non-authoritative facts; and
8. permit normal runtime only from the canonical source.

Failure before the canonical transaction commits rolls back. Failure afterward leaves one pending
import operation and resumes/reconciles it; it does not delete verified facts or repeat settled
effects. A repeated identical import returns the same receipt. Preserve a source identity collision
only when type, scope, and semantics agree; otherwise mint a canonical ID and store immutable
source-to-target correspondence.

An adapter exhaustion fact is keyed by adapter/version and accepted portfolio snapshot. It links
every owned fixture to a successful receipt, canonical-only run, and adapter-disabled result. A
newly discovered owned format invalidates/supersedes the exhaustion fact; it does not silently
reactivate a deleted fallback.
