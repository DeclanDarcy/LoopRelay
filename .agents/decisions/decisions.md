# Decisions: 2026-06-26 Workflow Fixture Ownership Classification

These decisions capture only newly authorized direction from the response accepting Slice 0026 and refining the next workflow fixture slice.

## Authorized Decisions

1. Preserve workflow projection coverage as inventory-only until the fixture slice begins.
   - Slice 0026 remains protection and planning evidence, not certification.
   - Workflow coverage should continue through the established Oracle lifecycle: inventory, ownership, fixture, consumer verification, artifact freshness, request-boundary verification, and local certification.

2. Use workflow as the next semantic robustness test for the Oracle.
   - Workflow is the first contract family in Milestone 0.2 to materially stress lifecycle state, transitions, gates, timelines, eligibility flags, diagnostics, completion state, and decision-session linkage.
   - If the Oracle mechanisms remain unchanged through workflow coverage, that becomes stronger evidence that the architecture generalizes beyond repository read models.

3. Treat Rust workflow response passivity as distinct from repository Rust mirror drift.
   - Because workflow responses already travel through `serde_json::Value`, there is no typed Rust workflow response mirror to verify in the same way as repository dashboard/workspace.
   - The principal downstream workflow compatibility risks are manual TypeScript types, development mocks, and other manually maintained consumers.

4. Classify the missing dev Tauri workflow command handler as a coverage gap, not drift.
   - Drift requires two existing artifacts that disagree.
   - The absent `get_workflow_projection` dev mock handler means mock coverage has not been implemented yet.

5. Keep the first workflow fixture narrowly scoped.
   - The first workflow fixture target remains `GET /api/repositories/{repositoryId}/workflow` and `WorkflowInstance`.
   - Sibling workflow endpoints must not be included unless separately authorized.

6. Before capturing the workflow fixture, classify every fixture field by architectural role.
   - Each field should be classified as semantic authority, structural metadata, compatibility field, diagnostic field, or derived presentation helper if any exist.
   - This classification should happen before or as part of fixture capture so later authority restoration work can reuse the evidence.

## Next Authorized Sequence

1. Stage, commit, and push this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, begin the primary workflow fixture slice by adding field-role classification for the `WorkflowInstance` fixture candidate before accepting the golden fixture.
