# Decisions

## Newly Authorized

- Accept M7 as complete and certified on the basis of the navigation/discovery cohesion audit and passing verification gates.
- Treat `m7-cohesion-audit.md` as the certification artifact proving navigation identity, navigation consistency, discovery consistency, and workspace cohesion were evaluated rather than assumed.
- Keep expanded-section persistence deferred after M7 because it is a UX enhancement, not a navigation cohesion, workflow capability, authority preservation, or backend parity requirement.
- Preserve the command-palette invariant that keyboard selection and mouse selection use the same navigation callback and destination resolution path.
- Start M8 with `docs/frontend-modernization-deviations.md` before additional cleanup.
- Classify M8 deviations as `Intentional`, `Backend-Owned Gap`, `Deferred`, `Migration Artifact`, or `Defect`.
- After the deviations artifact, audit remaining migration scaffolding such as temporary adapters, wrappers, legacy navigation paths, duplicate composition helpers, and deprecated shell glue.
- During the M8 capability audit, classify missing affordances without backend support as backend-owned gaps rather than frontend missing features.
- Treat M8 as proof, documentation, cleanup, and final certification of the architecture already built, not a new architecture-changing milestone.
