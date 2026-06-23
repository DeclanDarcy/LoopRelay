# Decisions

## Newly Authorized

- Close Milestone 9 as complete.
- Treat the implemented Tier 0 backend validation as sufficient evidence that generated decisions can influence execution while preserving human governance.
- Preserve the invariant that generated recommendations become execution guidance only after explicit accepted human resolution.
- Start Milestone 10 as workflow evaluation and certification, not new workflow mutation.
- Open Milestone 10 with `DecisionGenerationCertificationResult`, `DecisionGenerationCertificationReport`, and `IDecisionGenerationCertificationService`.
- Focus the first M10 certification slice on generation capability, governance compatibility, execution consumption, and human authoring burden.
- Make the first certification question: did a generated decision reach execution influence through human resolution?

## Not Authorized

- Do not let Milestone 10 introduce new workflow authority or mutation behavior.
- Certification should observe, measure, and certify downstream workflow evidence only.
