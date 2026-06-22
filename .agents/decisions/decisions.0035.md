# Decisions

## Newly Authorized

- M7C should continue prioritizing authority integrity verification over broader semantic evaluation.
- Resolution snapshot integrity is a high-value governance analyzer and should continue to be treated as authority corruption when broken.
- Missing resolved snapshots and invalid resolved snapshot fingerprints are blocking governance findings, not documentation warnings.
- Supersession graphs should remain non-branching unless the lifecycle model is explicitly changed later.
- Multiple replacement parents for one superseded decision are corruption findings.
- Multiple accepted authorities for one candidate are blocking findings because downstream consumers cannot infer current truth.
- Governance is converging into three relevant layers:
  - structural health: lineage, ancestry, references, fingerprints, snapshots
  - authority health: competing authorities, inactive references, boundary violations
  - execution readiness: whether execution can safely consume the repository
- The next implementation priority is conflicting execution directives.
- Execution projection readiness tests should be added before adding more broad analyzers so `BlocksExecutionProjection` semantics stay deterministic before M8 consumes them.
- Projection failure detection should follow because missing, invalid, or authority-broken projections are objective repository defects.
- Unresolved stale proposal findings are useful but should be framed as review backlog findings with low severity unless stronger structural evidence exists.
- Repeated-signal coverage should come after authority and execution-readiness work because it is closer to discovery effectiveness than repository integrity.
- Current M7 status is accepted as:
  - M7A Governance Reporting complete
  - M7B Governance Surface complete
  - M7C Structural Hardening roughly 70-80% complete
  - M7D Readiness Validation partially started
  - M7E Certification not started
- Governance must remain advisory:
  - governance reports problems
  - execution decides what to consume or do
  - continuity decides what to adopt
