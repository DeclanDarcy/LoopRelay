# M17 — Roadmap capability convergence


### Implementation

- [ ] Express Traditional and Eval Roadmap progression exclusively in the M13 catalog and M14 kernel. Move only candidate construction, parsing, and validation behavior from `LoopRelay.Roadmap.Cli` into canonical handlers/owners.
- [ ] Enforce one shared, versioned schema and gate contract for `PreparedEpic` and `MilestoneSpecificationSet`; preserve producer provenance without allowing downstream Plan behavior to branch on the producer.
- [ ] Route every prompt through the canonical prompt gateway and every mutation/review/recovery/interaction through its owner.
- [ ] Validate and either fully implement or remove unavailable declarations for `CreateEvalDependencyInventory`, `CreateEvalHypothesisInventory`, and `CreateEvalDag`. Apply D11 to `Planning/CreateNewRoadmap`.
- [ ] Port useful Roadmap tests to canonical project tests before deletion: state/resume, selection, split-family lineage, promotion, prompt contracts, transition journaling, storage/import, projections, failure persistence, and completion routing.
- [ ] Prove no new run or recovery command can enter `LoopRelay.Roadmap.Cli` or its readers/state machines.
- [ ] After component and live parity plus owner acceptance, delete `src/LoopRelay.Roadmap.Cli/`, its test project, solution/project references, publish artifacts/scripts, and last-only prompt/readers/assets.

### Exit gate

- [ ] Traditional and Eval routes produce the same validated downstream products/gates under canonical authorities, with distinct provenance only. Default and forced selection, recovery, publication, and downstream Plan entry pass. Building and running after physical deletion changes no supported behavior.

