# Decisions

## Newly Authorized

- Confirm the package comparison Milestone 6 slice as correct.
- Treat package-version-based comparison as the correct abstraction for package comparison.
- Preserve the invariant that proposals are workflow objects and packages are immutable governance evidence.
- Keep package comparison side-effect free.
- Keep package comparison informational only; comparison answers what changed and must not create authority, mutation, governance side effects, or resolution changes.
- Use immutable package versions as the comparison inputs rather than mutable proposal state.
- Treat structured risk comparison through `AnalyzedDecisionOption.Risks` as the correct model source.
- Continue Milestone 6 with authority hardening next.
- Resolution snapshots should record the proposal fingerprint, package fingerprint, package version, and resolution timestamp used for human authority.
- Resolution validation should reject stale or mismatched proposal/package authority references unless explicitly acknowledged.
- Scope the next slice to authority provenance and stale-resolution protection.

## Not Authorized

- Do not compare packages through mutable current proposal state.
- Do not turn package comparison into lifecycle action, governance mutation, or decision authority.
- Do not add workflow locking, package locking, cross-proposal coordination, or authority negotiation as part of the next Milestone 6 slice.
- Do not broaden the next slice beyond identifying which evidence was reviewed and protecting against stale authority input.
