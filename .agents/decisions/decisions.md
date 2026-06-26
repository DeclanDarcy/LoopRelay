# Decisions: 2026-06-26 Workflow Field Classification Gate

These decisions capture only newly authorized direction from the user response following checkpoint commit `d02be2b4`.

## Authorized Decisions

1. Treat the workflow contract family as inventory-only until the fixture slice executes.
   - Repository Dashboard Oracle pilot remains locally certified.
   - Repository Workspace Oracle pilot remains locally certified.
   - Workflow coverage remains active in Milestone 0.2 but has not progressed beyond inventory.

2. Make workflow fixture field classification an explicit review artifact.
   - The classification belongs outside the golden fixture itself.
   - The fixture must not be accepted until each `WorkflowInstance` fixture candidate field has been classified.

3. Record the following minimum attributes for each classified workflow fixture field.
   - Field name.
   - Semantic owner.
   - Role, such as lifecycle, diagnostic, eligibility, timeline, transition, metadata, or compatibility.
   - Required or optional serialization expectation.
   - Nullability and explicit serialization behavior.
   - Known downstream consumer set.
   - Compatibility obligation for consumers that depend on the current form.

4. Keep the first workflow fixture restricted to the primary workflow projection.
   - The only authorized fixture target is `GET /api/repositories/{repositoryId}/workflow`.
   - The backend contract identity is `WorkflowInstance`.
   - Sibling workflow endpoints remain excluded unless separately authorized.

5. Prefer applying the existing Oracle mechanism before changing the mechanism set.
   - Workflow should validate whether the current Oracle architecture generalizes to a richer contract family.
   - Introduce a new Oracle mechanism only if workflow exposes a genuine architectural gap that cannot fit the existing lifecycle.

## Next Authorized Sequence

1. Stage, commit, and push this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, create the workflow field-classification review artifact before capturing or accepting the primary workflow golden fixture.
