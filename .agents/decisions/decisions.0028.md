# Decisions: 2026-06-26 Checkpoint Before Workflow Fixture

These decisions capture only newly authorized direction from the user response that followed Slice 0026.

## Authorized Decisions

1. Treat the current user response as a checkpoint boundary.
   - Rotate the existing active decisions file before new execution begins.
   - Create a fresh active decisions file containing only newly authorized checkpoint direction.
   - Stage, commit, and push the checkpoint, then stop executing.

2. Do not begin the workflow fixture implementation in this checkpoint.
   - Milestone 0.2 remains active and uncertified.
   - The next executable slice remains the primary workflow projection fixture slice.

3. Preserve the next workflow fixture slice scope already authorized by `decisions.0027.md`.
   - The first fixture target remains `GET /api/repositories/{repositoryId}/workflow` and backend `WorkflowInstance`.
   - Before accepting the golden fixture, classify every fixture field by architectural role.
   - Sibling workflow endpoint fixtures remain out of scope unless separately authorized.

## Next Authorized Sequence

1. Stage, commit, and push this checkpoint.
2. Stop executing after the push.
3. In the next work slice, begin field-role classification for the `WorkflowInstance` fixture candidate before capturing the golden fixture.
