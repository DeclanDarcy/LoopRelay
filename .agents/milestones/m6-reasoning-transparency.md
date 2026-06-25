## Milestone 6: Reasoning Transparency

### Objective

Make reasoning conclusions explain their provenance, confidence, thresholds, reconstruction scope, capture mode, authority boundaries, lifecycle risk, and diagnostics.

### Backend

- [ ] Build a reasoning transparency inventory for:
   - [ ] materialization recommendations
   - [ ] reconstruction confidence
   - [ ] reconstruction direction
   - [ ] capture provenance
   - [ ] inferred reasoning
   - [ ] skipped or deduplicated captures
   - [ ] authority-boundary blocks
   - [ ] taxonomy lifecycle risk
   - [ ] diagnostics
- [ ] Extend materialization review models to include:
   - [ ] literal recommendation enum
   - [ ] failed scenario count
   - [ ] repeated workflow count
   - [ ] thresholds
   - [ ] elevated risk signals
   - [ ] branch reason
   - [ ] diagnostics
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
   - [ ] Manual
   - [ ] Assisted
   - [ ] Inferred
- [ ] For inferred capture, expose source transition, source artifact, capture reason, captured by, and source timestamp.
- [ ] For skipped or deduplicated capture, expose skip reason, existing event id, and duplicate signal where relevant.
- [ ] Replace plain boundary errors with structured boundary error responses:
   - [ ] boundary rule
   - [ ] owning domain
   - [ ] rejected assertion
   - [ ] allowed alternative
   - [ ] diagnostic detail
- [ ] Extend taxonomy lifecycle risk findings with:
   - [ ] family
   - [ ] event type count
   - [ ] event type threshold
   - [ ] terminal event type present
   - [ ] terminal event types
   - [ ] reason risk was or was not flagged
- [ ] Normalize reasoning diagnostics by category:
   - [ ] evidence
   - [ ] confidence
   - [ ] materialization
   - [ ] reconstruction
   - [ ] capture
   - [ ] authority boundary
   - [ ] lifecycle risk
   - [ ] validation

### UI

- [ ] Update reasoning TypeScript types and API responses.
- [ ] Update `ReasoningMaterializationReviewPanel` to render literal recommendations and threshold basis.
- [ ] Update `ReasoningReconstructionPanel`, `ReasoningQueryPanel`, and trace panels to show confidence rationale, evidence branches, missing evidence, direction, scope, and historical cutoff.
- [ ] Update `ReasoningEventFeed` with capture provenance badges and inferred capture details.
- [ ] Add authority boundary notices that identify the owning domain and allowed alternative.
- [ ] Update taxonomy and materialization review rendering to show lifecycle-risk rules and thresholds.
- [ ] Add a grouped reasoning diagnostics component.

### Tests

- [ ] Backend tests for materialization threshold branches.
- [x] Backend tests for confidence rationale branches.
- [x] Backend tests for forward/backward reconstruction scope.
- [ ] Backend tests for manual, assisted, and inferred capture.
- [ ] Backend tests for boundary violation explanations.
- [ ] Backend tests for lifecycle risk thresholds.
- [ ] UI tests for rendering each explanation branch.

### Exit Criteria

- [ ] Users can understand why materialization was recommended or not.
- [ ] Confidence labels explain their evidence and missing evidence.
- [ ] Reconstruction scope and direction are explicit.
- [ ] Authored, assisted, and inferred reasoning are distinguishable.
- [ ] Boundary violations explain the owning rule and allowed alternative.
- [ ] Lifecycle risk findings show their rule basis.
- [ ] Reasoning diagnostics are semantically grouped and actionable.
