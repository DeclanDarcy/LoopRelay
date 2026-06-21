# Decisions

## Newly Authorized Decisions

- Epic 3 is complete after the final review-hardening slice unless final packaging validation uncovers unexpected build or integration issues.
- Corrupt supporting artifacts must not make a repository unusable.
- Supporting artifacts are not workflow authority; reports, proposal metadata, diagnostics, and review-support files must degrade as lost observation or skipped support state rather than lost current understanding.
- Corrupt continuity reports should be ignored during report listing because reports are observational diagnostics, not understanding authority.
- Corrupt operational-context proposal metadata should be ignored during proposal listing because proposal metadata supports review workflow but is not current understanding.
- Regression coverage for corrupt report artifacts and corrupt proposal metadata is now a continuity invariant.
- The final packaging validation should confirm that repository state is shippable using the standard backend, UI, and shell build commands.

## Recommended Next Slice

Run the final packaging build pass, then proceed with the normal repository-level review, release, or PR workflow if no unexpected failures appear.
