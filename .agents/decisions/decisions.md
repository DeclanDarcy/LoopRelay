# Decisions

## Newly Authorized

- Use a stricter M0.5 extraction rule after M0.6: candidates must receive required data via props, render with no backend access, and function meaningfully as Storybook-style presentation components.
- Treat `projection data -> render` as the highest-confidence extraction pattern.
- Reject extraction candidates that transition from projection data into workflow meaning, workflow action, workflow coordination, command dispatch, readiness decisions, or authority decisions.
- Consider repository summary display and tightly scoped operational-context display regions as the safest remaining M0.5 candidates.
- Exclude accept/reject/promote/edit coordination and draft ownership from any operational-context display extraction.
- Continue presuming commit preparation, commit readiness, push readiness, execution launch, proposal review actions, handoff acceptance, artifact mutation, and promotion workflows are authority boundaries until proven otherwise.
- Treat remaining `App.tsx` workflow coordination, authority enforcement, draft orchestration, and projection composition as intentional ownership once clean presentation regions are harvested.
