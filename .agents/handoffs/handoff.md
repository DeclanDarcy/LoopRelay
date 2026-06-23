# Handoff

## New State From This Slice

- Continued Milestone 4 by wiring narrative reconstruction into the shell and UI.
- Added Tauri bridge commands:
  - `query_reasoning`
  - `reconstruct_reasoning`
- Added UI query/reconstruction models, API calls, and hooks:
  - `ReasoningQuery`
  - `ReasoningQueryResult`
  - `ReasoningReconstruction`
  - `ReasoningReconstructionEvidence`
  - `useReasoningQuery`
  - `useReasoningReconstruction`
- Added `ReasoningQueryPanel` with category, direction, graph target/manual target, question input, candidate trace counts, evidence counts, diagnostics, and derived-query authority labeling.
- Added `ReasoningReconstructionPanel` with narrative, confidence, trace counts, evidence list, provenance/reference display, diagnostics, and non-authoritative reconstruction labeling.
- Wired `ReasoningTrajectoryTab` and `App` so one query run executes both backend query and reconstruction endpoints.
- Updated the dev Tauri mock to synthesize query results and reconstructions from the mock reasoning graph, events, threads, and relationships.
- Updated `.agents/milestones/m4-narrative-reconstruction.md` to mark UI query/reconstruction exposure complete.
- Rotated previous handoff to `.agents/handoffs/handoff.0017.md`.

## Verification

- `npm run test --prefix src/CommandCenter.UI -- reasoningTrajectory` passes: 1 file, 7 tests.
- `npm run lint --prefix src/CommandCenter.UI` passes.
- `cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml` passes.

## Current Gaps

- Historical state reconstruction from event timelines is still not implemented.
- Persisted reconstruction report generation/listing is still not implemented.
- Category-specific narrative templates remain shallow.
- Backend tests for "what killed this hypothesis?" remain open.
- Full UI test/build matrix has not been rerun in this slice.

## Next Slice

- Implement historical state reconstruction from event timelines in the backend, starting with a focused service shape for point-in-time derived status over hypothesis, alternative, contradiction, and direction event families.
