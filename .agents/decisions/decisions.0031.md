# Decisions: 2026-06-26 Workflow Instance Fixture Checkpoint Authorization

These decisions capture only newly authorized direction from the user response following Slice 0028.

## Authorized Decisions

1. Accept Slice 0028 as an architectural checkpoint.
   - The workflow family has crossed from inventory/review-gate status into executable Oracle coverage.
   - The fixture was introduced after field-role classification and within the existing Milestone 0.2 lifecycle.

2. Treat `workflow-instance.golden.json` as backend serialization Oracle evidence only.
   - The fixture does not make Rust, TypeScript, development mocks, characterization data, or UI code contract authority.
   - Downstream consumers must continue to be verified or reported against backend Oracle truth.

3. Preserve `decisionSession: null` as the first workflow fixture state.
   - Explicit null is now part of the observed `WorkflowInstance` serialization contract.
   - Populated `decisionSession` is additional future coverage, not a requirement for this first fixture.

4. Keep flattened workflow lifecycle and eligibility fields compatibility-sensitive.
   - Flattened statuses and booleans must derive from backend-owned nested/source authority.
   - They must not become independent canonical semantics.

5. Distinguish workflow fixture variants from workflow contract families.
   - `workflow-instance.golden.json` is a representative variant within the `WorkflowInstance` contract family.
   - Future populated decision-session, alternate lifecycle, terminal workflow, or failed workflow fixtures should be treated as variants of the same family unless a distinct contract identity is authorized.

6. Continue the workflow Oracle lifecycle through downstream consumer verification next.
   - TypeScript should be verified against the workflow fixture.
   - Rust should be reported as drift evidence if applicable.
   - Dev mock coverage should be reported as a coverage gap until implemented.
   - Freshness and request-boundary verification should wait until after TypeScript consumer verification.

## Next Authorized Sequence

1. Stage, commit, and push the current Slice 0028 checkpoint and this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, add TypeScript consumer verification for `WorkflowInstance` before adding workflow artifact freshness or request-boundary verification.
