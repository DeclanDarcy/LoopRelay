# Milestone 10: Automated Decision Generation Certification

## Goal

certify that automated decision generation replaces human decision production with human governance.

## Work

- [x] Add `DecisionGenerationCertificationResult`:
  - [x] generation certified
  - [x] governance certified
  - [x] throughput certified
  - [x] quality certified
  - [x] consumption certified
  - [x] workflow replacement certified
  - [x] findings
  - [x] failures
- [x] Add `DecisionGenerationCertificationReport`.
- [x] Add `IDecisionGenerationCertificationService`.
- [x] Add backend generation-certification endpoints:
  - [x] current advisory report
  - [x] persisted certification run
  - [x] persisted report history
- [x] Add Tauri generation-certification bridge commands:
  - [x] current advisory report
  - [x] persisted certification run
  - [x] persisted report history
- [x] Add focused UI generation-certification exposure:
  - [x] decision API/types/hooks
  - [x] advisory certification panel
  - [x] persisted report history
  - [x] human authoring burden evidence
  - [x] quality evidence
- [x] Evaluate certification categories:
  - [x] generation capability
  - [x] governance compatibility
  - [x] throughput improvement
  - [x] human authoring burden reduction
  - [x] decision quality
  - [x] execution influence
  - [x] workflow replacement alignment
- [ ] Certification requirements:
  - [x] candidates discovered automatically
  - [x] multiple options generated
  - [x] tradeoffs generated
  - [x] recommendations generated or responsibly withheld
  - [x] packages generated and persisted
  - [x] humans review/refine/resolve
  - [x] humans do not author most final decision content
  - [x] history preserved
  - [x] quality assessments available
  - [x] human authoring burden assessments available
  - [x] execution consumes accepted resolved decisions
  - [x] influence is traceable
- [ ] Certification failure conditions:
  - [x] hardcoded recommendations
  - [x] single-option generation without justification
  - [x] missing evidence
  - [x] resolution bypass
  - [x] humans rewriting most generated packages
  - [x] generated decisions frequently classified as `FullRewrite` or `GenerationBypassed`
  - [x] recommendations ignored repeatedly as advisory quality signals, not automatic certification failures
  - [x] execution projection absent
  - [x] influence not traceable
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

- [x] Certification passes for fixtures that exercise discovery through execution influence.
- [x] Certification fails when recommendation is hardcoded or order-based.
- [x] Certification fails when options are missing.
- [x] Certification fails when governance resolution is bypassed.
- [x] Certification fails when no quality evidence exists after resolved generated decisions.
- [x] Certification fails when most generated decisions require `FullRewrite`.
- [x] Certification fails when manual decisions bypass generation more often than generated decisions reach resolution.
- [x] Certification fails when execution influence cannot be traced.
- [x] Certification report persists and reloads after restart.

## Exit Criteria

- [ ] Certification can answer with evidence whether the system primarily generates decisions, humans primarily review/refine/resolve, humans are no longer the primary authors, execution is directed by resolved decisions, and workflow burden is reduced.
