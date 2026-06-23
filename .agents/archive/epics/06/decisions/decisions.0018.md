# Decisions

## Newly Authorized

- Treat Milestone 4 as complete after the shell/UI query and reconstruction slice.
- Preserve the M4 boundary:
  - `Query -> Trace Candidates`
  - `Trace -> Reconstruction`
  - `Reconstruction -> Presentation`
- Treat the next reconstruction work as Historical Classification Reconstruction, not historical entity/state reconstruction.
- Historical reconstruction may derive event families, traces, relationships, references, provenance, and classifications visible at time T.
- Historical reconstruction must not expose lifecycle-bearing hypothesis, alternative, contradiction, or direction entities.
- Avoid status language such as active, resolved, rejected, accepted, or superseded for derived hypothesis/alternative/contradiction/direction concepts unless a later materialization review explicitly authorizes it.
- Use a trace-oriented unit such as `ReasoningTrace`, `TraceSnapshot`, or `HistoricalReasoningView`; do not introduce `HypothesisSnapshot`, `AlternativeSnapshot`, `ContradictionSnapshot`, or `DirectionSnapshot`.
- Treat accidental lifecycle creation for hypothesis, alternative, contradiction, and direction classifications as the main M5 architectural risk.
