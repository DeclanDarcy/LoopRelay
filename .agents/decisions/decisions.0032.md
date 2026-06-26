# Decisions: 2026-06-26 Workflow TypeScript Consumer Verification Checkpoint Authorization

These decisions capture only newly authorized direction from the user response following Slice 0029.

## Authorized Decisions

1. Accept Slice 0029 as the correct continuation of the workflow Oracle family.
   - TypeScript consumer verification was the right next step before workflow request-boundary verification and artifact freshness.
   - The ordering mirrors the repository pilot lifecycle while respecting workflow-specific transport differences.

2. Treat `workflowContractFixture.test.ts` as a downstream Oracle consumer-verification implementation.
   - The frontend test validates conformance to backend Oracle truth.
   - It does not create a second contract authority or a separate consumer-verification architecture.
   - The broader pattern remains one Contract Oracle architecture with multiple implementation points.

3. Preserve backend serialized JSON as workflow contract authority.
   - Authority continues to flow from backend serialization to golden fixture to consumer verification.
   - Manual TypeScript workflow types remain verified consumers, not contract authorities.

4. Keep the Rust workflow posture as passive transport.
   - Workflow Rust commands returning `serde_json::Value` should preserve backend response shape.
   - Do not introduce a Rust `WorkflowInstance` mirror merely to satisfy consumer verification.
   - Rust workflow concerns should remain aligned with Milestone 1.3 passive transport work.

5. Treat populated `decisionSession` as a future fixture variant.
   - The initial `decisionSession: null` fixture remains valid.
   - Populated `decisionSession` coverage should extend scenario coverage within the same workflow contract family, not redefine the initial fixture.

6. Characterize current workflow family posture as partially advanced but uncertified.
   - Field inventory, field-role classification, Oracle fixture, backend serialization verification, and TypeScript consumer verification are complete for the current variant.
   - Artifact freshness, request-boundary verification, and local certification remain pending.

## Next Authorized Sequence

1. Stage, commit, and push the current Slice 0029 checkpoint and this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, add workflow request-boundary verification before workflow artifact freshness and local certification.
