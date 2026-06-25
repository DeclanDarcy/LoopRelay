# Decisions

## Newly Authorized

- Accept the completed Milestone 5 push retry transparency slice as aligned with the execution authority model.
- Preserve `ExecutionSessionService.PushAsync` as the sole authority for push lifecycle and retry state.
- Preserve `409 Conflict` transport semantics while carrying structured push retry state to the UI.
- Treat retry evidence as projected execution state, not frontend inference.
- Proceed with the next Milestone 5 slice: backend git action eligibility.
- Establish the git action eligibility projection and endpoint before shell, TypeScript, hook, UI, and tests.
- Keep git action eligibility execution-owned and authoritative.
- React must render eligibility, disabled reasons, stale preparation, previous push failure, selected path state, and remote branch state without deriving or recomputing them.
- Stage all resulting changes, create a single commit for this execution session, push the branch, then stop.
