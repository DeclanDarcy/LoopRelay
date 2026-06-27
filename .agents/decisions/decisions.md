# Decisions: 2026-06-26 Milestone 0.2 Certification Posture

These decisions capture only newly authorized direction from the user response following Slice 0033.

## Authorized Decisions

1. Stop adding Contract Oracle evidence by default after Slice 0033.
   - The architectural question has shifted from whether the Oracle architecture repeats to whether current evidence is sufficient to certify Milestone 0.2.
   - Further contract-family coverage now requires a specific uncovered architectural property.

2. Do not automatically add a fourth contract family.
   - Repository dashboard, repository workspace, and primary workflow projection already form a materially different progression: baseline read model, aggregate read model, and rich semantic lifecycle.
   - More samples are not justified unless certification analysis identifies a concrete gap.

3. Treat Slice 0033 as the cross-family repeatability synthesis checkpoint.
   - The supported claim is one Oracle architecture reused across three independently certified pilots.
   - This is stronger than treating the work as three isolated successful pilots.

4. Make the next slice evidence-driven Milestone 0.2 certification analysis.
   - For every required M0.2 output and exit criterion, classify status as `Certified`, `Partial (accepted limitation)`, or `Blocker`.
   - Every partial entry must explain why the limitation does not invalidate certification.

5. Explicitly separate justified architectural claims from unjustified claims in the certification artifact.
   - Justified claims should include backend serialized JSON as Oracle authority, repeatable Oracle lifecycle, reusable consumer verification, reusable artifact freshness, reusable request-boundary verification, and no new Oracle mechanisms required for successive pilots.
   - Unjustified claims should include full contract-surface protection, global certification if not yet complete, and generated contract lifecycle, which remains reserved for Milestone 1.2.

6. Keep decision lifecycle eligibility as the preferred fourth family only if certification identifies a genuine backend-owned eligibility-semantics gap.
   - It is the right next candidate if additional evidence is needed because it stresses semantic authority, compatibility-sensitive booleans, and downstream inference risk.
   - It should not be added for breadth alone.

7. Keep error envelope coverage aligned with later passive transport or runtime isolation work unless Milestone 0.2 certification shows Oracle governance over failure contracts is insufficient.

8. Treat current remaining M0.2 work as certification analysis rather than implementation unless the certification artifact identifies a blocker.

## Next Authorized Sequence

1. Stage, commit, and push Slice 0033 plus this decision checkpoint.
2. Stop executing after the push.
3. In the next work slice, produce the Milestone 0.2 certification artifact described above.
