# Decisions: 2026-06-26 Workflow Artifact Freshness and Certification Authorization

These decisions capture only newly authorized direction from the user response following Slice 0031.

## Authorized Decisions

1. Accept Slice 0031 as completing the mechanism set for the initial workflow Oracle pilot.
   - Workflow now has fixture comparison, TypeScript consumer verification, request-boundary verification, and artifact freshness verification.
   - The remaining workflow questions are certification sufficiency questions, not missing-mechanism questions.

2. Treat artifact freshness transfer as the key architectural result of Slice 0031.
   - Repository dashboard, repository workspace, and workflow instance now use the same manifest-driven freshness architecture.
   - No workflow-specific freshness model should be introduced for the current pilot.

3. Preserve the workflow contract authority split.
   - `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.golden.json` remains Oracle truth.
   - `src/CommandCenter.UI/src/types/workflow.ts` remains a verified downstream contract artifact, not contract authority.

4. Treat missing dev mock workflow coverage as an accepted certification gap for the initial workflow pilot.
   - Unless the development mock is expected to support workflow today, the absence affects coverage breadth rather than Oracle mechanism validity.
   - Record the gap during certification instead of blocking local workflow certification on it.

5. Treat populated `decisionSession` workflow coverage as an accepted fixture variant gap.
   - The current `decisionSession: null` fixture pins a valid serialization contract.
   - A populated variant should remain planned follow-on coverage, not a prerequisite for certifying the initial workflow pilot architecture.

6. Proceed to local workflow Oracle certification next.
   - The certification slice should verify fixture comparison, TypeScript consumer verification, request-boundary verification, artifact freshness, the full backend test suite, and explicit gap review.
   - The certification record should state that dev mock workflow coverage and populated `decisionSession` coverage are intentionally absent and do not invalidate the demonstrated Oracle mechanisms.

7. If workflow certification passes, frame the evidence as Oracle repeatability across independent contract families with increasing semantic complexity.
   - The architectural claim should move beyond repository-only coverage.
   - The workflow pilot strengthens Milestone 0.2 by demonstrating the same Oracle architecture on a richer semantic contract family.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0031 plus this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, run local workflow Oracle certification with explicit accepted-gap review.
