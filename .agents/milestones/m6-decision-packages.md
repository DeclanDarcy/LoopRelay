# Milestone 6: Decision Package Generation

## Goal

harden generated proposals into governance-ready immutable decision packages after the core generation loop has been validated.

Tier 0 may use the existing proposal record as the first reviewable package shape if it contains generated options, structured tradeoffs, recommendation evidence, assumptions, and concerns. This milestone formalizes package versioning and package comparison as Tier 1 governance hardening.

## Work

- [ ] Add `DecisionPackage` as an immutable snapshot that contains:
  - [ ] candidate
  - [ ] typed context summary
  - [ ] options
  - [ ] analyzed options
  - [ ] recommendation
  - [ ] recommendation evidence
  - [ ] assumptions
  - [ ] open concerns
  - [ ] metadata
  - [ ] generated timestamp
- [ ] Add `DecisionPackageMetadata`:
  - [ ] context fingerprint/version
  - [ ] generator version
  - [ ] candidate id
  - [ ] repository state fingerprint
  - [ ] milestone id/path
  - [ ] source proposal id
- [ ] Add `IDecisionPackageService`.
- [ ] Add package validation:
  - [ ] summary required
  - [ ] context required
  - [ ] options required
  - [ ] recommendation or no-recommendation explanation required
  - [ ] evidence required
  - [ ] at least two options unless justified
  - [ ] recommended option id must exist when recommendation mode selects an option
- [ ] Store package versions under each proposal.
- [ ] Render deterministic package markdown with:
  - [ ] decision summary
  - [ ] context
  - [ ] options
  - [ ] tradeoff analysis
  - [ ] recommendation
  - [ ] supporting evidence
  - [ ] open concerns
  - [ ] assumptions
  - [ ] diagnostics
- [ ] Add package comparison:
  - [ ] recommendation changes
  - [ ] option changes
  - [ ] evidence changes
  - [ ] risk changes
  - [ ] context fingerprint changes

## Tests

- [ ] Package generation persists JSON and markdown.
- [ ] Missing required sections fail validation.
- [ ] Package identity is stable and repository-scoped.
- [ ] Package versions are immutable after creation.
- [ ] Package comparison detects recommendation and option changes.
- [ ] Resolution snapshots reference the package/proposal fingerprint used for authority.

## Exit Criteria

- [ ] Humans review complete packages, not raw runtime objects.
- [ ] Generated packages are durable, inspectable, comparable, and ready for governance.
