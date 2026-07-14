# M19 — Execute capability convergence


### Implementation

- [x] Encode D13 in the catalog, including explicit nonterminal routes back to decision/readiness and the only terminal route into M15.
- [x] Bind decision products, recommendation evidence, policy evaluation, effective runtime profile, prompt fact, and input manifest into one `ExecutionAuthorization`. Remove all paths where a recommendation or raw model/effort reaches `AgentSpecs` directly.
- [x] Make decision, implementation, handoff, operational-context update, publication, repository evaluation, milestone evaluation, non-implementation review, and completion handlers candidate/evidence producers only.
- [x] Derive stall/no-substantive-change from current Git/product/history evidence. Delete counters/latches that require manual unblock.
- [x] Route every filesystem/Git mutation through M8, every retry/resume/fork through M9, every human decision through M10, and every completion decision/closure through M15.
- [x] Apply D5 at every provider/effect boundary. Unknown decision, implementation, handoff, publication, or completion work reconciles before repeat.
- [x] Preserve all outcome discriminants through M16 and the application boundary.
- [x] Port useful `LoopRunner`, `ExecutionStep`, `CommitGate`, history, milestone, decision-session, recovery, handoff, and completion tests to canonical owners.
- [x] After both full chains, boundary-fault campaigns, and owner acceptance pass, delete `LoopRunner.cs`, `ExecutionStep.cs`, `CommitGate.cs`, feature-specific progression/policy fallbacks, superseded warm/checkpoint stores, and last-only consumers/tests.

### Exit gate

- [x] Execute is explainable and restart-safe from readiness through certified terminal state. Cancellation, stall, unknown work, partial effects, partial closure, and specific cannot-proceed remain distinct; no blind repeat or legacy progression/policy path is reachable; deletion changes no supported behavior.

### Execution-authorization freshness

`ExecutionAuthorization` is an immutable fact/hash over decision product, recommendation evidence,
policy evaluation, effective runtime profile, exact provider profile/capabilities, prompt-policy
profile, rendered-prompt fact, consumed-input manifest, catalog transition,
permission/approval/sandbox/network ceilings, and causal identities. Attempt authorization
rechecks that every referenced fact is current. A stale or missing reference returns a specific
result and cannot fall back to raw model/effort or recommendation values.

### Stall predicate and sequence ruling

D13's first-run/review order remains proposed. Once accepted, encode it in catalog successors and
test all nonterminal routes. Define a catalog-owned deterministic `substantive change` predicate
over current Git diff/commit evidence, promoted product versions, history/evaluation facts, and
declared output surfaces. It identifies which evidence was unchanged and returns `Stalled` without
incrementing a counter or setting a manual latch. Until the predicate and sequencing are accepted,
M19 cannot delete the retained loop.

Cancellation and unknown cases inherit the accepted M9 boundary matrix. An already certified root
short-circuits without decision, provider, handler, completion, or effect work.

Run both Traditional and Eval full chains for this shared-chain convergence milestone.
