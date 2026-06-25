# Decisions

## Newly Authorized

- Continue Milestone 6 with structured authority-boundary error responses next.
- Boundary violations should explicitly model why a request was rejected, not only that it failed.
- Structured boundary responses should include boundary rule, owning domain, rejected assertion, allowed alternative, diagnostic detail, and severity where applicable.
- Preserve the pattern: domain authority emits structured semantic projection; transport and typed clients carry it; UI renders it without recomputing authority.
- After authority-boundary diagnostics are complete, continue remaining Milestone 6 work through the same structured transparency pattern for lifecycle risk and grouped diagnostics.
