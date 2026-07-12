# M14 — Orchestration Kernel


### Kernel lifecycle

Create a kernel coordinator, using the existing resolver/runtime/controller/chain components where their contracts remain valid, that owns this universal sequence:

```text
observe -> resolve -> gate -> interact -> authorize attempt -> dispatch one attempt
-> interpret -> validate -> freshness check -> atomic product/state/effect intent
-> coordinate/reconcile effects -> recover if required -> reobserve -> chain -> project
```

### Implementation

- [ ] Move fresh-attempt authorization from `WorkflowChainRunner` into the kernel. Select fresh versus recovered authorization from durable run state and M9 plans.
- [ ] Re-enter an existing active root run/workflow instance after restart instead of minting a new run for every CLI invocation. New successor workflows receive new workflow-instance IDs under the same root run.
- [ ] Keep `TransitionRuntime` single-attempt and single-dispatch. It must not gain retry, recovery, effect execution, interaction presentation, or chaining policy.
- [ ] Make all causally required writes fail closed: run/workflow-instance creation, policy resolution, attempt intent, receipts, prompt/dispatch facts, candidate/evaluation facts, promotion/effect intent, recovery/interaction/chain facts. Remove best-effort catches around required run and policy writes.
- [ ] Reobserve through owner projections after every attempt/effect/recovery/interaction cycle. A prompt or handler output can only create candidates; only the atomic commit store can promote products/state.
- [ ] Move unbounded continuation guards, progression, successor choice, and completion-route looping from `CanonicalCliApplicationService` and `UnifiedEffectExecutor` into catalog-driven kernel decisions.
- [ ] Extract nested prompt handlers, context builders, interpreters, product validators, and local artifact handlers from `UnifiedCliComposition` into owner modules. They may contain workflow-specific transformation logic but no progression, policy, persistence selection, effects, recovery, or prompt framing.
- [ ] Replace `RepositoryObserver` raw SQL and compatibility heuristics with canonical domain projections plus independent filesystem/Git observation.
- [ ] Expose one typed kernel command/result containing outcome, causal identities, evidence, pending effects, recovery case, interaction request, and snapshot identity.
- [ ] Inventory every agent role in CLI, completion, projection, decision, review, and roadmap/plan/execute handlers. Replace raw `BrainConfiguration` constructor inputs with a durable role/session policy resolved by Policy Authority and linked to the attempt.
- [ ] Move `CODEX_HOME` and other ambient provider inputs behind validated configuration resolution with provenance. No handler or decision path may read ambient environment state directly.
- [ ] Replace provisional `runtime_cli_application` and `prompt_policy_cli_application` literals with identities produced by the resolved runtime profile and prompt-policy profile. Reject missing profile facts before attempt authorization.
- [ ] Stop synthesizing adaptive capability evidence. Provider operations are authorized only by the exact compatibility profile actually observed for that executable/app-server schema.
- [ ] Keep telemetry, usage-limit wait/retry, input-wait reporting, runtime prerequisite checks, and terminal session evidence as composed Runtime/Policy services. Add production-composition tests proving configured enable/disable values change the active wrapper graph or are rejected.

### Verification and exit gate

- [ ] Fault-inject after every durable lifecycle phase for both chains. Restart must preserve lineage and avoid duplicate provider/effect work. Test every success/non-success outcome, freshness conflict, no eligible transition, ambiguous selection, interaction, recovery, chain boundary, and required-write failure. Architecture tests must prove no feature runner or client advances canonical state and there is one reachable production kernel.

