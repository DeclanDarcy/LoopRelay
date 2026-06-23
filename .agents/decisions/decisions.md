# Decisions

## Newly Authorized

- Confirm the Milestone 6 authority-hardening slice as correct.
- Treat resolution as the correct home for authority provenance.
- Preserve the distinction that packages are reviewed evidence and resolutions are authority.
- Treat `DecisionResolvedProposalSnapshot` as the correct persistence point for package authority metadata.
- Keep stale proposal/package fingerprint rejection as the correct governance protection.
- Keep markdown authority-provenance projection because JSON remains authoritative and markdown remains reviewable.
- Continue Milestone 6 with UI authority submission next.
- The review workspace should surface proposal fingerprint, package version, and package fingerprint for the reviewed artifact.
- Resolution UI should submit expected proposal and package fingerprints automatically.
- Package/proposal mismatch during resolution should surface as a visible conflict.
- Scope the next slice narrowly to UI authority participation and conflict visibility.

## Not Authorized

- Do not expand the next slice into package locking.
- Do not add review sessions.
- Do not add reservation systems.
- Do not add multi-user coordination infrastructure.
- Do not treat packages as authority.
