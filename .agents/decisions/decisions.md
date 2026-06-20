# Decisions

## Newly Authorized Decisions

- M5 work should remain within workspace experience scope and avoid introducing new backend concepts unless a later gap requires it.
- Current M5 implementation direction is accepted as aligned with:
  - projection consumption
  - repository-scoped selection state
  - workspace UX
  - state reconciliation
- Artifact selection memory is authorized as repository-scoped UI state, not backend or repository state.
- Selection reconciliation after repository state changes is accepted as high-value M5 behavior.
- Empty-state cleanup is required so editor and preview state never display stale artifact content from another repository or prior projection.
- The projection authority invariant remains active for M5:
  - filesystem state flows into `ArtifactInventory`
  - `ArtifactInventory` flows into `RepositoryWorkspaceProjection`
  - React consumes the projection rather than deriving repository state
- The next M5 slice is authorized to focus on rendered workspace certification before adding more features.
- M5 certification should explicitly cover:
  - repository switching with artifact selection restoration
  - artifact edit/save persistence across repository switches
  - external filesystem mutation followed by refresh
  - handoff and decision rotation selection reconciliation
  - empty artifact states without stale preview/editor content
  - removing a selected repository while another repository remains
