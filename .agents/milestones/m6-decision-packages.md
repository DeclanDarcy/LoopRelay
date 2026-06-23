# Milestone 6: Decision Package Generation

## Goal

harden generated proposals into governance-ready immutable decision packages after the core generation loop has been validated.

Tier 0 may use the existing proposal record as the first reviewable package shape if it contains generated options, structured tradeoffs, recommendation evidence, assumptions, and concerns. This milestone formalizes package versioning and package comparison as Tier 1 governance hardening.

## Work

- [x] Add `DecisionPackage` as an immutable snapshot that contains:
  - [x] candidate
  - [x] typed context summary
  - [x] options
  - [x] analyzed options
  - [x] recommendation
  - [x] recommendation evidence
  - [x] assumptions
  - [x] open concerns
  - [x] metadata
  - [x] generated timestamp
- [x] Add `DecisionPackageMetadata`:
  - [x] context fingerprint/version
  - [x] generator version
  - [x] candidate id
  - [x] repository state fingerprint
  - [x] milestone id/path
  - [x] source proposal id
- [x] Add `IDecisionPackageService`.
- [x] Add package validation:
  - [x] summary required
  - [x] context required
  - [x] options required
  - [x] recommendation or no-recommendation explanation required
  - [x] evidence required
  - [x] at least two options unless justified
  - [x] recommended option id must exist when recommendation mode selects an option
- [x] Store package versions under each proposal.
- [x] Render deterministic package markdown with:
  - [x] decision summary
  - [x] context
  - [x] options
  - [x] tradeoff analysis
  - [x] recommendation
  - [x] supporting evidence
  - [x] open concerns
  - [x] assumptions
  - [x] diagnostics
- [ ] Add package comparison:
  - [ ] recommendation changes
  - [ ] option changes
  - [ ] evidence changes
  - [ ] risk changes
  - [ ] context fingerprint changes

## Tests

- [x] Package generation persists JSON and markdown.
- [x] Missing required sections fail validation.
- [x] Package identity is stable and repository-scoped.
- [x] Package versions are immutable after creation.
- [ ] Package comparison detects recommendation and option changes.
- [ ] Resolution snapshots reference the package/proposal fingerprint used for authority.

## Exit Criteria

- [ ] Humans review complete packages, not raw runtime objects.
- [ ] Generated packages are durable, inspectable, comparable, and ready for governance.
