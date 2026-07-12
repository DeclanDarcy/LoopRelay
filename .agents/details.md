# Canonical Architecture Convergence Universal Details

## 1. Purpose and authority

This document supplements [`.agents/plan.md`](plan.md), the milestone files in
[`.agents/milestones/`](milestones/), and the milestone deep dives in
[`.agents/specs/`](specs/). It contains only implementation details that apply across the
program. Milestone-specific requirements belong to the corresponding `milestones/m*.md` file,
even when that requires duplicating a cross-cutting rule in several milestones.

Use the roadmap's authority order when these documents and implementation evidence disagree:
production code and composition; schema/tests/certification evidence; accepted ADRs; roadmap
intent and milestone acceptance; then older documentation or retained legacy bodies. A detail
marked **owner ruling required** is not an accepted decision merely because a plan or milestone
states a recommended direction.

This supplement deliberately does not repeat plan material that is already actionable. Each gap
names the plan section it completes and the evidence needed to consider the gap closed.

## 2. Cross-program gaps

### G1 — Decision status must be explicit (`plan.md` §§5, 7–21)

Every owner decision remains a proposal until the owner accepts it at its named gate. Maintain one
decision record, not a second docket. Each entry needs status (`Proposed`, `Accepted`, `Rejected`,
or `Superseded`), decision evidence/ADR identity, acceptance date and commit, affected
contracts/tests, and the first milestone blocked by an unresolved status.

If a recommendation is rejected, change the plan, ADR, catalog/schema contracts, and tests in the
same slice. Do not preserve the rejected recommendation as an alternate runtime path.

Closure evidence: a test or generated decision manifest proves every decision required by the
current milestone is `Accepted`; prose in this file, the plan, or a milestone is insufficient.

### G2 — Specification-set integrity (`plan.md` §§2.3, 7)

Before treating generated references as machine-verifiable inputs:

1. choose one durable roadmap path and update every generated source/link consistently;
2. make the specification index accurately describe which milestone specifications exist and
   identify the accepted preservation authority for any intentionally absent specifications; and
3. add a link/source-integrity test that fails on missing normative files, unknown milestone
   targets, duplicate milestone IDs, or a generated source/version mismatch.

This is a governance/input-integrity requirement. Correcting broken references does not reopen an
accepted milestone.

### G3 — Durable acceptance and evidence manifest (`plan.md` §§25–27)

For each milestone, produce one machine-readable, immutable acceptance candidate keyed by commit,
architecture milestone, catalog identity, schema identity/version/fingerprint, prompt-policy and
exact-provider profile identities, and platform. It must contain:

- owner decision/ADR identities and unresolved-decision count;
- obligations added, changed, invalidated, credited, and explicitly uncredited;
- commands, case IDs, results, skips/unsupported reasons, independent observations, and privacy
  scan result;
- production owner and route, the removed/unreachable route, and reachability-verifier result;
- restart/fault boundaries exercised and typed outcomes observed;
- north-star metric deltas; and
- every temporary adapter/duplication with owner, callers, evidence, and deletion milestone.

Every evidence record identifies its durability and portability scope. Local or ignored diagnostic
evidence may not support a release claim that must survive another machine or environment.

### G4 — Schema-version allocation and predecessor coverage (`plan.md` §§4.5, 22)

- Allocate the next contiguous logical version whenever a slice changes durable semantics; record
  the owning milestone and physical manifest fingerprint in the migration catalog.
- A milestone may use more than one version only when independently deployable contract changes
  require it. Never reuse a stamped version for a different physical or semantic shape.
- Every fresh-create database and every supported predecessor chain must converge on the same
  semantic projection and final fingerprint.
- Migration adapters are read-only after import and are removed at their named parity gate.
  Historical absence stays null/unknown; no receipt or observation is synthesized.

### G5 — Architecture enforcement needs allowlists, not name searches (`plan.md` §§25.1, 21)

The ownership/reachability checks need machine-consumed registries for application boundaries,
composition roots, catalogs, kernels, validators, handlers, effect executors, recovery mechanisms,
interaction categories, import adapters, schema manifests, and prompt assets. Each registration
must name its owner, stable key/version, implementation type, supported callers, and retirement
milestone if temporary.

Architecture tests should build a production graph from those registries and enforce dependency
direction and cardinality. Source scanning may supplement this, but a matching class/file name is
not proof of reachability or ownership. Any temporary allowlist entry needs an expiry milestone;
an unowned or expired entry fails acceptance.

### G6 — Certification selection must be recorded before each slice (`plan.md` §25.3)

Before implementation, map every changed obligation to the lowest decisive certification tier and
record why higher tiers are required or not applicable.

- Always run build, full component tests, architecture tests, and static exact-profile fixtures
  affected by the change.
- Run deterministic disposable-repository campaigns when storage, effects, import, catalog,
  application, publication, or idempotency behavior changes.
- Run affected live transition campaigns when prompt, policy, provider, recovery, continuity, or
  provider-facing end-to-end behavior changes.
- Run every full chain affected by shared chain behavior.
- Run a release aggregate only after its referenced evidence exists. A missing platform or
  capability stays missing and cannot be credited by an aggregate result.

The acceptance manifest from G3 stores this mapping and the actual evidence IDs.

## 3. Definition of done for this supplement

Each universal gap above is closed only when its detail is encoded in executable contracts/tests
or superseded by an accepted ADR and corresponding plan/test update. This file is not an
acceptance artifact and does not itself authorize a milestone. When all gaps have executable
resolution, `.agents/plan.md` remains the execution sequence, the regenerated roadmap remains the
program authority, and the G3 manifest plus owner acceptance establishes architectural closure.
