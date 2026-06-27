# Decision Record Template

Use this template for architecture-affecting decisions while the implementation program is active.

## Metadata

- Decision id:
- Date:
- Status: proposed | accepted | superseded | reversed | quarantined
- Decision class:
- Capability:
- Invariant:
- Authority owner:
- Mechanism owner:
- Compatibility owner:
- Evidence package:
- Supersedes:
- Superseded by:

## Decision

State the architectural decision in one or two sentences.

## Context

Explain what changed, why governance is required, and which files, routes, generated artifacts, consumers, or reference docs are affected.

## Evidence

Link evidence packages and summarize the relevant proof:

- inventory evidence:
- contract or generated artifact evidence:
- authority/projection/state/transport/runtime evidence:
- compatibility evidence:
- mechanism evidence:
- certification or acceptance evidence:

## Alternatives

List the meaningful alternatives considered and why they were rejected or deferred.

## Compatibility Impact

Name affected consumers, transitional fields or routes, replacement path, retirement condition, and any derivation proof.

## Regression Impact

Name existing regressions affected, new guards required, weakened or quarantined mechanisms, lifecycle changes, and revalidation commands.

## Rollback Path

Describe how to restore the prior verified behavior, compatibility layer, generated artifact, fixture baseline, or mechanism state if evidence proves the decision wrong.

## Baseline Updates

List required updates to durable reference docs, capability matrix, mechanism docs, fixtures, generated artifacts, milestone evidence, or decision records.

## Follow-Up

List required follow-up slices, retirement conditions, certification work, or revalidation triggers.
