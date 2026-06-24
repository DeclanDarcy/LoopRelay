# Decisions

## Newly Authorized

- Treat the Stage 2B economics implementation as successful.
- Preserve the analysis dependency graph as `Evidence -> Metrics / Statistics / Cache -> Economics -> Coherence -> Policy`.
- Keep the economics layer operating from the Stage 2A metrics boundary rather than direct repository crawling.
- Keep economics snapshots derived, rebuildable, and non-authoritative.
- Keep economics analysis pure: no policy decisions, no transfer execution, and no lifecycle mutation.
- Accept the temporary assumed coherence score in economics until Stage 2C owns coherence explicitly.
- Classify the current solution-wide `ExecutionSessionServiceTests` failures as unrelated instability to investigate separately, not a blocker for Stage 2C.
- Capture the execution-session failure signatures so future lifecycle recovery work does not normalize them as expected noise.
- Treat Stage 2C coherence as ready to begin.
- Make coherence primarily topology-driven from reasoning events, threads, relationships, graph structure, decision references, and continuity revisions.
- Do not make coherence a disguised economics score; token count, context size, cost, TTL, and cache benefit are weak coherence inputs.
- Keep transfer pressure conceptually separate from coherence as a synthesis signal from coherence, economics, and metrics, even if stored in the coherence snapshot.
- No roadmap adjustments are indicated.
