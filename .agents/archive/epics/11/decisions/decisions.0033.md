# Decisions: 2026-06-26 Workflow Request Boundary Checkpoint Authorization

These decisions capture only newly authorized direction from the user response following Slice 0030.

## Authorized Decisions

1. Accept Slice 0030 as the correct continuation of the workflow Oracle family.
   - The workflow request-boundary verifier reused the established backend endpoint metadata, passive Rust transport, and TypeScript invocation model.
   - No workflow-specific request verification architecture was introduced.

2. Treat request-boundary mechanism reuse as the key architectural result of Slice 0030.
   - The same mechanism now covers repository dashboard, repository workspace, and workflow projection.
   - The workflow family adds richer response semantics without requiring a new request-boundary model.

3. Preserve the Rust workflow projection posture as passive transport.
   - `get_workflow_projection` returning `Result<Value, String>` means Rust preserves workflow response shape without modeling it.
   - Do not introduce a Rust `WorkflowInstance` mirror solely for symmetry with repository contract families.

4. Treat missing workflow dev mock coverage as a coverage gap, not a certification blocker by itself.
   - Unless the dev mock is an active workflow projection consumer, absence of a workflow handler should remain recorded as a gap.
   - It should not automatically prevent local workflow pilot certification.

5. Treat populated `decisionSession` as a fixture scenario gap, not a mechanism gap.
   - The current `decisionSession: null` fixture variant remains a valid intentional scenario.
   - Populated decision-session coverage can be added later as another fixture scenario within the same workflow contract family.

6. Proceed to workflow artifact freshness before local workflow certification.
   - Reuse the existing manifest, verifier, and failure taxonomy.
   - Expect no new Oracle mechanism to be introduced for workflow freshness.

7. After workflow freshness, explicitly review whether workflow introduced any new Oracle mechanisms.
   - The target answer is none.
   - If true, the workflow family becomes repeatability evidence for Oracle stability across a more semantically complex contract.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0030 plus this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, add workflow artifact freshness, then review readiness for local workflow Oracle certification.
