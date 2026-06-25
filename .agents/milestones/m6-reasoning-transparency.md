## Milestone 6: Reasoning Transparency

### Objective

Make reasoning conclusions explain their provenance, confidence, thresholds, reconstruction scope, capture mode, authority boundaries, lifecycle risk, and diagnostics.

### Backend

- [ ] Build a reasoning transparency inventory for:
   - [x] materialization recommendations
   - [x] reconstruction confidence
   - [x] reconstruction direction
   - [x] capture provenance
   - [x] inferred reasoning
   - [x] skipped or deduplicated captures
   - [x] authority-boundary blocks
   - [x] taxonomy lifecycle risk
   - [x] diagnostics
- [ ] Extend materialization review models to include:
   - [x] literal recommendation enum
   - [x] failed scenario count
   - [x] repeated workflow count
   - [x] thresholds
   - [x] elevated risk signals
   - [x] branch reason
   - [x] diagnostics
- [ ] Extend reconstruction models to include:
   - [x] confidence rationale
   - [x] event evidence present
   - [x] relationship evidence present
   - [x] trace diagnostics present
   - [x] missing evidence
   - [x] why confidence was not higher
   - [x] forward or backward direction
   - [x] target and source reference
   - [x] historical cutoff
   - [x] reachable and unreachable evidence where known
- [ ] Extend reasoning event or projection models to distinguish capture modes:
   - [x] Manual
   - [x] Assisted
   - [x] Inferred
- [x] For inferred capture, expose source transition, source artifact, capture reason, captured by, and source timestamp.
- [x] For skipped or deduplicated capture, expose skip reason, existing event id, and duplicate signal where relevant.
- [x] Replace plain boundary errors with structured boundary error responses:
   - [x] boundary rule
   - [x] owning domain
   - [x] rejected assertion
   - [x] allowed alternative
   - [x] diagnostic detail
- [ ] Extend taxonomy lifecycle risk findings with:
   - [x] family
   - [x] event type count
   - [x] event type threshold
   - [x] terminal event type present
   - [x] terminal event types
   - [x] reason risk was or was not flagged
- [x] Normalize reasoning diagnostics by category:
   - [x] evidence
   - [x] confidence
   - [x] materialization
   - [x] reconstruction
   - [x] capture
   - [x] authority boundary
   - [x] lifecycle risk
   - [x] validation

### UI

- [x] Update reasoning TypeScript types and API responses.
- [x] Update `ReasoningMaterializationReviewPanel` to render literal recommendations and threshold basis.
- [x] Update `ReasoningReconstructionPanel`, `ReasoningQueryPanel`, and trace panels to show confidence rationale, evidence branches, missing evidence, direction, scope, and historical cutoff.
- [x] Update `ReasoningEventFeed` with capture provenance badges and inferred capture details.
- [x] Add authority boundary notices that identify the owning domain and allowed alternative.
- [x] Update taxonomy and materialization review rendering to show lifecycle-risk rules and thresholds.
- [x] Add a grouped reasoning diagnostics component.

### Tests

- [x] Backend tests for materialization threshold branches.
- [x] Backend tests for confidence rationale branches.
- [x] Backend tests for forward/backward reconstruction scope.
- [x] Backend tests for manual, assisted, and inferred capture.
- [x] Backend tests for boundary violation explanations.
- [x] Backend tests for lifecycle risk thresholds.
- [x] UI tests for rendering each explanation branch.
   - [x] Grouped reasoning diagnostics rendering.
   - [x] Capture diagnostic group rendering.

### Exit Criteria

- [x] Users can understand why materialization was recommended or not.
- [x] Confidence labels explain their evidence and missing evidence.
- [x] Reconstruction scope and direction are explicit.
- [x] Authored, assisted, and inferred reasoning are distinguishable.
- [x] Boundary violations explain the owning rule and allowed alternative.
- [x] Lifecycle risk findings show their rule basis.
- [x] Reasoning diagnostics are semantically grouped and actionable.
