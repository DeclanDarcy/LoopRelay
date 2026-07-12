# Canonical Architecture Convergence Plan Gap Details

## 1. Purpose and authority

This document supplements [`.agents/plan.md`](plan.md). It does not replace the regenerated
roadmap or the milestone deep dives in [`.agents/specs/`](specs/). It records the implementation
detail needed to close meaningful omissions, ambiguities, and internal tensions in the plan.

Use the roadmap's authority order when this document and implementation evidence disagree:
production code and composition; schema/tests/certification evidence; accepted ADRs; roadmap
intent and milestone acceptance; then older documentation or retained legacy bodies. A detail
below marked **owner ruling required** is not an accepted decision merely because the plan states
a recommended direction.

This supplement deliberately does not repeat plan material that is already actionable. Each gap
names the plan section it completes and the evidence needed to consider the gap closed.

## 2. Cross-program gaps

### G1 — Decision status must be explicit (`plan.md` §§5, 7–21)

The roadmap and deep dives still classify D1–D13's subjects as owner decisions. The plan turns
several recommended rulings into imperative implementation text, which can be misread as prior
acceptance.

- D1–D4 are blocking baseline-ratification proposals. M8 cannot begin until the owner accepts
  them and the acceptance is encoded in enduring ADRs and executable architecture tests.
- D5–D13 remain proposals until the owner accepts each at its named gate. In particular, D8 is
  not proof that the listed formats are the owner's actual workspace portfolio.
- Maintain one decision record, not a second docket. Each entry needs status (`Proposed`,
  `Accepted`, `Rejected`, or `Superseded`), decision evidence/ADR identity, acceptance date and
  commit, affected contracts/tests, and the first milestone blocked by an unresolved status.
- If a recommendation is rejected, change the plan, ADR, catalog/schema contracts, and tests in
  the same slice. Do not preserve the rejected recommendation as an alternate runtime path.

Closure evidence: a test or generated decision manifest proves every decision required by the
current milestone is `Accepted`; prose in this file or the plan is insufficient.

### G2 — Specification-set integrity (`plan.md` §§2.3, 7)

The generated deep dives link their normative roadmap as `../epic.md`, which resolves to the
currently absent `.agents/epic.md`; the actual roadmap supplied with this plan is
`.agents/specs/epic.md`. The index also links M0–M7 deep dives that are absent from the current
specification directory.

Before treating generated references as machine-verifiable inputs:

1. choose one durable roadmap path and update every generated source/link consistently;
2. either restore/generate the indexed M0–M7 preservation specifications or remove those links
   and state that their accepted commits plus the roadmap are the preservation authority; and
3. add a link/source-integrity test that fails on missing normative files, unknown milestone
   targets, duplicate milestone IDs, or a generated source/version mismatch.

This is a governance/input-integrity fix only; it does not reopen M0–M7.

### G3 — Durable acceptance and evidence manifest (`plan.md` §§25–27)

The plan lists evidence fields but does not define the atomic acceptance record that binds them.
For each milestone produce one machine-readable, immutable acceptance candidate keyed by commit,
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

The record may remain local only while D9 is unresolved, but it must then say `LocalOnly` and may
not support a cross-machine release claim. Ignored `.tmp` files are diagnostic evidence, not
durable release provenance. M21 may accept cross-machine claims only from the D9-selected durable,
scrubbed evidence owner.

### G4 — Schema-version allocation and predecessor coverage (`plan.md` §§4.5, 22)

M8 owns the first post-baseline logical version, v10. Later milestones must not keep referring to
their live schema as v9 or assume a preallocated number.

- Allocate the next contiguous logical version whenever a slice changes durable semantics; record
  the owning milestone and physical manifest fingerprint in the migration catalog.
- A milestone may use more than one version only when independently deployable contract changes
  require it. Never reuse a stamped version for a different physical/semantic shape.
- Every fresh-create database and every supported predecessor chain must converge on the same
  semantic projection and final fingerprint. Tests for v8/partial-v9 remain ingress tests even
  after the production version has advanced beyond v10.
- Migration adapters for old effect, recovery, compatibility, or feature tables are read-only
  after import and are removed at their named parity gate. Historical absence stays null/unknown;
  no receipt or observation is synthesized.

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

“Applicable” certification is otherwise subjective. Before implementation, map every changed
obligation to the lowest decisive tier and record why higher tiers are required or not applicable.

- Always run build, full component tests, architecture tests, and static exact-profile fixtures
  affected by the change.
- Run deterministic disposable-repository campaigns for storage, effects, import, catalog,
  application, publication, and idempotency behavior.
- Run the affected live transition campaigns whenever prompt, policy, provider, recovery,
  continuity, or provider-facing end-to-end behavior changes.
- Run both Traditional and Eval full chains for M14 and M17–M21 and for any earlier change that
  alters shared chain behavior.
- Run the release aggregate only after its referenced evidence exists. A missing platform or
  capability stays missing and cannot be credited by an aggregate result.

The acceptance manifest from G3 stores this mapping and the actual evidence IDs.

## 3. M8 — Effect Coordinator gaps

### M8.1 — Legal effect lifecycle and retry authority (`plan.md` §8)

Use an append-only lifecycle with a derived current state. The minimum legal flow is:

```text
Planned -> Leased -> Started -> Pending | Succeeded | Failed | Stalled | Cancelled | Unknown
Unknown -> Reconciling -> Succeeded | Failed | Stalled | RetryAuthorized | HumanActionRequired
Failed/Stalled -> RetryAuthorized only through policy/recovery -> Leased
expired Leased with no Started fact -> Planned/lease-available
expired Leased after Started or indeterminate executor evidence -> Unknown
```

`Pending` means known incomplete required-asynchronous work, not uncertainty. `Unknown` means the
mutation may have occurred. Neither failed, stalled, cancelled, nor unknown work is automatically
executed merely because the scanner finds it. Only an unstarted plan or a durable
`RetryAuthorized` fact is executable. Every transition validates row version, lease owner/expiry,
dependency settlement, executor version, and preconditions.

A local output-surface commit is `BlockingLocal`. Its verified receipt permits the attempt to
advance to the point allowed by the catalog. A push is a distinct `RequiredAsync` intent; the
public result may expose it as pending, but Plan readiness, certified completion, and any catalog
boundary that declares it required cannot settle until its postcondition is verified.

### M8.2 — Receipt and reconciliation contract (`plan.md` §§8, 24)

Every receipt needs intent and attempt identity, semantic operation/idempotency key, executor
key/version, normalized target identity, before observation, after observation, postcondition
verdict, external correlation (commit/ref/path/process identifier as applicable), evidence IDs,
and observation time as diagnostic data. Reconciliation must use an independent observer rather
than trusting the executor's returned success.

The decisive negative fixture is mutation success followed by receipt-write loss: restart must
observe the postcondition, append a receipt/reconciliation fact, and create no second semantic
mutation. Add equivalent cases for duplicate leases, cancellation, dependency-order violation,
unavailable remote push, and a crash after receipt but before state settlement.

### M8.3 — Direct-mutation retirement boundary (`plan.md` §8 Routing and extraction)

Inventory call sites for Git, filesystem writes/moves/deletes, archive materialization, exports,
projection/history materialization, nested `.agents` publication, parent gitlink publication,
checkpoint cleanup, and completion cleanup. The architecture registry from G5 must identify the
allowlisted effect adapter for each operation and reject production calls from feature handlers,
kernel, application, completion decision code, or CLI. A handler may construct typed payload data;
it may not invoke the outward adapter or advance workflow state.

## 4. M9 — Recovery Coordinator gaps

### M9.1 — Classification precedence and evidence matrix (`plan.md` §9)

Avoid overlapping `AcceptedUnknown`/`ProviderUnknown` meanings by defining one boundary taxonomy:

| Last durable evidence | Classification | Minimum permitted next step |
|---|---|---|
| no authorized attempt/dispatch/effect start | `NotStarted` | authorize normal work |
| authorized work, no outward-start fact | `InFlight` only while a valid lease/process correlation exists; otherwise reclassify from evidence | wait or inspect |
| provider/effect start, no terminal observation | `AcceptedUnknown` | reconcile; never resend/re-execute |
| normalized provider output durable, promotion absent | `SucceededUncommitted` | validate freshness and reuse output or supersede via a new plan |
| durable explicit terminal failure | `Failed` | policy-gated new plan/attempt |
| durable cancellation | `Cancelled` plus the boundary-specific salvage facts | apply accepted D5 ruling |
| one or more effects settled and required work remains | `PartiallyEffected` | reconcile/resume the same effect plan |
| completion closure partly settled | `CompletionPartiallyClosed` | resume the same closure plan |
| facts conflict or required boundary evidence is absent/corrupt | `EvidenceIncomplete` or `Corrupt` | fail closed; repair/import/human decision |

Classifications are immutable observations. New evidence appends a new classification that
supersedes the prior identity; it never edits the prior fact. The planner must persist the exact
source-evidence set and selected mechanism before action.

### M9.2 — Action legality and identity (`plan.md` §9)

Each recovery action records plan/action identity, source attempt/effect/session/completion IDs,
required capability/profile and policy evidence, pre/postconditions, idempotency key, and result.
`ResumeSession`, `NativeFork`, and provider read are authorized only by the exact observed profile.
`ReconstructContext` must bind the reconstructed input receipts and prompt facts. `RetryNewAttempt`
keeps root run, workflow instance, and transition-run identity but mints a new immutable attempt.
`ReuseRawOutput` never creates a second dispatch. `Compensate` is an effect plan, not an in-memory
undo.

Add the deep-dive fixture omitted from the general plan: a lost provider thread with retained
rollout evidence. It must select only a certified salvage/reconstruction path and must not infer
resume/fork support from the interface alone.

### M9.3 — Cancellation remains a blocking ruling (`plan.md` D5)

The D5 matrix is a recommended policy, not accepted roadmap authority. Before implementing M9,
the owner must rule cancellation before dispatch, after outward acceptance, after validated
output, during partial effects, and during partial completion closure. Tests must cover caller
cancellation plus terminal evidence written with a non-cancelled evidence token. No ruling may
erase accepted/unknown work or convert cancellation into ordinary failure.

## 5. M10 — Interaction Broker gaps

### M10.1 — Request/response state machine (`plan.md` §10)

Use the durable flow:

```text
Required -> Persisted -> Presented
Presented -> valid Responded -> Validated -> Resolved -> ResumeAuthorized
Presented -> Expired | Defaulted | Cancelled
invalid/late/conflicting response -> Rejected event; request state unchanged
```

Presentation is idempotent and can recur after restart. The immutable response is recorded only
after schema, request state, deadline, semantic idempotency, correlation, and compare-and-set
checks succeed. An identical semantic duplicate returns the existing response/resolution IDs; a
different duplicate appends a rejection and cannot replace the accepted response. Kernel or
Recovery resumes only from `ResumeAuthorized`, never directly from submitted JSON.

### M10.2 — Initial category registry and unresolved policy (`plan.md` D6, §10)

The registry must at least reserve typed categories for dirty-input commit offer, import conflict,
recovery ambiguity, and completion ambiguity. Each category declares question/presentation
version, response JSON schema/hash, deadline behavior, allowed default, headless result, required
authorization/trust evidence, and resolver owner. M10 production-wires only the dirty-input offer;
later owners activate their categories without inventing another interaction store.

D6's timeout/default, isolation depth, and trust-evidence choices remain owner rulings. Until
accepted, use no hidden timeout/default and fail headless requests with the category-specific
typed result. For dirty input specifically, headless mode creates no request and no commit; it
returns `DirtyInputSurface` with the declared surface and Git evidence.

## 6. M11 — Workspace Storage Authority gaps

### M11.1 — Break the M11/M12 export round-trip cycle (`plan.md` §§11–12)

M11 precedes M12, yet the deep dive's semantic exporter/comparator names M12 and the plan asks for
export -> fresh import. Close M11 without giving it legacy-import authority as follows:

- M11 owns the versioned canonical export codec, canonical decoder, semantic fingerprint, and a
  test-only/fresh-target canonical rehydration path.
- M11 acceptance performs export -> fresh canonical rehydration -> owner-projection comparison.
  This proves the package without exposing a public legacy `storage import` implementation.
- The public `storage import` use case remains explicitly unavailable/delegated until M12 wires
  source detection, mapping, approval, and one-way import.
- M12 may consume the canonical codec but may not redefine its schema or storage semantics.

### M11.2 — Recoverable initialization when no ledger exists (`plan.md` §11)

`storage init` cannot persist its plan in the target database before that authority exists. Use a
deterministic staging database/artifact under the canonical persistence directory, keyed by
operation ID, containing the intended workspace identity, target schema manifest, operation plan,
and creation evidence. Validate it completely, then use one absence-guarded atomic filesystem
promotion effect to install the canonical database. Restart inspects the staging artifact and
target independently and either completes the same promotion, records the existing matching
receipt, or fails ambiguous; it never overwrites an existing authority.

Migration/sync plans for an existing recognizable database live in that database's operation
journal. Database-internal transactional changes are executed by the Storage Authority's
versioned migration executor; outward file creation, replacement, export, and projection work are
M8 effects. Do not model every SQL statement as a separate external effect.

### M11.3 — Strict read-only verification (`plan.md` §§4.5, 11)

Verification/status must open SQLite in an OS/driver-enforced read-only mode and must not create or
alter the database, `-wal`, `-shm`, journal, metadata, migration receipt, access time where the test
platform can observe it, or any workspace projection. Hash and inventory the relevant persistence
tree before and after two repeated queries. Report `Healthy`, `ActionRequired`, `Unsupported`, or
`Corrupt` with identity/family/version/shape/fingerprint evidence. A v8 or recognized partial-v9
source reports the complete migration chain to the then-current version; it does not stop at v9.

### M11.4 — Bound `storage sync` and export semantics (`plan.md` §11)

`storage sync` may reconcile only rebuildable projections and already-journaled effect work from
canonical facts. It cannot import legacy facts, rewrite authoritative history, merge another
store, or invent defaults. Its plan enumerates each projection/effect, source watermark,
postcondition, and expected no-op cases.

The export manifest needs schema/codec version, workspace identity, domain row counts, canonical
ordering rules, explicit null/unknown fields, per-domain hashes, whole-package SHA-256, and a
logical fingerprint independent of SQLite bytes and insertion order where domain order is not
semantic. Round-trip comparison reports a typed per-domain semantic diff.

## 7. M12 — Import Gateway gaps

### M12.1 — Portfolio is an accepted registry, not D8 prose (`plan.md` D8, §12)

For every owner-approved format, register source kind/family/version, unambiguous detector,
read-only reader, source fingerprint algorithm, mapped domains/identity rules, unsupported fields,
conflict rules, semantic comparator, fixture identities, and retirement/exhaustion criteria.
Unknown, mixed, or overlapping detector matches fail closed. D8's list becomes executable only
after owner acceptance and a fixture for each actual owned workspace family.

### M12.2 — Import ordering and partial external work (`plan.md` §12)

“All-or-nothing canonical writes/effects” cannot mean an atomic transaction across SQLite and
external targets. Use this ordering:

1. detect and preview without writes; bind preview to a complete source fingerprint;
2. validate explicit approval or a resolved M10 interaction and persist an immutable plan;
3. re-hash the source and reject stale preview before any canonical write;
4. map and stage canonical facts in one database transaction, preserving valid identities and
   recording correspondence for remapped IDs;
5. validate the staged target projection and commit canonical facts plus verification evidence;
6. execute any outward/projection work as ordered M8 effects and recover it through M9;
7. append the import receipt only after semantic verification and required effects settle; then
   append the monotonic canonical-only/source-non-authoritative facts; and
8. permit normal runtime only from the canonical source.

Failure before the canonical transaction commits rolls back. Failure afterward leaves one pending
import operation and resumes/reconciles it; it does not delete verified facts or repeat settled
effects. A repeated identical import returns the same receipt. A source identity collision is
preserved only when type, scope, and semantics agree; otherwise mint a canonical ID and store an
immutable source-to-target correspondence.

### M12.3 — Adapter exhaustion evidence (`plan.md` §§12, 21)

An adapter exhaustion fact is keyed by adapter/version and accepted portfolio snapshot. It links
every owned fixture to a successful receipt, canonical-only run, and adapter-disabled result. A
newly discovered owned format invalidates/supersedes the exhaustion fact; it must not silently
reactivate a deleted fallback.

## 8. M13 — Workflow Catalog gaps

### M13.1 — Canonical identity and stable obligation keys (`plan.md` §13)

Define a versioned canonical serialization for the fully derived catalog. Normalize Unicode and
repository-relative paths, use invariant enum/scalar encodings, sort maps and unordered sets by
stable identity, preserve order only where workflow semantics require it, exclude diagnostics and
process/type names, include referenced prompt/profile/schema/capability versions and structurally
derived effects, then compute SHA-256. The catalog ID and explicit semantic version are both stored
on root runs and workflow instances.

An obligation key should be derived from owner + obligation kind + stable semantic path/identity,
not array position or the whole catalog hash. Its content/version hash changes when the obligation
semantics change. Adding one declaration therefore adds or changes only its affected obligations;
it does not renumber the ledger.

### M13.2 — Version availability for active runs (`plan.md` §§13–14)

On restart, an active run must resolve the exact catalog ID/version it recorded. Keep accepted
catalog snapshots/declarations available for all active durable lineages, or provide an explicit
catalog migration decision that proves semantic compatibility. Missing or mismatched catalog
identity is `RecoveryRequired`/specific unsupported state, never a silent upgrade to the newest
catalog. New root runs use the current accepted snapshot.

### M13.3 — Registry validation and failure ordering (`plan.md` §13)

Validator, handler, effect, recovery, interaction, and capability references resolve through
unique owner registries from G5. Catalog validation collects all deterministic, path-qualified
errors, orders them stably, and runs before workspace access or provider/process initialization.
Surface validation resolves repository target, normalized path, root escape, symlink ambiguity,
nested-repository topology, ownership, commit policy, and push policy. An output surface without
its derived publication obligations is invalid.

## 9. M14 — Orchestration Kernel gaps

### M14.1 — Durable kernel decisions and re-entry (`plan.md` §14)

Persist an immutable kernel-decision/boundary fact for each observation cycle: decision ID,
catalog identity, snapshot/watermarks, root/workflow/transition/attempt identities, eligible and
rejected alternatives with gate evidence, selected action, and outcome/reason. This is distinct
from the rebuildable read model.

At invocation, locate the single nonterminal root run for the workspace and requested chain. Zero
creates a new authorized root; one re-enters it; more than one is an ambiguity/corruption result
and no work starts. A successor workflow gets a new workflow-instance ID under the same root. A
terminal root short-circuits from facts and creates no attempt, provider session, or effect.

### M14.2 — Resolution and bounded execution (`plan.md` §§14, 20)

No eligible transition is not automatically failure: use catalog terminal evidence to return
completed, known wait conditions to return waiting/human/effect/recovery required, conflicting
eligible paths to return ambiguous, and missing/invalid prerequisites to return the specific
cannot-proceed result. Persist the rejected alternatives and evidence.

A bounded run command supplies a maximum observation/transition budget as invocation policy. When
the budget is exhausted, return passive waiting with the causal snapshot and next eligible work;
do not persist a workflow stall or manual latch. The kernel reobserves after every attempt,
effect, recovery action, or interaction resolution before spending the next unit.

### M14.3 — Required-write and handler boundaries (`plan.md` §14)

A required write failing before outward work returns failed/specific storage-unusable with no
advance. A failure after outward start becomes `RecoveryRequired` with the last durable identity.
Handlers are registry-owned pure candidate/evidence transformations: no progression choice,
ambient configuration, prompt framing, policy resolution, persistence selection, external
adapter, retry, interaction presentation, or completion settlement.

## 10. M15 — Completion Authority gaps

### M15.1 — Certificate, closure, and terminal fact are distinct (`plan.md` §15)

A `CompletionCertificate` records an accepted certified candidate and its evidence; it is not the
public terminal claim. The public run is certified only after the immutable closure plan's every
required effect has a verified receipt and the Completion/State settlement transaction appends
the monotonic `CertifiedTerminal` fact.

The terminal fact is authoritative ledger settlement, not an outward effect. Projection-file
materialization may be an effect. This avoids a circular design in which an effect must claim
authoritative completion before effects are known settled. A later decision may supersede a
nonterminal completion decision through a new attempt; it never edits the old decision or
certificate.

### M15.2 — Partial closure and cleanup (`plan.md` §15)

Archive success followed by push failure remains `EffectsPending` (or `RecoveryRequired` when the
push outcome is unknown), never certified. Settled closure effects are retained, not compensated
merely because a later effect failed. Cleanup/retirement runs only after archive, context update,
nested and parent publication, and independent postconditions; cleanup failure leaves the same
closure plan recoverable. Terminal rerun reads the terminal fact and creates zero new provider
sessions/turns, effect intents, user-tree writes, or Git mutations.

D2 and M15's obstacle vocabulary and partial-effect/resume behavior remain owner rulings until accepted;
the outcome matrix must include every specific cannot-proceed reason as well as continue, waiting,
failed, cancelled, effect-pending, recovery-required, and certified-terminal.

## 11. M16 — Canonical Read Model gaps

### M16.1 — Consistent snapshot and claim shape (`plan.md` §16)

Compose owner projections under one SQLite read transaction where they share the canonical store.
For independent filesystem/Git/provider evidence, capture before/after watermarks; retry a bounded
number of times if they change, then return a snapshot with explicit staleness/conflict rather
than combining incompatible moments. Compute snapshot identity from workspace, schema/catalog
identity, ordered owner watermarks, and external observation identities.

Represent every operational claim as value/status plus owner, source fact/evidence IDs, source
watermark, observed version, and one of `Known`, `Unknown(reason)`, `Conflict(source set)`, or
`Stale(reason)`. The aggregate composer can join and expose conflicts but cannot choose an owner,
fill a default, trigger migration, or repair state.

### M16.2 — Determinism, obligation credit, and profile retirement (`plan.md` §16)

All collections exposed to renderers have canonical ordering. Given one snapshot, text/JSON/export
rendering is byte-deterministic and has no repository, database, provider, clock, or environment
dependency.

Evidence credit requires an exact obligation key/content version plus catalog/schema/asset/profile
scope and evidence tier. Evidence for an older content version is displayed as stale/uncredited,
not inherited. Exact provider profiles may be retired only after no active root, attempt, session,
recovery plan, or evidence claim references them and the D10 replacement evidence exists.

D9 and D10 remain owner rulings. Until D9 is accepted, release claims sourced only from `.tmp`
must be visibly `LocalOnly`.

## 12. M17 — Roadmap convergence gaps

### M17.1 — Parity evidence and producer neutrality (`plan.md` §17)

The shared `PreparedEpic` and `MilestoneSpecificationSet` schemas, gates, and semantic validators
must be identical for Traditional and Eval producers. Provenance remains a separate immutable
fact and must not be accepted as a Plan branching input. Add a cross-producer fixture that creates
semantically equivalent outputs and proves identical downstream Plan eligibility.

Retain parity evidence, route-reachability evidence, and the legacy-body deletion commit through
M21. The three registered Eval prompt stubs must either have accepted complete contracts and
hash-covered assets or be removed from the catalog before M17 acceptance. D11's
`Planning/CreateNewRoadmap` ruling remains blocking; an unused asset is neither an implemented
capability nor authority.

## 13. M18 — Plan convergence gaps

### M18.1 — Scoped artifact transaction (`plan.md` §18)

Before mutation, record a manifest of the declared candidate output surface: repository identity,
commit, normalized paths, existence, hashes, modes, and aggregate surface hash. Apply changes to a
candidate/staging surface through effect-owned writes. Review and validation read the candidate;
promoted product facts and live output surfaces remain unchanged until schema, gates, and input
freshness pass. Rejection/validation failure reconciles an effect-owned restore/removal to the
recorded manifest and appends rollback evidence.

Publication order is strict: nested `.agents` materialize -> nested commit -> required nested push
settled -> parent gitlink/materialize -> parent commit -> required parent push settled -> promote
`ExecutionReadiness`. A receipt must identify repository top, ref, commit, tree/surface hash, and
remote postcondition. No early readiness is allowed even if product validation passed.

### M18.2 — Warm continuity mismatch remains a ruling (`plan.md` D12)

A warm checkpoint binds session/turn, exact executable/app-server profile, rendered prompt facts,
input receipts/surface hash, plan candidate hash, catalog identity, and causal spine. On mismatch,
use only the owner-accepted D12 mechanism: certified reconstruction if available; otherwise
unsupported-capability/human-action-required. Never silently resume under a different profile or
start a fresh warm session and call it continuation.

## 14. M19 — Execute convergence gaps

### M19.1 — Execution authorization freshness (`plan.md` §19)

`ExecutionAuthorization` is an immutable fact/hash over decision product, recommendation evidence,
policy evaluation, effective runtime profile, exact provider profile/capabilities, prompt-policy
profile, rendered-prompt fact, consumed-input manifest, catalog transition, permission/approval/
sandbox/network ceilings, and causal identities. Attempt authorization rechecks that every
referenced fact is current. A stale or missing reference returns a specific result and cannot fall
back to raw model/effort or recommendation values.

### M19.2 — Stall predicate and sequence require owner acceptance (`plan.md` D13, §19)

D13's first-run/review order remains proposed. Once accepted, encode it in catalog successors and
test all nonterminal routes. Likewise define a catalog-owned deterministic “substantive change”
predicate over current Git diff/commit evidence, promoted product versions, history/evaluation
facts, and declared output surfaces. The predicate must identify which evidence was unchanged and
return `Stalled` without incrementing a counter or setting a manual latch. Until that predicate
and sequencing are accepted, M19 cannot delete the retained loop.

Cancellation and unknown cases inherit the accepted M9 boundary matrix. An already certified root
short-circuits without decision, provider, handler, completion, or effect work.

## 15. M20 — Application Boundary gaps

### M20.1 — Dependency direction and policy scope (`plan.md` §20)

`LoopRelay.Application` references owner contract assemblies. Owner assemblies do not reference
Application or CLI; temporary pre-M20 adapters live at the outer CLI/composition edge. The CLI
references Application contracts plus pure rendering/parsing only. Infrastructure implementations
are visible only to the composition root.

“Resolve configuration and policy once” means: parse/validate raw configuration and construct one
policy resolver once per composition; it does not mean reuse one global effective policy for all
attempts/sessions. Effective policy/runtime profiles are resolved and durably recorded at their
invocation/attempt/session scope using current inputs and provenance.

### M20.2 — Request/result and composition guarantees (`plan.md` §20)

Every application request has a correlation ID, explicit workspace/repository identity,
invocation mode/limits, policy overrides, and cancellation. Every result carries the exact typed
discriminant/reason, causal IDs when created, evidence links, warnings, pending effects,
recovery/interaction/action identities, snapshot identity, and suggested exit code. Sharing an
exit code never changes the discriminant.

Composition validation runs before workspace/provider work and reports all missing, duplicate,
or version-incompatible owner/registry dependencies. The production graph contains one
configuration source, policy resolver, validated catalog snapshot, exact-profile registry,
application service, and composition root. No alternate factory remains reachable.

## 16. M21 — Retirement completion gaps

### M21.1 — Verifier inputs and non-overridable output (`plan.md` §21)

The architecture verifier consumes the solution/project graph, executable/publish outputs,
production entrypoints, composition registrations, catalog snapshot, all owner registries from
G5, schema/migration/import manifests, prompt/generated asset manifests, public application claims,
and adapter-exhaustion facts. Its immutable result is keyed by commit, build, catalog, schema,
configuration, and exact-profile identities and calculates every final metric with itemized
offenders. There is no manual pass override.

Static calculation cannot prove “one plausible place to change behavior” by itself. Pair the
generated graph with the roadmap §5 owner walk-through; record each behavior family, owner,
production registration, implementation location, and zero alternate reachable locations in the
acceptance manifest.

### M21.2 — Deletion and platform evidence boundaries (`plan.md` §§21, 25)

Delete only after parity evidence and owner acceptance for the owning convergence milestone. Keep
canonical behavior tests and exact-provider compatibility fixtures; “exhausted compatibility
fixtures” refers only to retired legacy/import formats, not evidence still required to authorize a
provider capability. An imported workspace must run with its adapter absent/disabled, not merely
unused by the happy path.

The roadmap's current release aggregate lacks genuine Linux evidence. If the release continues to
claim Windows and Linux agreement, M21 requires genuine evidence for both and stable evidence IDs.
If Linux is no longer a claimed platform, change the release contract and aggregate expectation
explicitly; do not convert missing evidence into a pass. Both roadmap full chains, former-route
absence checks, adapter-disabled imports, unowned-asset and duplicate-owner sentinels, reduced
solution build/tests, and the privacy scan are required on the final deletion candidate.

## 17. Definition of done for this supplement

Each gap above is closed only when its detail is either encoded in executable contracts/tests or
superseded by an accepted ADR and corresponding plan/test update. This file is not an acceptance
artifact and does not itself authorize a milestone. When all gaps have executable resolution,
`.agents/plan.md` remains the execution sequence, the regenerated roadmap remains the program
authority, and the G3 manifest plus owner acceptance establishes architectural closure.
