# Decisions

## Newly Authorized

- Treat the completed work as the correct Milestone 4 opening slice.
- Preserve `ReasoningTrace -> Reconstruction` as the M4 reconstruction boundary.
- Continue rejecting `Repository -> Reconstruction` and `Thread -> Reconstruction` as primary reconstruction paths.
- Keep reconstructions response-only for now.
- Do not introduce persisted narrative, reasoning story, historical summary, explanation record, graph, hypothesis, alternative, contradiction, or direction artifacts.
- Keep query and reconstruction as separate concerns: query discovers trace candidates, reconstruction explains trace candidates.
- Avoid collapsing query into direct narrative generation.
- Keep category-specific narration shallow until stronger evidence proves richer templates are needed.
- Narration may consume event classifications, but must not materialize classified concepts into new ontology or lifecycle state.
- Treat Narrative Authority as the main Milestone 4 risk.
- Model and present `ReasoningReconstruction` as a derived explanation, not historical fact.
- Execute the next slice in this order:
  - Add Tauri bridge commands `query_reasoning` and `reconstruct_reasoning`.
  - Add thin UI API/types/hooks for query and reconstruction.
  - Add `ReasoningQueryPanel` focused on question, candidate traces, evidence counts, and relationship counts.
  - Add `ReasoningReconstructionPanel` focused on reconstruction, supporting events, references, and provenance.
- Do not add reconstruction caching or local persistence in the next slice.
