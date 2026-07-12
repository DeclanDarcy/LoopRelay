# M13 — Workflow Catalog


### Catalog model

- [ ] Replace `CanonicalWorkflowDefinitionSketches` with a single `CanonicalWorkflowCatalog` snapshot constructed once and injected everywhere. Its stable identity is a canonical hash over semantic declarations; its explicit version changes whenever semantics change. Persist catalog identity/version on root runs and workflow instances.

Extend declarations with typed, validated contracts for:

- [ ] workflow, stage, transition, product, product schema/version, entry/exit, successor, and terminal outcome;
- [ ] required input products and complete filesystem input surfaces;
- [ ] complete output surfaces with repository target (`Workspace`, nested `Agents`, or parent gitlink), mutation kind, ownership, validation, commit policy, and push policy;
- [ ] typed validator identities resolved from an owner registry, replacing ungoverned strings;
- [ ] prompt template identity, prompt-policy profile requirement, execution posture, resolved-policy requirements, and exact runtime capabilities;
- [ ] explicit interaction categories, effect categories, recovery strategies, and completion behavior; and
- [ ] gate requirements, warnings, conflicts, unsupported cases, and specific failure outcomes.

### Validation and derivation

- [ ] Validate unique identities, references, product schemas, graph reachability, cycles, stage/transition successors, entry/exit compatibility, all terminal paths, prompt assets, policy/profile requirements, runtime capabilities, validator ownership, and effect/recovery ownership.
- [ ] Require every disk read and write to be covered by a normalized repository-relative surface with no root escape.
- [ ] Derive blocking commit and required-asynchronous push effects from output surfaces. Workflow authors may declare domain mutations but may not repeat Git publication mechanics.
- [ ] Include derived effects in catalog identity and the production obligation ledger.
- [ ] Construct the snapshot once in the composition root. Kernel, resolver, prompt asset lookup, effect coordinator, and certification all consume that exact instance.
- [ ] Add a deterministic obligation enumerator over catalog, prompt assets, exact profiles, schema manifest, known risks, effects, products, and chains. A one-item semantic change must produce one stable changed obligation rather than reorder the denominator.

### Verification and exit gate

- [ ] Build an invalid-catalog corpus for dangling references, duplicate IDs, unsupported capabilities, unowned validator, missing input/output surface, output without generated publication effects, cycles, unreachable terminal, missing prompt asset, and ambiguous successor. Production startup must return all validation errors before workspace access. All four workflows and both chains resolve from one catalog, and adding a fixture workflow requires declarations/handlers only—no kernel branch.

### Canonical identity and stable obligation keys

Define a versioned canonical serialization for the fully derived catalog. Normalize Unicode and
repository-relative paths, use invariant enum/scalar encodings, sort maps and unordered sets by
stable identity, preserve order only where workflow semantics require it, exclude diagnostics and
process/type names, include referenced prompt/profile/schema/capability versions and structurally
derived effects, then compute SHA-256. Store both the catalog ID and explicit semantic version on
root runs and workflow instances.

Derive an obligation key from owner + obligation kind + stable semantic path/identity, not array
position or the whole catalog hash. Its content/version hash changes when its semantics change.
Adding one declaration therefore changes only affected obligations and does not renumber the
ledger.

### Active-version availability and failure ordering

On restart, an active run resolves the exact catalog ID/version it recorded. Keep accepted catalog
snapshots/declarations available for all active durable lineages, or require an explicit catalog
migration decision that proves semantic compatibility. Missing or mismatched identity is
`RecoveryRequired`/a specific unsupported state, never a silent upgrade. New root runs use the
current accepted snapshot.

Validator, handler, effect, recovery, interaction, and capability references resolve through the
unique owner registries. Catalog validation collects all deterministic, path-qualified errors,
orders them stably, and runs before workspace access or provider/process initialization. Surface
validation resolves repository target, normalized path, root escape, symlink ambiguity,
nested-repository topology, ownership, commit policy, and push policy. An output surface without
its derived publication obligations is invalid.
