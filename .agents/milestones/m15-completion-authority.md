# M15 — Completion Authority


### Decision and closure contracts

Consolidate completion in `LoopRelay.Completion` behind:

- [ ] `CompletionDecision`: CertifiedCandidate, Continue, Waiting, Failed, Cancelled, or SpecificCannotProceed with evidence/gate/review identities;
- [ ] `CompletionCertificate`: immutable certificate only for a valid CertifiedCandidate;
- [ ] `CompletionClosurePlan`: immutable ordered operations and dependencies; and
- [ ] `CompletionSettlement`: EffectsPending, RecoveryRequired, Failed, Cancelled, SpecificCannotProceed, or CertifiedTerminal.

- [ ] Exactly one service decides completion. Decision is pure with respect to external mutation. Persist the decision/certificate and complete closure plan before executing closure effects.

### Ordered closure

The catalog/plan builder must explicitly order:

- [ ] archive materialization and semantic verification;
- [ ] roadmap completion-context update/materialization;
- [ ] nested `.agents` commit and required push when that surface changed;
- [ ] parent gitlink/repository commit and required push when applicable;
- [ ] completion-route evidence and independent postcondition checks;
- [ ] decision, warm-session, and certification-checkpoint retirement; and
- [ ] certified terminal fact.

- [ ] The final fact is unavailable while any required effect is pending, unknown, failed, or stalled. Completed effects are never rolled back merely because a later effect failed; M9 resumes/reconciles the same closure plan.

### Refactoring

- [ ] Replace generic completion/non-implementation blocker enums per D2.
- [ ] Move archive, context, Git, cleanup, and terminal mutations out of `CompletionCertificationService`, `UnifiedPromptExecutor`, `UnifiedEffectExecutor`, and CLI cleanup into typed M8 executors.
- [ ] Delete direct `CommitGate` usage from completion.
- [ ] Replace opaque completion checkpoint metadata with typed decision/plan/effect identities.
- [ ] Add a dedicated completion subprojection consumed by M16.

### Verification and exit gate

- [ ] Test certified, continue, each specific cannot-proceed reason, failed, cancelled, effect-pending, and recovery-required through persistence, read model, application result, renderer, and exit code. Fault-inject at every closure step, including archive success/push failure and unknown Git result. After settlement, rerun must create zero provider sessions/turns/effects and no user-tree or Git mutation. Only then may `CertifiedCompletion` be promoted.

### Certificate, closure, and terminal fact

A `CompletionCertificate` records an accepted certified candidate and its evidence; it is not the
public terminal claim. The public run is certified only after every required effect in the
immutable closure plan has a verified receipt and the Completion/State settlement transaction
appends the monotonic `CertifiedTerminal` fact.

The terminal fact is authoritative ledger settlement, not an outward effect. Projection-file
materialization may be an effect. This prevents an effect from claiming authoritative completion
before effects are known settled. A later decision may supersede a nonterminal completion decision
through a new attempt; it never edits the old decision or certificate.

### Partial closure and cleanup

Archive success followed by push failure remains `EffectsPending`, or `RecoveryRequired` when the
push outcome is unknown, never certified. Retain settled closure effects rather than compensating
them because a later effect failed. Cleanup/retirement runs only after archive, context update,
nested and parent publication, and independent postconditions. Cleanup failure leaves the same
closure plan recoverable. A terminal rerun reads the terminal fact and creates zero new provider
sessions/turns, effect intents, user-tree writes, or Git mutations.

D2 and M15's obstacle vocabulary and partial-effect/resume behavior remain owner rulings until
accepted. The outcome matrix includes every specific cannot-proceed reason plus continue, waiting,
failed, cancelled, effect-pending, recovery-required, and certified-terminal.

Completion ambiguity activates M10's reserved typed interaction category and resolver contract; it
does not create a completion-specific interaction store or accept raw response JSON directly.
