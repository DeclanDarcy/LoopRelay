# Decisions: 2026-06-26 Slice 0025 Repeatability Baseline

These decisions capture only newly authorized direction from the response accepting Slice 0025 as the repeatability baseline before workflow projection coverage.

## Authorized Decisions

1. Treat the Oracle lifecycle repeatability claim as explicitly proven at two-pilot scope.
   - The demonstrated architectural claim is stronger than having two fixtures.
   - Repository dashboard and repository workspace proved that field inventory, golden fixture comparison, drift classification, consumer verification, artifact freshness, request-boundary verification, and local certification can repeat without Oracle architectural redesign.
   - The absence of architectural redesign across the second pilot is itself accepted as architectural evidence.

2. Preserve the narrow certification posture.
   - Repository dashboard and repository workspace remain locally certified pilots.
   - The Oracle capability is not globally certified.
   - Repeatability evidence must not be conflated with broad contract coverage or milestone-wide certification.

3. Treat the two certified pilots as the reference Oracle implementation before workflow begins.
   - Workflow projection should be evaluated against the existing reference lifecycle rather than evolving the Oracle immediately.
   - The reference implementation consists of the dashboard and workspace inventory, fixture, drift, consumer-verification, artifact-freshness, request-boundary, and certification mechanisms.

4. Start workflow projection coverage with field-level inventory only.
   - No workflow fixture is authorized until ownership, producers, consumers, compatibility obligations, request boundaries, and semantic lifecycle fields are mapped.
   - Workflow is selected as the next stress test because it adds richer lifecycle semantics, deeper object graphs, more compatibility consumers, and more request/response interactions.

5. Classify workflow-discovered gaps before changing Oracle architecture.
   - Contract-specific complexity should be handled by inventory and contract-family evidence.
   - Framework implementation refinement may improve existing mechanisms without changing the Oracle design.
   - True architectural gaps require explicit governance before changing the Oracle design.

6. Keep documentation-only repeatability slices tied to prior verifier evidence.
   - Not rerunning tests for Slice 0025 is accepted because it only updated documentation and evidence.
   - Slice 0025 may rely on Slice 0024's focused Oracle and full backend verification results.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0025 and this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, begin workflow projection Oracle coverage with gated field inventory only.
