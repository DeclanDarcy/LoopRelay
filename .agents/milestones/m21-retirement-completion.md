# M21 — Retirement completion


### Deletion and reachability audit

- [ ] Add a machine-derived architecture verifier over solution projects, production entrypoints, composition registrations, catalog definitions, executor/recovery/interaction registries, prompt assets, schema/import adapters, and public result claims.
- [ ] Delete exhausted import/compatibility adapters only when portfolio exhaustion facts and adapter-disabled canonical runs pass.
- [ ] Delete provisional bridges, direct table readers, direct required mutations, feature persistence/retry/recovery, duplicate prompt catalogs/framing, dead declarations, stale settings, and unowned prompt/generated assets.
- [ ] Remove stale supported-behavior claims, including any claim that `unblock` is a public command, narrow storage commands perform full import/export/sync, retired executables remain supported, or an uncertified provider capability is available.
- [ ] Remove obsolete project references, tests that only exercise deleted authorities, publish scripts, build artifacts, and compatibility fixtures whose supported portfolio is exhausted. Preserve useful behavior tests against the canonical owners.
- [ ] Build and test the reduced solution after physical deletion; use Git history as the recovery mechanism for accepted deletions.

### Exact final metrics

The verifier must report:

| Metric | Target |
|---|---:|
| Behaviors with zero or multiple owners | 0 |
| Production application boundaries | 1 |
| Production composition roots | 1 |
| Production orchestration kernels | 1 |
| Production workflow catalogs | 1 |
| Logical authoritative mutable stores | 1 |
| Direct required effects outside Effect Coordinator | 0 |
| Workflow-specific persistence/retry/recovery paths | 0 |
| Behavior reachable only through retired code | 0 |
| Unowned runtime/generated prompt assets | 0 |
| Public operational claims without evidence identity or explicit unknown | 0 |

### Exit gate

- [ ] All metrics equal target, all former routes are absent, imported workspaces run with adapters disabled, both full chains pass from the published CLI, exact provider and platform evidence is truthful, and the owner accepts the single-authority production graph.

### Verifier inputs and non-overridable output

The architecture verifier consumes the solution/project graph, executable/publish outputs,
production entrypoints, composition registrations, catalog snapshot, all owner registries, schema/
migration/import manifests, prompt/generated asset manifests, public application claims, and
adapter-exhaustion facts. Its immutable result is keyed by commit, build, catalog, schema,
configuration, and exact-profile identities and calculates every final metric with itemized
offenders. There is no manual pass override.

Static calculation cannot prove `one plausible place to change behavior` by itself. Pair the
generated graph with the roadmap §5 owner walk-through; record each behavior family, owner,
production registration, implementation location, and zero alternate reachable locations in the
acceptance manifest.

### Deletion and platform-evidence boundaries

Delete only after parity evidence and owner acceptance for the owning convergence milestone. Keep
canonical behavior tests and exact-provider compatibility fixtures; `exhausted compatibility
fixtures` refers only to retired legacy/import formats, not evidence still required to authorize a
provider capability. An imported workspace runs with its adapter absent/disabled, not merely
unused by the happy path.

Retain M17 parity evidence, route-reachability evidence, and its legacy-body deletion commit
through this milestone's final acceptance candidate.

The current release aggregate lacks genuine Linux evidence. If release continues to claim Windows
and Linux agreement, acceptance requires genuine evidence for both and stable evidence IDs. If
Linux is no longer claimed, change the release contract and aggregate expectation explicitly;
missing evidence cannot become a pass.

Both roadmap full chains, former-route absence checks, adapter-disabled imports, unowned-asset and
duplicate-owner sentinels, reduced-solution build/tests, and the privacy scan are required on the
final deletion candidate.

The acceptance candidate may accept cross-machine claims only from the D9-selected durable,
scrubbed evidence owner. Ignored `.tmp` files remain diagnostic evidence, not durable release
provenance.
