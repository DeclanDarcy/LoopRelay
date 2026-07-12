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

