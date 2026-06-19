# Decisions

## Newly Authorized Decisions

- M4 planning and readiness implementation is accepted as complete.
- M4 preserved the roadmap's intended architectural boundary: backend services and projections own readiness, while the UI consumes projected state.
- Readiness must remain derived only from filesystem facts: `.agents/plan.md` presence and `.agents/milestones/*.md` presence.
- Readiness must not be coupled to artifact content, execution state, completion state, Git state, or UI workspace state.
- The refresh-after-milestone validation is high-value certification coverage because it proves filesystem authority and projection rebuild behavior.
- Current epic implementation status is M0 complete, M1 complete, M2 complete, M3 complete, M4 complete, and M5 not started.
- Do not begin meaningful M5 work until full M1-M4 desktop-path certification has been run.
- Before M5, run certification passes for repository management, artifact infrastructure, artifact lifecycle rotation, refresh behavior, and restart recovery.
- Only defects discovered during certification should be addressed before moving forward to M5.
- If certification passes cleanly, M5 should focus on workspace composition, dashboard refinement, navigation refinement, state restoration, and missing-state polish rather than new infrastructure.
