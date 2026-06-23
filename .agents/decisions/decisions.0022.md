# Decisions

## Newly Authorized

- Accept Milestone 6 as complete specifically because it finished with almost no implementation.
- Treat `Materialization Recommendation != Materialization Authorization` as a formal invariant.
- Treat `Materialization Recommendation != Read Model Creation` as a formal invariant.
- Treat the new specialized read-model boundary test as the required M5-to-M6 certification.
- Raise the burden of proof for future materialization:
  - show a reconstruction failure,
  - show a graph failure,
  - show a query failure,
  - show a repository survivability failure,
  - or show an inability to answer required questions.
- Start Milestone 7 from the core invariant: `Repository Truth -> Recovered Repository -> Equivalent Reconstruction`.
- Structure Milestone 7 around proving persisted events, threads, relationships, references, and provenance are sufficient to rebuild graphs, queries, traces, and reconstructions after recovery.
- Include M7 certification scenarios for repository restart, partial historical repository loading, missing derived state, and replay equivalence.
- Do not allow long-horizon validation to become long-horizon caching.
- In M7, delete or ignore every derived artifact and recover solely from authoritative persistence when certifying equivalent answers.
