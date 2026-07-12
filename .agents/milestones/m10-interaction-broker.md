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

