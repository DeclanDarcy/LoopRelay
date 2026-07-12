# M10 — Interaction Broker


### Target design

Add `Interactions/` to `LoopRelay.Orchestration.Primitives` with:

- [ ] typed category and request IDs;
- [ ] question/presentation data separated from a versioned response JSON schema and schema hash;
- [ ] causal subject, creation evidence, resolved category policy, deadline/default policy, and required trust/authorization evidence;
- [ ] append-only Presented, Responded, Rejected, Expired, Defaulted, Cancelled, and Resolved events;
- [ ] immutable response IDs and semantic idempotency keys; and
- [ ] broker commands to create, list, show, respond, cancel, and resolve.

- [ ] The store persists a request before any renderer can present it. A semantically identical duplicate response returns the existing response; a conflicting duplicate, late response, or schema-invalid response is a typed rejection and leaves the request unchanged. Kernel/recovery resume only from a validated resolved-response fact.

### First production integration

- [ ] Replace the clean-input gate's current dirty-surface result for interactive invocations with a durable `DirtyInputCommitOffer` request that names the exact declared surface and Git evidence.
- [ ] On acceptance, create a scoped commit effect through M8; on rejection, return the specific dirty-input result. Never commit directly from the broker.
- [ ] In headless mode, return `DirtyInputSurface` immediately with no request, no commit, and no indefinite wait.
- [ ] Add application/CLI list, show, and respond paths. Workflow handlers must not read stdin or console.

### Persistence and tests

- [ ] Add request, response, lifecycle-event, and policy-evaluation tables linked to the causal spine.
- [ ] Test restart with an outstanding request; valid, invalid, late, expired, identical duplicate, and conflicting duplicate responses; compare-and-set conflicts; cancellation; headless behavior; and renderer purity.
- [ ] Add an architecture test rejecting console/input dependencies from workflow, kernel, recovery, effect, completion, storage, and import assemblies.

### Exit gate

- [ ] Every required human action has a stable request identity, exact response contract, visible policy, and restart-safe resolution. Status can name the action without relying on ephemeral console state.

### State-machine and category-policy details

Use the durable flow:

```text
Required -> Persisted -> Presented
Presented -> valid Responded -> Validated -> Resolved -> ResumeAuthorized
Presented -> Expired | Defaulted | Cancelled
invalid/late/conflicting response -> Rejected event; request state unchanged
```

Presentation is idempotent and can recur after restart. Record an immutable response only after
schema, request state, deadline, semantic idempotency, correlation, and compare-and-set checks
succeed. An identical semantic duplicate returns the existing response/resolution IDs; a different
duplicate appends a rejection and cannot replace the accepted response. Kernel or Recovery resumes
only from `ResumeAuthorized`, never directly from submitted JSON.

The registry reserves typed categories for dirty-input commit offer, import conflict, recovery
ambiguity, and completion ambiguity. Each category declares question/presentation version,
response JSON schema/hash, deadline behavior, allowed default, headless result, required
authorization/trust evidence, and resolver owner. M10 production-wires only the dirty-input offer;
later owners activate their categories without inventing another interaction store.

D6's timeout/default, isolation depth, and trust-evidence choices remain owner rulings. Until
accepted, use no hidden timeout/default and fail headless requests with the category-specific typed
result. For dirty input, headless mode creates no request and no commit; it returns
`DirtyInputSurface` with the declared surface and Git evidence.
