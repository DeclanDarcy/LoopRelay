# Decisions

## Newly Authorized

- Treat Milestone 2 as architecturally complete: reasoning can capture history across domains, integrate with source-domain transitions, remain non-authoritative, and avoid premature hypothesis, alternative, contradiction, or direction materialization.
- Treat remaining Milestone 2 checklist gaps as follow-on usability/reference-quality work rather than blockers for the milestone's core architectural objective.
- Make reference helper APIs the next highest-value slice because later graph navigation, reconstruction, materialization review, long-horizon validation, and certification depend more on reference quality than raw event volume.
- Implement reference helpers as small factories over the existing `ReasoningReference` shape: kind, id, path, and metadata.
- Do not introduce domain-specific reasoning wrapper concepts such as `DecisionReasoningReference`, `GovernanceReasoningReference`, or `ExecutionReasoningReference` unless a concrete reconstruction need proves that the generic reference shape is insufficient.
- Continue pushing source-domain specificity into references and provenance rather than expanding the core reasoning ontology.
