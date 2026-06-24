# Decisions

## Newly Authorized

- Continue Milestone 4 from the current observability state.
- Preserve the authority direction: lifecycle core feeds observability; observability must not feed lifecycle behavior.
- Treat influence traces as explanation of existing authority, not an alternative interpretation or decision engine.
- Influence traces should continue reusing existing metrics, economics, coherence, policy, eligibility, transfer, and recovery evidence.
- Endpoint layering is accepted as:
  - `/analysis/*` exposes facts.
  - `/lifecycle/policy` exposes the lifecycle decision.
  - `/lifecycle/eligibility` exposes the execution gate.
  - `/lifecycle/projection` exposes lifecycle state.
  - `/lifecycle/history` exposes lifecycle reconstruction.
  - `/lifecycle/influence` exposes lifecycle explanation.
- Before health, add richer projection models for transfer events, continuity artifacts, and size.
- Transfer event projection should expose source session, target session, started/completed timestamps, result, reason, policy decision, eligibility status, and artifact id, derived entirely from transfer evidence and related lifecycle evidence.
- Continuity artifact projection should expose artifact id, fingerprint, source session, target session, decision references, reasoning references, operational context references, and created timestamp without making the artifact an authority source.
- Size projection should expose estimated token count, context byte size, reasoning event count, decision count, session age, idle duration, cache risk, and measured timestamp from metrics snapshots.
- Health should be decomposed into independent dimensions for registry, analysis, policy, eligibility, continuity artifact, transfer, and recovery.
- Do not add a composite health score.
- Health must remain entirely evidence-driven.
- Transfer health should derive from interrupted transfers, transfer-pending duration, transfer diagnostics, and failed transfer events.
- Analysis health should derive from missing snapshots, rebuild failures, and corrupt snapshots rather than score-based heuristics.
- Milestone 4 projection, history, and influence are accepted as complete.
- Milestone 4 health is ready as the next major work area after projection models.
