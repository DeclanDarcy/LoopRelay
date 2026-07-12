# M12 — Import Gateway


### Portfolio and adapters

Implement ingress-only adapters for D8. Distinguish schema convergence from domain import:

- [ ] Canonical v8 and recognized partial-v9 shapes use Storage Authority migration/convergence.
- [ ] LegacyContinuity v3 maps session scopes, lineage, turns, recovery plans/attempts, and correlation into canonical facts.
- [ ] Pre-unification roadmap adapters map roadmap state, decision ledger, artifact lifecycle, split families/order, selection provenance, projection manifests, execution preparation, transition journal, and compatible history/evidence.
- [ ] Planning adapters detect incomplete plan, detail, milestone, operational-context, projection, and publication surfaces.
- [ ] Execute adapters detect decision sessions, numbered histories, handoffs, evidence, and completion archives.

### Lifecycle

- [ ] Detect source kind/version/fingerprint read-only.
- [ ] Produce a durable preview with complete domain identity mapping, conflicts, unsupported facts, unknown fields, and semantic delta. A source change invalidates the preview.
- [ ] Require explicit approval; ambiguity creates an M10 request and never guesses.
- [ ] Persist an import plan and execute all-or-nothing canonical writes/effects.
- [ ] Compare logical source and target projections. Preserve source identities when valid; map with durable correspondence when not. Leave unobserved historical fields null.
- [ ] Commit a receipt only after semantic verification, then write a monotonic canonical-only marker and mark the source non-authoritative.
- [ ] Guard runtime source selection: once canonical-only is set, any legacy reader invocation is a defect and fails tests.
- [ ] Track portfolio/adapter exhaustion. Delete an adapter only after every owned fixture imports and runs canonical-only with the adapter disabled.

### Persistence and verification

- [ ] Add detection, preview, source-fingerprint, mapping, operation/event, verification, receipt, and canonical-only facts. Reuse existing compatibility operation facts through a migration/projection adapter, not as a second store.

- [ ] For each portfolio fixture test no-write detection/preview, ambiguity, conflict, malformed input, rollback, crash/restart, semantic fidelity, receipt idempotency, no dual write, and canonical-only runtime. The acceptance fixture disables/removes the legacy reader after import and proves behavior is unchanged.

