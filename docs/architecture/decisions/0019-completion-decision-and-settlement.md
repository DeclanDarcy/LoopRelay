# ADR-0019: Separate Completion Decision from Terminal Settlement

## Status

Accepted.

## Decision

Completion Authority records a typed, immutable `CompletionDecision`. Only a
`CertifiedCandidate` can produce a `CompletionCertificate`, and the certificate is not a public
terminal claim. Before closure work starts, Completion Authority persists the decision,
certificate, and one content-hashed `CompletionClosurePlan` in the same transaction.

The plan orders archive materialization and verification, roadmap-context materialization,
nested publication, parent publication, route and independent postcondition evidence, decision
and session/checkpoint retirement, and finally ledger settlement. Closure mutations are M8
effects. Settled work is retained when later work fails; recovery resumes or reconciles the same
plan.

A `CertifiedTerminalFact` is appended atomically with a `CertifiedTerminal` settlement only when
every required closure operation has succeeded and has verified receipt evidence. Pending work
returns `EffectsPending`; unknown outward work returns `RecoveryRequired`; failure,
cancellation, and typed cannot-proceed outcomes remain distinct. The terminal fact is monotonic
and unique per root run.

## Consequences

- Completion decisions remain pure with respect to external mutation.
- A certificate supports closure but cannot make status appear terminal early.
- Archive success followed by push failure remains nonterminal and recoverable.
- Restart and the canonical read model can expose the exact decision, plan, pending operation,
  settlement, certificate, and terminal identities.
