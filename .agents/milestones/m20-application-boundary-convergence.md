# M20 — Application Boundary convergence


### Project and contracts

- [x] Create `src/LoopRelay.Application/` as a reusable class library for public commands, queries, results, the thin coordinator, and application-level outcome/exit semantics. It may reference owner contracts but must not reference CLI parsing/rendering, `Microsoft.Data.Sqlite`, provider transports, or filesystem/Git implementations.

The command/query matrix must cover:

- [x] run default/forced/bounded workflow;
- [x] canonical status;
- [x] storage verify/init/migrate/export/sync;
- [x] import detect/preview/execute/verify;
- [x] recovery inspect/plan/execute;
- [x] interaction list/show/respond/cancel;
- [x] completion status/reconcile; and
- [x] capability/prerequisite diagnostics.

- [x] Every result carries correlation and causal IDs, exact outcome/reason, evidence links, warnings, pending effects, recovery/interaction/required actions, snapshot identity where applicable, and suggested exit code.

### Refactoring

- [x] Replace internal invocation-wrapping requests with use-case-specific typed inputs. Repository/workspace identity and invocation policy overrides are explicit fields, not hidden access to CLI objects.
- [x] Split `UnifiedCliComposition.cs` into owner modules and one production `LoopRelayCompositionRoot`. Resolve configuration and policy once, build one validated catalog, validate exact capabilities and unique owner registrations, then construct one `ILoopRelayApplication`.
- [x] Make missing or duplicate required owner registrations a typed startup failure before workspace/provider work.
- [x] Reduce `Program` and `UnifiedCliRunner` to parse -> request -> application -> render -> returned suggested exit code; retire the duplicate `CliArguments` parser. Formatters accept results/snapshots only.
- [x] Add dependency tests proving CLI parser/renderer assemblies cannot reference workspace stores, SQL, kernel internals, effect/recovery implementations, completion mutation, or provider transports.
- [x] Remove retired Roadmap/Plan entrypoints and every alternate composition factory. Update solution and publish scripts so only `LoopRelay.Cli` is a supported application executable; keep `LoopRelay.Certification` as the independent certification executable.

### Verification and exit gate

- [x] Exercise the full command/query and typed-outcome matrix through the published CLI and directly through the application library. Assert delegation to the correct owner, renderer purity, cancellation forwarding, exact exit mapping, missing/duplicate composition failure, and absence of historical binaries. One boundary and one composition root must be reachable in the production graph.

### Dependency direction and policy scope

`LoopRelay.Application` references owner contract assemblies. Owner assemblies do not reference
Application or CLI; temporary pre-M20 adapters live at the outer CLI/composition edge. The CLI
references Application contracts plus pure rendering/parsing only. Infrastructure implementations
are visible only to the composition root.

`Resolve configuration and policy once` means parse/validate raw configuration and construct one
policy resolver once per composition. It does not mean reuse one global effective policy for all
attempts/sessions. Resolve and durably record effective policy/runtime profiles at their
invocation/attempt/session scope using current inputs and provenance.

### Request/result and composition guarantees

Every application request has a correlation ID, explicit workspace/repository identity, invocation
mode/limits, policy overrides, and cancellation. Every result carries the exact typed
discriminant/reason, causal IDs when created, evidence links, warnings, pending effects,
recovery/interaction/action identities, snapshot identity, and suggested exit code. Sharing an
exit code never changes the discriminant.

Composition validation runs before workspace/provider work and reports all missing, duplicate, or
version-incompatible owner/registry dependencies. The production graph contains one configuration
source, policy resolver, validated catalog snapshot, exact-profile registry, application service,
and composition root. No alternate factory remains reachable.

Run both Traditional and Eval full chains for this shared application-boundary milestone.
