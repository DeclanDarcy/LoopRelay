# Decisions

## Newly Authorized

- Proceed with real non-authority continuation progression as the next M9
  implementation slice.
- Keep progression narrow and evidence-driven.
- Treat workflow timeline as lifecycle facts.
- Treat continuation history as coordinator evaluation evidence.
- Continuation may persist coordination evidence.
- Continuation may project eligible stage movement.
- Continuation may not satisfy gates.
- Continuation may not mutate domains.
- Allow mechanical progression only when all of these are true:
  current projection is known, current stage has exactly one eligible next
  stage, no open authority gate exists, the state machine validates the
  transition, the continuation fingerprint has not already been applied, and
  domain evidence proves the source stage is complete.
- Stop with diagnostics whenever those progression conditions are not met.

## Explicitly Deferred

- Do not add preparation yet.
- Do not add hosted continuation yet.
- Do not add decision generation yet.
- Do not add context proposal generation yet.
- Do not add commit preparation yet.
- Do not invoke domain commands yet.
