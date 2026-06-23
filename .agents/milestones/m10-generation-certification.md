# Milestone 10: Automated Decision Generation Certification

## Goal

certify that automated decision generation replaces human decision production with human governance.

## Work

- [ ] Add `DecisionGenerationCertificationResult`:
  - [ ] generation certified
  - [ ] governance certified
  - [ ] throughput certified
  - [ ] quality certified
  - [ ] consumption certified
  - [ ] workflow replacement certified
  - [ ] findings
  - [ ] failures
- [ ] Add `DecisionGenerationCertificationReport`.
- [ ] Add `IDecisionGenerationCertificationService`.
- [ ] Evaluate certification categories:
  - [ ] generation capability
  - [ ] governance compatibility
  - [ ] throughput improvement
  - [ ] human authoring burden reduction
  - [ ] decision quality
  - [ ] execution influence
  - [ ] workflow replacement alignment
- [ ] Certification requirements:
  - [ ] candidates discovered automatically
  - [ ] multiple options generated
  - [ ] tradeoffs generated
  - [ ] recommendations generated or responsibly withheld
  - [ ] packages generated and persisted
  - [ ] humans review/refine/resolve
  - [ ] humans do not author most final decision content
  - [ ] history preserved
  - [ ] quality assessments available
  - [ ] human authoring burden assessments available
  - [ ] execution consumes accepted resolved decisions
  - [ ] influence is traceable
- [ ] Certification failure conditions:
  - [ ] hardcoded recommendations
  - [ ] single-option generation without justification
  - [ ] missing evidence
  - [ ] resolution bypass
  - [ ] humans rewriting most generated packages
  - [ ] generated decisions frequently classified as `FullRewrite` or `GenerationBypassed`
  - [ ] recommendations ignored repeatedly
  - [ ] execution projection absent
  - [ ] influence not traceable
- [ ] Add certification scenarios:
  - [ ] architectural fork
  - [ ] workflow priority decision
  - [ ] contradiction with withheld recommendation
  - [ ] refinement after human changes assumptions
  - [ ] end-to-end repository lifecycle
- [ ] Add certification reports:
  - [ ] repository report
  - [ ] workflow report
  - [ ] human authoring burden report
  - [ ] executive report that directly answers whether human decision production has been replaced by system generation and human governance

## Tests

- [ ] Certification passes for fixtures that exercise discovery through execution influence.
- [ ] Certification fails when recommendation is hardcoded or order-based.
- [ ] Certification fails when options are missing.
- [ ] Certification fails when governance resolution is bypassed.
- [ ] Certification fails when no quality evidence exists after resolved generated decisions.
- [ ] Certification fails when most generated decisions require `FullRewrite`.
- [ ] Certification fails when manual decisions bypass generation more often than generated decisions reach resolution.
- [ ] Certification fails when execution influence cannot be traced.
- [ ] Certification report persists and reloads after restart.

## Exit Criteria

- [ ] Certification can answer with evidence whether the system primarily generates decisions, humans primarily review/refine/resolve, humans are no longer the primary authors, execution is directed by resolved decisions, and workflow burden is reduced.
