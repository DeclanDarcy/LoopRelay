# M19 — Execute capability convergence


### Implementation

- [ ] Encode D13 in the catalog, including explicit nonterminal routes back to decision/readiness and the only terminal route into M15.
- [ ] Bind decision products, recommendation evidence, policy evaluation, effective runtime profile, prompt fact, and input manifest into one `ExecutionAuthorization`. Remove all paths where a recommendation or raw model/effort reaches `AgentSpecs` directly.
- [ ] Make decision, implementation, handoff, operational-context update, publication, repository evaluation, milestone evaluation, non-implementation review, and completion handlers candidate/evidence producers only.
- [ ] Derive stall/no-substantive-change from current Git/product/history evidence. Delete counters/latches that require manual unblock.
- [ ] Route every filesystem/Git mutation through M8, every retry/resume/fork through M9, every human decision through M10, and every completion decision/closure through M15.
- [ ] Apply D5 at every provider/effect boundary. Unknown decision, implementation, handoff, publication, or completion work reconciles before repeat.
- [ ] Preserve all outcome discriminants through M16 and the application boundary.
- [ ] Port useful `LoopRunner`, `ExecutionStep`, `CommitGate`, history, milestone, decision-session, recovery, handoff, and completion tests to canonical owners.
- [ ] After both full chains, boundary-fault campaigns, and owner acceptance pass, delete `LoopRunner.cs`, `ExecutionStep.cs`, `CommitGate.cs`, feature-specific progression/policy fallbacks, superseded warm/checkpoint stores, and last-only consumers/tests.

### Exit gate

- [ ] Execute is explainable and restart-safe from readiness through certified terminal state. Cancellation, stall, unknown work, partial effects, partial closure, and specific cannot-proceed remain distinct; no blind repeat or legacy progression/policy path is reachable; deletion changes no supported behavior.

