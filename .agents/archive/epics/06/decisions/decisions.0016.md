# Decisions

## Newly Authorized

- Close Milestone 3 as complete.
- Treat the missing `cargo-fmt.exe` component as a repository hygiene item, not a Milestone 3 architectural blocker.
- Start Milestone 4 from the boundary that narrative work is `Narrative Reconstruction`, not open-ended narrative generation.
- Adopt `Narrative = Projection` for Milestone 4, matching the Milestone 3 `Graph = Projection` boundary.
- Keep narrative reconstructions derived, disposable, rebuildable, and non-authoritative.
- Do not store narrative, explanation, summary, or reasoning story artifacts as reasoning authority.
- Use `ReasoningTrace` as the unit of reconstruction for Milestone 4.
- Do not use `ReasoningThread` as the primary reconstruction unit, because that risks turning threads into workflow containers.
- Do not use repository-wide narrative as the primary reconstruction unit, because it becomes subjective and weakens deterministic reconstruction.
- Preserve reconstruction inputs as `ReasoningTrace`, relationships, evidence, references, and provenance.
- Preserve existing M4 authority boundaries: reconstruction may explain reasoning evolution, but it may not approve decisions, mutate operational context, create execution directives, or become a competing source of truth.

