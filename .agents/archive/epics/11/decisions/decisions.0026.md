# Decisions: 2026-06-26 Slice 0026 Workflow Inventory

These decisions capture only newly authorized direction from the request to continue the current milestone after Slice 0025 and then rotate handoff/decisions and publish the slice.

## Authorized Decisions

1. Continue Milestone 0.2 by starting workflow projection Oracle coverage.
   - The workflow projection is the next coverage target after repository dashboard and repository workspace repeatability evidence.
   - The initial workflow slice is inventory-only and must not add a golden fixture before ownership, producers, consumers, compatibility obligations, request boundaries, and semantic lifecycle fields are mapped.

2. Keep workflow primary projection scope narrow.
   - The primary workflow fixture candidate is `WorkflowInstance` from `GET /api/repositories/{repositoryId}/workflow`.
   - Sibling workflow endpoint contracts for diagnostics, timeline, history, transitions, gates, recovery, execution, handoff, decisions, operational context, Git, continuation, preparation, health, reports, and certification are not certified by the primary projection inventory.

3. Treat workflow inventory as protection, not certification.
   - Workflow projection has field inventory and fixture planning evidence only.
   - No workflow fixture, consumer verifier, artifact freshness manifest, request-boundary verifier, or local certification is authorized by this slice.

4. Record workflow consumer gaps before fixture work.
   - Rust shell workflow response pass-through through `serde_json::Value` is relevant evidence for passive transport.
   - Manual TypeScript workflow types remain the primary parallel response-shape representation.
   - The absent dev Tauri mock `get_workflow_projection` handler is a coverage gap, not a verified mock consumer.

5. Publish the completed slice and stop executing.
   - Rotate active handoff and decisions files.
   - Stage only the intended Slice 0026 files plus the rotated Slice 0025 handoff and decision files.
   - Commit and push, then stop further milestone execution.

## Next Authorized Sequence

1. Add the primary workflow projection golden fixture for `WorkflowInstance` only.
2. Use representative data that covers lifecycle enums, explicit nulls, non-empty and empty arrays, transitions, gates, timeline, completion, diagnostics, eligibility booleans, and nullable or populated `decisionSession`.
3. Do not add sibling workflow endpoint fixtures unless separately authorized.
