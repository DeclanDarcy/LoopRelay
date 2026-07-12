# M20 — Application Boundary convergence


### Project and contracts

- [ ] Create `src/LoopRelay.Application/` as a reusable class library for public commands, queries, results, the thin coordinator, and application-level outcome/exit semantics. It may reference owner contracts but must not reference CLI parsing/rendering, `Microsoft.Data.Sqlite`, provider transports, or filesystem/Git implementations.

The command/query matrix must cover:

- [ ] run default/forced/bounded workflow;
- [ ] canonical status;
- [ ] storage verify/init/migrate/export/sync;
- [ ] import detect/preview/execute/verify;
- [ ] recovery inspect/plan/execute;
- [ ] interaction list/show/respond/cancel;
- [ ] completion status/reconcile; and
- [ ] capability/prerequisite diagnostics.

- [ ] Every result carries correlation and causal IDs, exact outcome/reason, evidence links, warnings, pending effects, recovery/interaction/required actions, snapshot identity where applicable, and suggested exit code.

### Refactoring

- [ ] Replace internal invocation-wrapping requests with use-case-specific typed inputs. Repository/workspace identity and invocation policy overrides are explicit fields, not hidden access to CLI objects.
- [ ] Split `UnifiedCliComposition.cs` into owner modules and one production `LoopRelayCompositionRoot`. Resolve configuration and policy once, build one validated catalog, validate exact capabilities and unique owner registrations, then construct one `ILoopRelayApplication`.
- [ ] Make missing or duplicate required owner registrations a typed startup failure before workspace/provider work.
- [ ] Reduce `Program`, `CliArguments`, and `UnifiedCliRunner` to parse -> request -> application -> render -> returned suggested exit code. Formatters accept results/snapshots only.
- [ ] Add dependency tests proving CLI parser/renderer assemblies cannot reference workspace stores, SQL, kernel internals, effect/recovery implementations, completion mutation, or provider transports.
- [ ] Remove retired Roadmap/Plan entrypoints and every alternate composition factory. Update solution and publish scripts so only `LoopRelay.Cli` is a supported application executable; keep `LoopRelay.Certification` as the independent certification executable.

### Verification and exit gate

- [ ] Exercise the full command/query and typed-outcome matrix through the published CLI and directly through the application library. Assert delegation to the correct owner, renderer purity, cancellation forwarding, exact exit mapping, missing/duplicate composition failure, and absence of historical binaries. One boundary and one composition root must be reachable in the production graph.

