# Decisions: 2026-06-26 Workflow Fixture Capture Authorization

These decisions capture only newly authorized direction from the user response following Slice 0027.

## Authorized Decisions

1. Accept the workflow fixture field-classification gate as the required control point before the first workflow fixture.
   - The workflow fixture must remain an observation of an already-understood contract.
   - The fixture must not define unresolved ownership or semantic decisions.

2. Keep flattened workflow statuses and booleans compatibility-sensitive.
   - Flattened fields must be treated as backend-derived compatibility surfaces when they duplicate richer lifecycle or nested projection state.
   - The Oracle must not freeze UI convenience fields as independent canonical semantics.

3. Preserve explicit `decisionSession` serialization semantics.
   - `decisionSession = null` and `decisionSession = object` are distinct serialized contract states.
   - Omission is not equivalent to explicit null for the workflow Oracle fixture path.

4. Keep the first workflow fixture scoped to the primary workflow endpoint.
   - The fixture target remains `GET /api/repositories/{repositoryId}/workflow`.
   - The backend contract identity remains `WorkflowInstance`.
   - Sibling workflow endpoints remain excluded unless separately authorized.

5. Keep the next workflow fixture slice intentionally minimal.
   - Capture representative backend JSON.
   - Produce the golden fixture.
   - Add backend serialization comparison.
   - Stop before layering consumer verification, artifact freshness, or request-boundary verification.

6. Add an explicit representative field-category coverage checklist to workflow fixture review evidence.
   - The checklist should verify coverage for lifecycle enum, compatibility boolean, explicit null, nested object, array, timeline, transition, gate, diagnostic, eligibility, and `decisionSession = null`.
   - `decisionSession = object` may be planned for a future fixture or variant if it does not fit the first minimal fixture.

## Next Authorized Sequence

1. Stage, commit, and push the current Slice 0027 evidence and this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, capture the primary `WorkflowInstance` golden fixture and backend serialization comparison only, with fixture review evidence that includes the representative field-category coverage checklist.
