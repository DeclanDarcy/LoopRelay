# Decisions

## Newly Authorized

- Continue Milestone 9 in the current diagnostic-first sequence.
- Treat the first M9 slice as accepted: enriched execution context may include constraints, directives, priorities, and architecture rules while preserving the existing authority boundary.
- Persist M9 projection diagnostics before adding decision influence tracing.
- Persist diagnostics for included decisions, excluded decisions, superseded decisions, projected statements, projection conflicts, projection timestamp, and projection fingerprint.
- Keep `ArchitectureRules` and `Priorities` as execution context elements derived from resolved authority.
- Continue deriving execution guidance only from accepted, resolved, governance-passing decisions.
- Preserve compatibility with the existing constraints/directives prompt contract while enriching execution context.
- Keep conflict detection in the projection layer before influence tracing.

## Not Authorized

- Do not project generated recommendations directly into execution guidance.
- Do not derive priorities or architecture rules from current unresolved recommendations.
- Do not add `ExecutionInfluenceTrace` before durable projection diagnostics exist.
- Do not expand execution UI influence surfaces before persisted diagnostics prove the enriched projection path.
