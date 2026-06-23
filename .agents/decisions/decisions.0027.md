# Decisions

## Newly Authorized

- Start Milestone 8 with backend outcome-oriented reasoning certification.
- Reuse the M7 recovery fixture shape to prove that target reasoning questions remain answerable after repository recovery and service restart.
- Treat M8 as outcome certification over architecture expansion.
- Do not introduce specialized reasoning entities, read models, caches, graph persistence, narrative persistence, or specialized reconstruction engines for this M8 start.
- Preserve the validated M7 path as the basis for certification:

```text
Repository
  -> Graph
  -> Trace
  -> Reconstruction
  -> Materialization Review
```

- Continue treating derived concepts as derived unless certification exposes a concrete answerability, persistence, recovery, or usability failure.
