# Decisions: 2026-06-26 Slice 0024 Oracle Repeatability Checkpoint

These decisions capture only newly authorized direction from the response accepting Slice 0024 as a meaningful repeatability milestone.

## Authorized Decisions

1. Treat Slice 0024 as evidence that the Contract Oracle lifecycle repeats.
   - Repository dashboard and repository workspace are now independently locally certified pilot ecosystems.
   - The architectural significance is reuse of the same lifecycle across two contract families, not only successful workspace certification.
   - The lifecycle evidence includes field inventory, serialization observations, golden fixture, consumer verification, artifact freshness, request-boundary verification, and local certification.

2. Treat Milestone 0.2 as moving from architecture discovery toward coverage expansion.
   - The Oracle architecture, mechanism decomposition, consumer verification model, artifact freshness model, request-boundary model, local certification model, and two-family repeatability are considered proven at pilot scope.
   - Remaining Milestone 0.2 work is broader contract coverage, more complex semantic projections, and eventual milestone-wide certification.
   - This does not authorize global Milestone 0.2 certification.

3. Preserve narrow request-boundary certification claims.
   - Repository workspace certification covers only the primary GET path.
   - Refresh, artifact rotation, and similar request paths remain future coverage targets.
   - Narrow certification language is required to avoid overstating pilot completeness.

4. Select workflow projection as the preferred next contract-family expansion.
   - Workflow projection should stress the Oracle through deeper nesting, richer lifecycle state, broader consumers, and stronger compatibility obligations.
   - The goal is to determine whether the existing Oracle mechanisms scale with contract-specific data changes rather than architectural redesign.

5. Add a cross-pilot repeatability evidence artifact before starting workflow.
   - The next slice should summarize dashboard and workspace evidence side by side.
   - The summary should identify which mechanisms were reused and whether framework changes were required.
   - This artifact becomes the baseline before introducing the more complex workflow contract family.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0024 and this decision checkpoint.
2. Stop executing after the checkpoint.
3. In the next work slice, create the cross-pilot Oracle repeatability evidence artifact before beginning workflow projection coverage.
