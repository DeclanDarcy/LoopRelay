# Decisions

## Newly Authorized

- Confirm the completed M6 opening slice as correct.
- Preserve the sequencing where generation creates a proposal first, then creates an immutable package snapshot after proposal persistence/projection.
- Treat the proposal as the active workflow object.
- Treat the package as immutable governance evidence, not decision authority.
- Preserve human review and human resolution as the path to decision authority.
- Keep create-once `PKG-*` immutability as the right implementation strategy for M6.
- Keep package persistence at the repository abstraction layer with allocate, list, read, and save semantics.
- Continue Milestone 6 with package validation next.
- Implement package validation before package comparison.
- Validate required context.
- Validate required options.
- Validate insufficient options unless explicitly justified.
- Require either a recommendation or a no-recommendation explanation.
- Require evidence when a recommendation exists.
- Verify the recommended option id exists inside the package.

## Not Authorized

- Do not implement package comparison yet.
- Do not replace proposals with packages.
- Do not let packages imply decision authority.
- Do not add dashboards, certification, or broader package governance before package validity is established.
