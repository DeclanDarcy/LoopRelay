# Decisions

## Newly Authorized

- Treat the completed markdown extraction as valid Workstream 0.5 work because `renderMarkdown` is presentation, not projection, workflow, navigation, or draft authority.
- Treat the remainder of M0 as organizational hardening rather than architectural correction.
- Continue respecting the established M0 authority boundaries for projection authority, navigation authority, draft boundaries, transport authority, certification artifacts, and workflow deferrals.
- Prefer `workflow-step display mapping` for the next Workstream 0.5 slice over execution event merge helpers.
- Extract only pure presentation, pure formatting, and pure mapping during the remainder of Workstream 0.5.
- Do not extract meaning during Workstream 0.5.
- Before extracting event helpers, prove they are purely mechanical and would still exist if execution workflow semantics changed.
- Mechanical event helpers may include deduplicating sequence ids, sorting by sequence, or replacing duplicate sequence entries.
- Workflow-semantic event logic must remain unextracted for now, including determining execution phase, completion, readiness, or current milestone.
- Perform a quick inventory of remaining large `App.tsx` blocks and classify them as Pure Presentation, Pure Formatting, Pure Mapping, Workflow Composition, or Workflow Authority before choosing further extractions.

## Next Authorized Slice

Inventory remaining large `App.tsx` blocks by authority category, then extract workflow-step display mapping only if it is pure presentation/mapping and characterization protects current behavior.
