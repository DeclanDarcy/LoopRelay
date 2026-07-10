# Canonical Orchestration Plan Detail Supplement

## Purpose

This file captures orchestration details that are universally relevant across the canonical migration plan.

Milestone-specific detail requirements belong in `.agents/milestones/m*.md`. Duplicate requirements across milestone files when needed rather than keeping milestone-specific guidance here.

## Cross-Cutting Details

### Architecture Authority

Existing CLIs and workflow implementations are evidence for behavior, not architectural authority. The canonical workflow contracts, transition runtime, resolver, controller, and product model become authority once introduced and certified.

### Repository Ownership

All workflow state, stage state, transition evidence, blockers, recovery state, products, chain progression, and storage authority must be interpretable from repository-owned evidence. Hidden process memory, global orchestrator state, cached decisions, and CLI call chains must not be required to reconstruct progress.

### Progress Is Not Prompt Success

Every milestone should preserve these distinctions:

- prompt rendered
- prompt executed
- raw output captured
- output interpreted
- output product emitted
- output product validated
- effects applied
- transition completed
- stage completed
- workflow completed
- workflow chain completed

No later state may be inferred solely from prompt completion or artifact existence.

### Freeze Discipline

Later milestones consume earlier certified models rather than redefining them. Any later need for a new foundational concept should explicitly revisit the milestone that owns that concept instead of introducing a competing definition downstream.
