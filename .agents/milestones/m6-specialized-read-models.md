# Milestone 6: Optional Specialized Read Models

Goal: implement only the specialized read models approved by Milestone 5. Skip this milestone for concepts that remain derived.

## Allowed Implementation Choices

| Choice | Description |
| --- | --- |
| Derived cache | Rebuildable file or memory cache, clearly marked non-authoritative. |
| Read-model report | Persisted reconstruction/report artifact created on demand. |
| First-class entity | Repository-backed structured artifact with explicit authority disclaimers and recovery rules. |

If no concept is approved for materialization, close this milestone with a no-op certification report and proceed to long-horizon validation.

## Constraints

- [ ] Do not introduce CRUD endpoints for all concepts by default.
- [ ] Do not create state machines just because an event family exists.
- [ ] Do not persist direction as a first-class object unless the materialization review proves a stable abstraction.
- [ ] Every new artifact type must document how it can be rebuilt or why it cannot be rebuilt.

## Tests

- [ ] Approved read models are rebuildable from events or explicitly justified.
- [ ] No unapproved artifact directories are created.
- [ ] New projections remain explanatory.
- [ ] Existing authority boundaries remain intact.

## Exit Criteria

- [ ] Only justified specialization exists.
- [ ] Event-led reconstruction remains the primary path.
