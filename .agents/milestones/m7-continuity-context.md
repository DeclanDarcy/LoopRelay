## Milestone 7: Continuity and Operational Context Transparency

### Objective

Make operational context explain why information was retained, removed, compressed, assimilated, rejected, modified, contradicted, resolved, or lost.

### Backend

- [x] Extend decision assimilation analysis to expose, for every analyzed decision:
   - [x] taxonomy
   - [x] assimilated or excluded
   - [x] exclusion reason
   - [x] durability
   - [x] resulting operational statement
   - [x] evidence
- [x] Expose taxonomy classification basis:
   - [x] matched evidence
   - [x] matched rules
   - [x] heuristic/fallback status
   - [x] diagnostics
- [x] Expose assimilation limits:
   - [x] total qualifying items
   - [x] assimilated items
   - [x] omitted items
   - [x] limit
   - [x] reason
- [x] Expose consequences with originating decision, reasoning, and operational impact.
- [x] Surface every detected contradiction with decision A, decision B, conflict type, evidence, severity, and resolution guidance.
- [ ] Extend operational evolution reporting:
   - [ ] added
   - [ ] modified
   - [ ] removed
   - [ ] preserved
   - [ ] lost
   - [ ] resolved
   - [ ] previous state
   - [ ] current state
   - [ ] reason
   - [ ] evidence
- [ ] Extend compression output:
   - [ ] retained
   - [ ] compressed
   - [ ] removed
   - [ ] merged
   - [ ] noise removed
   - [ ] duplicate removed
   - [ ] transient removed
   - [ ] rule
   - [ ] evidence
   - [ ] threshold
- [ ] Improve `UnderstandingDiffService` to detect modifications rather than remove/add pairs when item identity, source reference, section, or stable lineage indicates continuity.
- [ ] Add or update semantic change types for modified architecture, modified constraint, modified workflow, modified decision, modified understanding, lost understanding, resolved understanding, duplicate removed, and transient removed.
- [ ] Normalize continuity diagnostics by category:
    - [ ] assimilation
    - [ ] compression
    - [ ] evolution
    - [ ] diff
    - [ ] recovery
    - [ ] classification
    - [ ] contradictions
    - [ ] lost understanding
    - [ ] resolved understanding

### UI

- [x] Extend operational-context and continuity TypeScript types.
- [ ] Add panels:
   - [x] `OperationalContextAssimilationPanel`
   - [x] `OperationalContextTaxonomyPanel`
   - [x] `OperationalContextAssimilationLimitPanel`
   - [x] `OperationalContextConsequencePanel`
   - [x] `OperationalContextContradictionPanel`
   - [ ] `OperationalContextEvolutionTimeline`
   - [ ] `OperationalContextCompressionExplanation`
   - [ ] `ContinuityDiagnosticsGroupedPanel`
- [ ] Update `OperationalContextProposalComparison` and `OperationalContextSemanticChangeList` to display modifications as modifications, not separate remove/add entries.
- [x] Show omitted assimilation items and silent truncation as visible facts.
- [ ] Show compression warnings with specific item reasons and evidence.

### Tests

- [x] Backend tests for assimilation inclusion/exclusion reasons.
- [x] Backend tests for taxonomy basis and heuristic fallback.
- [x] Backend tests for assimilation limits and omitted items.
- [x] Backend tests for all contradiction detection.
- [ ] Backend tests for identity-aware semantic diff modifications.
- [ ] Backend tests for compression reason categories.
- [x] UI tests for the new panels and modification rendering.

### Exit Criteria

- [ ] Every analyzed decision explains why it was assimilated or rejected.
- [ ] Taxonomy classifications expose their basis.
- [ ] Assimilation limits and omitted items are visible.
- [ ] Consequences stay linked to originating decisions.
- [x] All contradictions are explorable.
- [ ] Operational evolution distinguishes added, modified, removed, preserved, lost, and resolved understanding.
- [ ] Compression explains item-level outcomes.
- [ ] Semantic diff preserves identity and lineage for modifications.
