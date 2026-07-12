<!-- BEGIN GENERATED: source=.agents/epic.md version=3.0 index -->
# Milestone Deep-Dive Index

Source: [`.agents/epic.md`](../epic.md), version 3.0 (2026-07-12).

This index covers every architecture milestone named by the roadmap. M0–M7 are accepted historical boundaries, so their files specify preservation and post-merge reconciliation rather than reimplementation. M8–M21 are open implementation blueprints. The unnumbered post-merge baseline ratification gate is a hard prerequisite to M8, not an invented milestone.

The roadmap defines no separate capability IDs. The deep dives preserve capability names without manufacturing identifiers. Filenames preserve the explicit milestone IDs and the roadmap’s `m0-...` example convention; the apparent conflict between “zero-padded” wording and those examples is resolved in favor of exact IDs/examples.

| Milestone | Name | Roadmap status | Hard prerequisite(s) | Deep dive |
|---|---|---|---|---|
| M0 | Architecture Constitution | Accepted | Roadmap authority | [m0-architecture-constitution-deep-dive.md](m0-architecture-constitution-deep-dive.md) |
| M1 | Workspace State Authority | Accepted | M0 | [m1-workspace-state-authority-deep-dive.md](m1-workspace-state-authority-deep-dive.md) |
| M2 | Evaluation Authority | Accepted | M1 | [m2-evaluation-authority-deep-dive.md](m2-evaluation-authority-deep-dive.md) |
| M3 | Product Authority | Accepted | M2 | [m3-product-authority-deep-dive.md](m3-product-authority-deep-dive.md) |
| M4 | History Authority | Accepted | M3 | [m4-history-authority-deep-dive.md](m4-history-authority-deep-dive.md) |
| M5 | Policy Authority | Accepted | M4 | [m5-policy-authority-deep-dive.md](m5-policy-authority-deep-dive.md) |
| M6 | Prompt Authority | Accepted; supersession ratification | M5 | [m6-prompt-authority-deep-dive.md](m6-prompt-authority-deep-dive.md) |
| M7 | Runtime Authority | Accepted | M6 | [m7-runtime-authority-deep-dive.md](m7-runtime-authority-deep-dive.md) |
| M8 | Effect Coordinator | Open | Post-merge baseline ratification | [m8-effect-coordinator-deep-dive.md](m8-effect-coordinator-deep-dive.md) |
| M9 | Recovery Coordinator | Open | M8 | [m9-recovery-coordinator-deep-dive.md](m9-recovery-coordinator-deep-dive.md) |
| M10 | Interaction Broker | Open | M9 | [m10-interaction-broker-deep-dive.md](m10-interaction-broker-deep-dive.md) |
| M11 | Workspace Storage Authority | Open | M8, M10 | [m11-workspace-storage-authority-deep-dive.md](m11-workspace-storage-authority-deep-dive.md) |
| M12 | Import Gateway | Open | M11 | [m12-import-gateway-deep-dive.md](m12-import-gateway-deep-dive.md) |
| M13 | Workflow Catalog | Open | M12, M9 | [m13-workflow-catalog-deep-dive.md](m13-workflow-catalog-deep-dive.md) |
| M14 | Orchestration Kernel | Open | M13, M10 | [m14-orchestration-kernel-deep-dive.md](m14-orchestration-kernel-deep-dive.md) |
| M15 | Completion Authority | Open | M14 | [m15-completion-authority-deep-dive.md](m15-completion-authority-deep-dive.md) |
| M16 | Canonical Read Model | Open | M15, M12 | [m16-canonical-read-model-deep-dive.md](m16-canonical-read-model-deep-dive.md) |
| M17 | Roadmap capability convergence | Open | M16 | [m17-roadmap-capability-convergence-deep-dive.md](m17-roadmap-capability-convergence-deep-dive.md) |
| M18 | Plan capability convergence | Open | M17 | [m18-plan-capability-convergence-deep-dive.md](m18-plan-capability-convergence-deep-dive.md) |
| M19 | Execute capability convergence | Open | M18, M15 | [m19-execute-capability-convergence-deep-dive.md](m19-execute-capability-convergence-deep-dive.md) |
| M20 | Application Boundary convergence | Open | M19 | [m20-application-boundary-convergence-deep-dive.md](m20-application-boundary-convergence-deep-dive.md) |
| M21 | Retirement completion | Open | M20 | [m21-retirement-completion-deep-dive.md](m21-retirement-completion-deep-dive.md) |

## Dependency Sequence

```text
Post-merge baseline ratification
  -> M8 Effects
  -> M9 Recovery
  -> M10 Interaction
  -> M11 Storage
  -> M12 Import
  -> M13 Catalog
  -> M14 Kernel
  -> M15 Completion
  -> M16 Read Model
  -> M17 Roadmap
  -> M18 Plan
  -> M19 Execute
  -> M20 Application Boundary
  -> M21 Retirement
```

Cross-edges preserved from roadmap §9: M8 and M10 both enable M11; M9 also enables M13; M10 also enables M14; M12 also enables M16; M15 also enables M19.

## Open Decisions and Ambiguities

- Baseline: ratify prompt template + versioned prompt-policy profile composition, canonical logical-v9/configuration-policy separation, durable pre-dispatch prompt facts, typed obstacle vocabulary, and the green component baseline.
- M9: cancellation salvage at each durable boundary.
- M10: timeout/default policy, isolation depth, and trust evidence.
- M11: whether status migrates; deep dive uses the roadmap-recommended read-only default.
- M12: enumerate the owner’s actual workspace portfolio and conflict rulings.
- M15: typed obstacle mapping and partial closure failure/resume semantics.
- M16: durability of release evidence and exact-profile promotion/retirement.
- M17: reserved full-roadmap generation intent.
- M18: restart behavior when exact capabilities differ.
- M19: first-run sequencing and review order.
- Filename wording is ambiguous as described above but does not block implementation.

## Consistency Contract

- Each file contains the same 31 required sections in the required order; `Open Implementation Questions` is appended only where the roadmap has a gated ruling.
- Shared authority and status vocabulary comes from roadmap §§0, 3, 5, and 7; milestone files describe only milestone-specific implementation.
- Generated regions are delimited. A future idempotent regeneration replaces only those regions and preserves clearly separated content outside them.
- Human-facing documentation production is not an implementation activity. Section 29 is retained because the requested schema requires it, but normally limits work to tested machine-consumed manifests/contracts. M21 alone carries the roadmap’s explicit stale-claim deletion requirement.

<!-- END GENERATED: index -->

