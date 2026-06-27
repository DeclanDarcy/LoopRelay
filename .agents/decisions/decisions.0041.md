# Decisions: 2026-06-26 M0.3 Ownership And Severity Direction

These decisions capture only newly authorized direction from the user response following Slice 0038.

## Authorized Decisions

1. Accept the M0.3 regression taxonomy slice as correctly scoped.
   - Regression mechanism selection should remain governed by taxonomy rather than individual engineering judgment.
   - The taxonomy is the policy layer between architectural intent and regression implementation.
   - The framework progression remains: architecture, invariant catalog, regression taxonomy, regression framework, executable regressions.

2. Keep `Preferred mechanism` and `Minimum acceptable mechanism` separate.
   - This distinction is an accepted way to express architectural maturity honestly.
   - Weak temporary protections may exist, but they must not be treated as equivalent to executable protections.
   - Future strengthening should move protections toward preferred mechanisms without redefining the underlying invariant.

3. Keep `Preferred execution phase` as regression-selection metadata.
   - Execution phase should help decide whether a regression belongs in local build, CI, integration, UI characterization, or E2E verification.
   - Expensive regressions should not move into fast verifier layers without explicit justification.
   - This metadata should support later answers about which regressions run locally, in CI, or near release.

4. Treat the next M0.3 slice as the ownership and severity model slice.
   - Ownership surfaces should include backend, frontend, shell, cross-layer, Oracle, generated artifacts, build, and CI.
   - Ownership should remain orthogonal to regression category: category classifies mechanism type; owner classifies responsibility.
   - The slice should guard owner, severity, evidence, remediation, and escalation metadata.

5. Separate severity meaning from execution policy.
   - Severity should describe architectural impact, not be tightly coupled to the current build pipeline.
   - Initial severity meanings should distinguish advisory, warning, required, and critical impact levels.
   - Local behavior, CI behavior, and release behavior should be specified separately from severity meaning.

6. Add `Escalation rule` to ownership and severity metadata.
   - Escalation should describe the response path when a regression fails.
   - Initial escalation examples may include local fix, milestone blocker, architectural decision required, and governance review required.
   - This field should connect regression failure to the correct architectural response.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0038 plus this decision checkpoint.
2. Stop executing after the push.
