# Canonical Orchestration Baseline

This inventory records the supported post-convergence orchestration surface. The former Roadmap
and Plan executables are historical evidence only; neither is a supported or reachable runtime
authority.

## Public application surface

`LoopRelay.Cli` is the sole supported application executable. Its parser produces typed requests
for bounded or chained runs, status, storage, compatibility import, recovery, interactions,
completion observation, and capability diagnostics. `LoopRelay.Application` owns the public
request/result contracts and exact outcome/exit semantics. `LoopRelay.Certification` remains a
separate evidence-producing executable, not an alternate application entrypoint.

There is no public `unblock` command. A blocked operation resumes only through the typed recovery,
interaction, effect-settlement, or completion-closure owner named by its persisted evidence.

## Canonical owners

- The workflow catalog owns Traditional Roadmap, Eval Roadmap, Plan, and Execute definitions.
- The orchestration kernel owns transition progression; handlers produce candidates and evidence.
- the effect coordinator owns required filesystem, Git, publication, and cleanup mutations.
- Recovery Authority owns unknown-work reconciliation, retry, resume, and fork decisions.
- Interaction Authority owns durable human decisions.
- Completion Authority owns terminal decisions, certification, closure planning, settlement, and
  the certified terminal fact.
- Workspace State Authority owns the SQLite evidence ledger and canonical read model.

Structured runtime state is canonical in `.LoopRelay/persistence/looprelay.sqlite3`. Legacy files
and schemas are never runtime fallback authorities. A supported legacy portfolio can enter only
through `import detect|preview|execute|verify`; an imported workspace subsequently runs with the
adapter absent from the ordinary execution graph.

## Required semantics

- Prompt success is not workflow completion.
- Status and verification are read-only projections.
- Cancellation, stall, unknown work, partial effects, recovery-required, interaction-required,
  and every specific cannot-proceed reason remain distinct through the application result.
- Required effects settle from durable intent and exact receipts before products or completion
  can become observable.
- Nested `.agents` publication settles before its parent gitlink, commit, and required push.
- A terminal Execute result exists only after Completion Authority records a certified terminal
  fact backed by the exact closure receipts.
- Milestone checkbox changes are canonical progress evidence when their promoted surface changes.

## Coverage

Canonical behavior is covered by the Core, Orchestration Primitives, Completion, Application, CLI,
provider compatibility, projection, and certification test projects. The convergence architecture
verifier additionally audits project reachability, owner registrations, catalogs, persistence,
effects, prompt assets, public claims, and historical binaries.
