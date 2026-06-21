# Decisions

## Newly Authorized

- Treat the artifact editor extraction as valid M0.5 work because `ArtifactMetadata` and `ArtifactMarkdownPreview` are purely representational `props -> render` components.
- Keep save, rotate, dirty tracking, draft ownership, textarea editing, loading disablement, and mutation gating in `App.tsx` because moving them now would fragment workflow authority.
- Keep the `.gitignore` exception for `src/CommandCenter.UI/src/features/artifacts/` narrow. It should allow frontend artifact UI source while preserving the original ignore rule's intent for artifact outputs.
- Approach operational-context proposal review more cautiously than the artifact editor because many proposal displays exist to support workflow decisions.
- For operational-context proposal review candidates, use this audit question: if accept, reject, promote, and generate disappear, does the section still have standalone meaning?
- Treat proposal metadata, compression summary display, and review status display as likely good presentation-only extraction candidates if they only render already-derived projection data.
- Treat semantic change display as medium risk. Extract it only if it renders backend-provided before/after/delta facts without adding interpretation logic.
- Keep proposal loading, generation, review notes, draft ownership, accept, reject, promote, and comparison coordination in `App.tsx` unless a focused audit proves a narrower presentation boundary.
- Expect late M0.5 slices to move fewer lines and spend more effort auditing responsibility boundaries; retaining workflow-heavy regions in `App.tsx` is acceptable when they still own draft state, workflow coordination, or mutation authority.
