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

### Parity, producer neutrality, and retained evidence

The shared `PreparedEpic` and `MilestoneSpecificationSet` schemas, gates, and semantic validators
are identical for Traditional and Eval producers. Provenance remains a separate immutable fact and
is not accepted as a Plan branching input. Add a cross-producer fixture that creates semantically
equivalent outputs and proves identical downstream Plan eligibility.

Retain parity evidence, route-reachability evidence, and the legacy-body deletion commit through
M21. The three registered Eval prompt stubs must either have accepted complete contracts and
hash-covered assets or be removed from the catalog before M17 acceptance. D11's
`Planning/CreateNewRoadmap` ruling remains blocking; an unused asset is neither an implemented
capability nor authority.

Run both Traditional and Eval full chains for this shared-chain convergence milestone.
