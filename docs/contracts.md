# Contracts

This document defines the Contract Oracle direction for Command Center. It is intentionally seeded during Milestone 0.2 before golden fixtures are added, because fixtures must observe an identified authoritative contract rather than whichever parallel representation currently happens to exist.

## Oracle Definition

The canonical contract authority is the serialized external shape of backend-owned projections and command results under the backend JSON configuration.

The Oracle is an observation and drift-detection mechanism. It does not own domain meaning, projection construction, transport behavior, resource state, or presentation mapping.

Contract truth flows as:

```text
backend authority
  -> projection or command result
  -> backend JSON serialization
  -> Oracle fixture or comparison
  -> generated or verified consumers
```

## Boundary Taxonomy

| Boundary | Contract responsibility |
| --- | --- |
| Domain | Owns semantic rules, lifecycle legality, eligibility, diagnostics, recovery meaning, and source facts. |
| Projection | Exposes backend-owned read models and command results without creating new meaning. |
| Contract | Defines the externally observable serialized shape of projections, command requests, command results, and error envelopes. |
| Serialization | Applies backend JSON naming, enum, null, collection, date, identifier, ordering, and compatibility rules. |
| Transport | Preserves request, response, status, null/empty values, unknown fields, and error payloads without interpretation. |
| Resource | Owns frontend loading, refresh, invalidation, stale-response, and mutation mechanics for a contract consumer. |
| Controller | Builds feature view models and action sequencing from resource state and authoritative backend facts. |
| Presentation | Maps typed facts to labels, color, layout, icons, accessibility text, and local interaction affordances. |
| Persistence | Stores repository data and internal records; persisted shapes are not external contracts unless exposed across a boundary. |
| Runtime | Scopes failures, partial data, absence, retries, and recovery without changing contract meaning. |

## Canonical Contract Identity Model

A contract identity is the stable architectural name for one externally observable request, response, event, stream event, error envelope, or command result shape. It names the boundary artifact that consumers may depend on; it is not the C# type name, Rust mirror name, TypeScript alias, endpoint path, Tauri command, fixture file, or UI feature that happens to observe it.

Contract identity exists so generation, compatibility review, fixture drift, and consumer migration can all point at the same artifact without allowing any downstream representation to become contract authority.

The canonical identity record for each contract must include:

| Field | Requirement |
| --- | --- |
| Contract identity | Stable human-readable name used by Oracle fixtures, manifests, generated artifacts, evidence, and compatibility records. |
| Category | Contract taxonomy category, such as public projection, command request, command response, event, streaming event, error envelope, diagnostics, health, or certification. |
| Owning authority | Backend domain service, projection service, endpoint, or shell-owned command that owns the source meaning. |
| Owning projection or command | Projection type, request DTO, command result, stream event, or primitive that is serialized externally. |
| Producer boundary | Backend route, Tauri command, event stream, or shell-owned surface that emits the contract. |
| Serialization authority | Backend HTTP JSON serialization unless explicitly shell-owned or stream-specific. |
| Version identity | Current accepted version label or explicit unversioned pilot status. Until mechanical versioning exists, fixture and freshness manifests are the observable baseline. |
| Compatibility obligations | Current manual consumers, compatibility fields, downstream drift, migration owner, and retirement condition. |
| Oracle source | Golden fixture, stream trace, request-boundary inventory, or explicit statement that no Oracle source exists yet. |
| Consumer set | Generated, verified, observational, transforming, testing, presentation, and compatibility consumers that depend on the identity. |
| Evolution rule | Required governance path for additive, breaking, compatibility, fixture, artifact, or request-boundary changes. |

Identity rules:

- The identity is assigned to the externally observable serialized shape, not to a downstream mirror or local UI model.
- A single backend projection may expose multiple contract identities only when producer boundaries, consumer sets, compatibility obligations, or version lifecycles differ.
- Multiple producer boundaries may share one contract identity only when they serialize the same authority-owned shape and accept the same compatibility obligations.
- Aggregation contracts are allowed only when the aggregation itself is an authority-owned projection with named source projections and consumer obligations.
- Request and response contracts are separate identities even when they are handled by the same endpoint or Tauri command.
- Error envelopes are contracts whenever structured error payloads cross backend, shell, or TypeScript transport boundaries.
- Stream contracts require an identity for the event payload and, when ordering/reconnection semantics matter, a separate stream lifecycle identity.
- Persistence and configuration shapes are not external contract identities unless they cross a runtime, API, generated-artifact, or compatibility boundary.
- Manual Rust mirrors, manual TypeScript aliases, and dev mocks are compatibility consumers or verified artifacts. They cannot define identity.

Initial version identity rule:

| State | Meaning | Allowed use |
| --- | --- | --- |
| Unversioned pilot | Contract is identified and protected by Oracle or request-boundary evidence, but mechanical versioning does not exist yet. | Phase 0 and M1.1 inventory records. |
| Fixture baseline | Contract has an accepted golden fixture or stream trace that acts as the observable version baseline. | Oracle-managed read models and command results before generated artifacts. |
| Freshness baseline | A verified artifact is tied to an Oracle source by hash. | Manual TypeScript artifacts during Phase 0; generated artifacts in M1.2 after generation exists. |
| Versioned contract | Contract has an explicit semantic or schema version with compatibility rules and generated artifact lifecycle. | Future M1.2 and later generated contract ecosystem work. |

## Contract Taxonomy

A contract category identifies why a serialized shape crosses a boundary and which rules govern its ownership, compatibility, verification, and evolution. The category does not make a downstream representation authoritative.

| Category | Definition | Primary authority | Current examples |
| --- | --- | --- | --- |
| Public projection response | Backend-owned read model exposed to the UI or shell as externally observable product state. | Backend projection or composition service. | Repository dashboard, repository workspace, workflow projection. |
| Internal projection response | Backend-owned read model exposed only to internal tooling, tests, diagnostics, or a non-product integration boundary. | Backend service that owns the projection. | Planning milestone projection until product consumers require public compatibility. |
| Command request | Serialized route, query, or body input accepted by a backend route or shell-owned command. | Backend endpoint/request DTO, except explicitly shell-owned commands. | Repository id route parameters, future POST command bodies. |
| Command response | Serialized result emitted after a command changes state or performs an action. | Backend command service and result projection. | Repository refresh result, execution start result, Git commit result, proposal review result. |
| Event | Discrete serialized fact emitted from a backend event or activity list. | Backend event producer. | Execution events endpoint payloads, reasoning event records. |
| Notification | Serialized transient message intended to inform a client without defining durable state. | Backend or shell runtime producer that owns the notification meaning. | Future UI notifications or runtime progress messages. |
| Streaming event | Serialized event payload delivered through a stream where ordering, reconnection, or lifecycle semantics may matter. | Backend stream producer and stream lifecycle owner. | Execution event stream entries. |
| Stream lifecycle | Contract for stream connection, ordering, terminal, retry, or reconnection behavior when those semantics are observable. | Backend stream endpoint and runtime isolation owner. | Future execution stream lifecycle identity. |
| Persistence | Stored shape that becomes a contract only when read across a runtime, generated-artifact, migration, or compatibility boundary. | Persisting domain service or storage adapter. | Reasoning repository records when exposed through reports or generated artifacts. |
| Configuration | Configuration shape that becomes a contract when edited, generated, imported, exported, or consumed across a process boundary. | Configuration owner. | Repository registration/configuration surfaces. |
| Diagnostics | Serialized health, warning, finding, or explanatory evidence emitted for troubleshooting or governance. | Service that owns the diagnostic meaning. | Continuity diagnostics, workflow diagnostics, governance findings. |
| Health | Serialized availability, readiness, liveness, or degradation state. | Health/status authority. | Ping primitive, workflow health, repository availability. |
| Certification | Serialized certification report or acceptance evidence. | Certifying service or governance mechanism. | Decision certification, workflow certification, reasoning certification. |
| Error envelope | Structured failure payload that crosses backend, shell, TypeScript transport, or runtime boundaries. | Boundary that creates the failure contract; backend for backend errors. | Backend error payload with `boundaryViolation`. |
| Compatibility artifact | Transitional downstream representation that is checked against a contract but cannot define it. | The upstream contract identity and compatibility owner. | Manual TypeScript types, Rust mirrors, dev Tauri mock payloads. |

Category rules:

- A public projection response must be fixture-eligible unless an evidence record explains why the response is not stable enough to baseline yet.
- A command request and command response are separate contracts even when they share one user action.
- A streaming event contract does not certify stream ordering or retry behavior unless a stream lifecycle identity also exists.
- Persistence and configuration shapes are not contract categories by storage location alone; they enter the contract taxonomy only when a boundary makes their shape externally observable.
- Diagnostics, health, and certification contracts may contain semantic conclusions, but those conclusions must be computed by the owning backend authority before serialization.
- Compatibility artifacts may be generated or verified, but they are never the source category for a contract identity.

## Projection And Contract Relationship Model

A projection is the backend-owned read model or command result before serialization. A contract is the externally observable serialized shape emitted from that projection or command result.

Projection-to-contract rules:

- One projection normally maps to one response contract at one producer boundary.
- One projection may expose multiple contract identities only when producer boundaries, consumer classes, compatibility obligations, or version lifecycles differ.
- Multiple endpoints or commands may share one contract identity only when they emit the same serialized shape from the same authority and accept the same compatibility obligations.
- An aggregation contract is allowed only when the aggregation is itself an authority-owned projection with named source projections and explicit consumer obligations.
- A route parameter, query parameter, body payload, response payload, event payload, stream lifecycle, and error envelope are distinct contract surfaces even when they are processed by the same endpoint.
- Compatibility fields inside a projection remain part of the serialized contract until retired; they must derive from authoritative structure and include owner, consumer list, replacement path, and retirement condition.

Allowed projection-to-contract transformations:

| Transformation | Allowed when |
| --- | --- |
| Backend JSON property naming | It is produced by the backend serialization authority. |
| Enum-to-string serialization | The backend JSON serializer emits the enum string and the semantic enum remains backend-owned. |
| Nullable value emission | The backend serializer emits explicit null or omission according to the accepted contract baseline. |
| Collection ordering | The projection owner defines ordering or the fixture records the current non-semantic order without treating it as a semantic guarantee. |
| Compatibility derivation | The compatibility field is derived upstream, documented, and regression-protected until retirement. |
| Aggregation | The aggregate projection names source authorities and does not let the consumer compute missing meaning. |

Forbidden projection-to-contract transformations:

| Transformation | Why forbidden |
| --- | --- |
| Rust, TypeScript, mock, hook, controller, or presentation code adding semantic fields | Downstream code would become semantic authority. |
| Transport converting null to omitted, omitted to null, empty array to null, or error payload to string-only failure | Transport would stop preserving the contract. |
| UI deriving eligibility, severity, health, retryability, lifecycle legality, certification status, or recommendation rank from weak strings | Presentation would become authority. |
| Generated artifacts silently renaming, dropping, widening, narrowing, or reordering contract fields without Oracle evidence | Generation would become an implicit contract authority. |
| A fixture update without authority, compatibility, consumer, verification, and rollback evidence | The Oracle baseline would move without governance. |

## Contract Ownership Matrix

Every contract identity must eventually name owners for these dimensions. A single service may own multiple dimensions, but each dimension must remain explicit.

| Ownership dimension | Owner | Owns | Does not own |
| --- | --- | --- | --- |
| Semantic ownership | Domain or backend service that computes meaning. | Lifecycle legality, eligibility, severity, health, diagnostics, recovery, certification, and source facts. | JSON naming, UI labels, transport mechanics, generated artifact location. |
| Shape ownership | Projection service, command result, request DTO, event producer, or error envelope producer. | Field membership, nesting, required/null states, collection shape, and compatibility fields. | Presentation grouping or downstream convenience aliases. |
| Serialization ownership | Backend HTTP JSON configuration unless explicitly shell-owned or stream-specific. | Property names, enum representation, date/time representation, null emission, primitive JSON kinds. | Semantic interpretation or consumer migration. |
| Compatibility ownership | Named compatibility owner for each temporary field, mirror, route, command, or artifact. | Consumer list, replacement path, retirement condition, and compatibility regressions. | Redefining source semantics or weakening Oracle truth. |
| Version ownership | Contract lifecycle owner established by M1.2 versioning or, before then, fixture/freshness baseline owner. | Version identity, additive/breaking classification, fixture baseline, generated artifact freshness. | Domain meaning unless also the semantic owner. |
| Evolution ownership | Contract governance decision owner for the affected contract identity. | Approval path for additive, breaking, migration, fixture, artifact, request, stream, and error-envelope changes. | Bypassing evidence requirements. |
| Deprecation ownership | Compatibility owner plus affected authority owner. | Deprecation notice, migration target, retirement criteria, rollback path, and final removal evidence. | Silent deletion while consumers remain. |
| Consumer ownership | Generated, verified, observational, transforming, testing, presentation, or compatibility consumer owner. | Correct use of the contract and local adaptation within allowed boundaries. | Contract identity, semantics, serialization, or compatibility obligations. |

Ownership rules:

- Shape ownership follows the serialized producer, not the most convenient downstream consumer.
- Serialization ownership is singular for a contract identity. If shell serialization differs from backend serialization, the shell surface must be classified as shell-owned or quarantined.
- Compatibility ownership is required before a compatibility field, manual mirror, or manual artifact can be accepted as transitional debt.
- Version ownership cannot be delegated to fixture filenames, TypeScript aliases, Rust structs, or mocks.
- Deprecation is not complete until consumers are migrated or retired and the removal is regression-protected.

## Consumer Model

Consumers are classified by how they depend on a contract. A consumer may belong to more than one class, but its permissions are limited by the most restrictive class involved.

| Consumer class | Definition | Allowed ownership | Forbidden ownership |
| --- | --- | --- | --- |
| Generated consumer | Artifact deterministically produced from the canonical contract model or Oracle source. | Local generated representation and regeneration evidence. | Manual semantic edits or independent compatibility policy. |
| Verified consumer | Manual artifact checked against Oracle-observed truth. | Local representation while freshness and consumer verification pass. | Contract authority or unreviewed shape divergence. |
| Observational consumer | Test, report, or tool that observes contract behavior without producing product behavior. | Evidence capture and drift reporting. | Changing contract meaning or compatibility obligations. |
| Transforming consumer | Adapter that maps a contract into another local format for a bounded purpose. | Mechanical mapping that preserves source facts and records lossiness. | Semantic inference or silent data loss. |
| Testing consumer | Unit, integration, characterization, fixture, or E2E test that depends on a contract. | Regression evidence and expected behavior. | Defining shape independently of Oracle truth. |
| Presentation consumer | UI component, display adapter, or accessibility mapper. | Labels, colors, icons, layout, grouping, local affordances, and accessibility text. | Eligibility, severity, health, lifecycle legality, recovery, certification, or recommendation meaning. |
| Compatibility consumer | Transitional mirror, mock, manual type, route, command, or field maintained for migration. | Temporary shape preservation with owner, risk, replacement path, and retirement condition. | Becoming permanent contract authority. |

Consumer rules:

- Generated and verified consumers may fail a build when stale; they may not update the contract by being updated first.
- Transforming consumers must identify whether they preserve unknown fields, drop unsupported fields, or create a lossy view.
- Presentation consumers may adapt display, but missing backend semantics must be fixed in the owning projection rather than inferred in React.
- Compatibility consumers are allowed only while they have an explicit retirement path or blocking condition.

## Contract Stability Model

Contract stability defines which externally observable properties are part of contract identity and which properties may vary without redefining the contract. Stability is distinct from versioning and compatibility: versioning names a lifecycle state, compatibility describes consumer obligations, and stability decides whether an observable change is identity-significant.

Stability classes:

| Stability class | Definition | Identity impact |
| --- | --- | --- |
| Identity-bearing | Property or behavior that consumers may rely on as part of the contract's architectural meaning. | Changing it requires a governed contract change and may require a new identity or version. |
| Additive stable | New optional field, enum member, diagnostic item, metadata entry, or collection member that preserves existing meaning. | May remain the same identity only after compatibility review and fixture or artifact evidence. |
| Observational metadata | Evidence, timestamps, hashes, durations, counts, trace identifiers, or diagnostic details that describe production or verification context without changing product meaning. | Does not create a new identity when shape and nullability remain governed and consumers treat it as metadata. |
| Intentionally unstable | Runtime ordering, transient progress, elapsed time, volatile diagnostic text, or environment-specific values explicitly marked unstable. | Does not create a new identity when instability is documented and consumers do not depend on exact values. |
| Compatibility transitional | Deprecated field, alias, mirror, command, or route kept for a named consumer during migration. | Remains in the current identity until retired; removal requires deprecation evidence and consumer migration proof. |
| Breaking | Field removal, requiredness change, type-kind change, semantic reassignment, enum narrowing, status/null/empty behavior change, error-envelope loss, request argument change, or stream lifecycle change. | Requires governance and either a new identity, new version, or explicit compatibility bridge. |

Identity-bearing properties include:

- contract category, producer boundary, owning authority, serialization authority, and request/response/event/error role;
- field names, field JSON value kinds, required versus optional presence, explicit nullability, collection shape, and nested object structure;
- enum domain and semantic meaning, including lifecycle, health, severity, eligibility, retryability, certification, and recovery values;
- request argument names, route/query/body location, requiredness, accepted primitive kinds, and command body structure;
- response success shape, structured error envelope shape, status semantics, and null/empty semantics;
- stream payload shape when streamed, plus ordering, terminal, retry, and reconnection semantics when a stream lifecycle identity exists.

Properties that may change without changing identity:

- additional optional fields after compatibility review;
- additional enum values only when the consumer contract explicitly requires unknown-value tolerance or a compatibility decision names affected consumers;
- additional diagnostic or evidence entries when consumers are required to preserve or render them generically;
- non-semantic ordering where the contract explicitly states ordering is unstable;
- observational timestamps, durations, trace ids, hashes, and environment paths when consumers do not use them as semantic facts.

Stability rules:

- Do not equate every JSON value change with a contract identity change. Fixtures observe shape and representative values; stability decides architectural significance.
- Do not treat fixture approval as permission to weaken identity-bearing semantics. The authority and compatibility review must precede fixture movement.
- A value may be observational metadata only when the producer documents it as such and consumers are not using it for eligibility, severity, health, lifecycle, recovery, certification, or recommendation decisions.
- Intentionally unstable values must be isolated so generators, fixtures, and regressions can ignore or normalize only the unstable portion without ignoring identity-bearing shape.
- Compatibility fields are stable while present. Their deprecation path may be temporary, but consumers may rely on the documented compatibility behavior until removal is accepted.

## Contract Normalization Rules

Normalization defines the canonical serialized representation that producers emit and consumers may depend on. These rules describe the model before M1.2 generation; they do not introduce a generator or broaden fixture coverage by themselves.

| Topic | Canonical representation | Allowed producer variation | Consumer guarantee | Compatibility evolution |
| --- | --- | --- | --- | --- |
| Identifiers | Stable strings for external ids; GUID-backed ids use canonical string form at JSON boundaries. | Backend may store richer id types internally. Route ids, body ids, and response ids must serialize consistently for the same identity. | Consumers treat ids as opaque equality keys unless the contract states a typed semantic role. | Changing id kind, formatting, or scope is breaking unless a new field or compatibility alias is provided. |
| Names | JSON property names follow the backend serialization authority. Contract names use stable architectural identity names, not C# or TypeScript implementation names. | Internal type names may differ from external property names. | Consumers depend on serialized property names, not implementation symbols. | Renames require additive replacement, deprecation, consumer migration, and removal evidence. |
| Enums | Semantic enums serialize as strings from backend authority. Values are domain facts, not presentation labels. | Producers may use internal enum types or validated strings if the authority owns the domain. | Consumers may switch on known values only within the documented enum domain and must not invent semantic fallbacks. | New values require compatibility review; removed, renamed, or semantically reassigned values are breaking. |
| Dates and times | Instants crossing external boundaries serialize as explicit date/time strings under backend JSON configuration. Date-only, duration, and elapsed-time values must be named as such. | Internal storage may use domain-specific time types. | Consumers preserve the serialized value and may display it, but cannot infer lifecycle or freshness semantics unless exposed separately. | Changing time zone, precision, or instant/date/duration meaning is breaking unless a new field carries the new meaning. |
| Optional values | Absence, explicit `null`, empty string, empty object, and empty array are distinct contract states. | A producer may omit optional fields only when the accepted contract identity says omission is allowed. | Consumers may rely on the documented distinction and transport must preserve it. | Changing omitted to null, null to omitted, empty to null, or null to empty is breaking unless compatibility evidence proves no consumer depends on the distinction. |
| Collections | Arrays represent ordered or unordered collections as documented by the projection owner. Empty arrays represent known empty collections. | Producers may choose internal collection types. | Consumers may rely on array shape and item kind; ordering is semantic only when the contract says it is. | Item shape changes follow nested field rules; ordering changes are breaking only for semantic ordering contracts. |
| Metadata | Metadata is auxiliary context that must not carry hidden domain meaning. Metadata fields must be named so consumers know whether values are observational, diagnostic, evidence, or compatibility data. | Producers may add metadata when it is clearly non-authoritative or authority-owned. | Consumers may render or preserve metadata but cannot promote it to semantic authority. | New metadata is additive when it remains optional and non-semantic; semantic metadata requires authority and compatibility review. |
| Ordering | Semantic ordering must be produced by backend authority and documented. Non-semantic ordering must be marked unstable or fixture-only. | Producers may emit deterministic internal order for convenience only when it is not documented as semantic. | Consumers may not infer rank, priority, recency, or recommendation order from undocumented order. | Making order semantic requires a contract change; removing semantic order is breaking. |
| Evidence | Evidence items include source, basis, confidence, trace, or verification context and must remain attached to the authority that produced the conclusion. | Producers may include richer evidence entries as optional additive metadata. | Consumers may display or link evidence, but the conclusion remains in the authoritative field. | Removing evidence required for certification or governance is breaking for that contract category. |
| Diagnostics | Diagnostics serialize typed severity, code/category, message, source, affected target, and recovery guidance when available. | Diagnostic prose may vary when code/category/severity remain stable and the contract marks prose as observational. | Consumers must use typed diagnostic fields for behavior and presentation mapping; they may not parse prose for meaning. | Severity/code/category changes require authority review; additional findings are additive. |
| Compatibility fields | Compatibility fields derive from authoritative structure and carry owner, consumer list, replacement path, retirement condition, and regression evidence. | Producers may expose aliases only while compatibility ownership exists. | Consumers may rely on the field until its documented retirement condition is met. | Removal requires deprecation evidence, migrated consumers, rollback path, and regression protection. |
| Error envelopes | Structured errors preserve status, error text, boundary violation details, nulls, and unknown fields across transport. | Boundary-specific producers may add details if the envelope remains structured. | Consumers receive typed failure context instead of string-only loss. | Dropping structured error data, changing status meaning, or flattening errors is breaking. |
| Streams | Stream event payloads follow event contract normalization; lifecycle semantics are separate when observable. | Transport may frame events, but payload semantics remain backend-owned. | Consumers may depend on payload shape and documented lifecycle semantics only. | Ordering, retry, terminal, or reconnection changes require stream lifecycle governance when observable. |

Normalization rules:

- Backend JSON serialization is the default normalization authority for externally observable backend contracts.
- Shell-owned commands may define their own normalization only when classified as shell-owned and documented as a separate contract identity.
- Generated artifacts must encode these normalization rules mechanically in M1.2; until then, verified manual artifacts are compatibility consumers.
- Consumers must preserve unknown fields when acting as transport or compatibility relays and must declare lossiness when transforming into local view models.
- A normalization exception requires a decision record naming the invariant, affected identity, consumers, compatibility path, and rollback rule.

## Boundary Semantics

Boundary semantics define what each architectural boundary may transform after normalization. They prevent contract ownership from moving downstream during generation, transport migration, resource extraction, controller extraction, and presentation normalization.

| Boundary | May transform | Must preserve | Must not do |
| --- | --- | --- | --- |
| Domain to projection | Domain facts into authoritative read models, command results, diagnostics, eligibility, recovery, health, and certification conclusions. | Semantic meaning, lifecycle legality, source facts, and authority ownership. | Serialize for a specific client or embed presentation-only labels as authority. |
| Projection to contract | Backend-owned shape into externally observable serialized shape under normalization rules. | Field membership, null/empty semantics, enum meaning, collection semantics, compatibility fields, and structured errors. | Add downstream convenience meaning or drop authority fields without governance. |
| Contract to transport | Request, response, status, nulls, empty values, unknown fields, and structured error payloads. | Exact contract payload and boundary failure context. | Parse domain meaning, collapse errors, coerce null/omitted/empty values, or become compatibility authority. |
| Transport to resource | Payload delivery into loading, refresh, invalidation, stale-response, mutation, and error mechanics. | Contract data, structured failures, request identity, repository identity, and stale-response ordering. | Infer eligibility, severity, health, lifecycle, recovery, certification, or recommendation rank. |
| Resource to controller | Resource state into feature action sequencing and feature view models. | Authoritative backend facts and typed resource failure states. | Create new backend semantics or duplicate mutable authority across features. |
| Controller to workspace | Feature view models and actions into workspace composition and local interaction flow. | Feature ownership, controller boundaries, and scoped failure behavior. | Cross-wire unrelated workspace state or make the workspace root a semantic authority. |
| Workspace to presentation | Typed facts into layout, labels, icons, colors, grouping, accessibility text, and local affordances. | Authoritative semantic fields and local interaction state ownership. | Parse weak strings or infer missing backend meaning from display text, order, labels, or style. |

Request boundary rules:

- Route, query, body, header, and command arguments are separate request surfaces even when one endpoint handles them.
- Requiredness, nullability, defaulting, and validation ownership must be explicit for each request field.
- Client wrappers may adapt naming from TypeScript call style to Tauri command arguments, but they may not add fields, defaults, or semantic validation not owned by the backend or shell-owned command.
- Backend request DTOs and route constraints own accepted request shape for backend-owned routes.

Response boundary rules:

- Success response shape, no-content semantics, explicit null response, empty collections, and partial data must be documented as distinct states when observable.
- A command response that reports eligibility, skipped work, partial success, or recovery guidance must expose those facts as typed backend-owned fields.
- Compatibility aliases in responses must be derived upstream and protected until retired.

Error boundary rules:

- Error envelopes are contracts when structured error data crosses a boundary.
- Transport must preserve structured error payloads and status context without reducing them to message-only failures.
- Runtime boundaries may add local failure context only outside the authoritative backend error payload or in a documented wrapper field.

Stream boundary rules:

- Event payload contracts and stream lifecycle contracts are distinct.
- Ordering, terminal events, retry behavior, reconnection cursor semantics, heartbeat behavior, and partial replay are lifecycle semantics when observable.
- A stream consumer may not infer lifecycle guarantees from transport framing unless the lifecycle identity documents them.

## Contract Evolution Model

Contract evolution is the governed change process for one contract identity. It describes what changed, whether identity changes, whether existing consumers remain compatible, whether version state changes, and what evidence is required before the Oracle, generated artifacts, verified artifacts, or compatibility consumers move.

Evolution operations:

| Operation | Identity impact | Compatibility impact | Version impact | Consumer action | Required governance and evidence |
| --- | --- | --- | --- | --- | --- |
| Additive field | Same identity when optional, normalized, authority-owned, and reviewed. | Compatible only when existing consumers tolerate unknown fields or the compatibility record names affected consumers. | Baseline or minor-compatible version update once versioning exists. | Generated consumers regenerate; verified consumers refresh or record reviewed additive tolerance. | Authority owner, consumer list, fixture action, artifact action, rollback path, and additive-field evidence. |
| Deprecated field | Same identity while the field remains present and derives from authority-owned structure. | Compatible during the documented deprecation window. | Deprecation marker or compatibility lifecycle update. | Consumers migrate to the replacement field before retirement. | Compatibility owner, replacement path, retirement condition, affected consumers, regression proving derivation, and rollback path. |
| Removed field | Breaking for any identity-bearing or compatibility field. | Incompatible unless all consumers are migrated or a bridge remains. | Breaking version or new identity unless removal is proven non-observable. | Consumers must migrate or retire before removal is accepted. | Deprecation completion evidence, migrated-consumer evidence, fixture retirement/update, artifact regeneration, and rollback plan. |
| Renamed field | Breaking unless implemented as additive replacement plus deprecated alias. | Compatible only while both names are emitted or consumers are migrated. | Compatibility lifecycle update first; breaking version only after alias retirement if required. | Consumers move from old name to new name through the documented path. | Rename decision, alias derivation evidence, consumer migration list, retirement condition, and tests for old and new names during transition. |
| Semantic reinterpretation | Usually identity-bearing and breaking even when JSON shape is unchanged. | Incompatible when consumers use the old meaning. | Breaking version or new identity when the old and new meanings cannot coexist. | Consumers must be reviewed for meaning, not only shape. | Authority decision, semantic diff evidence, consumer behavior review, fixture rationale, and rollback to prior meaning. |
| Representation normalization | Same identity only when meaning, requiredness, nullability, and consumer guarantees remain equivalent. | Compatible only when consumers tolerate both forms or a bridge exists. | Baseline update or breaking version depending on observable effect. | Consumers may need parser or generated artifact updates. | Normalization decision, before/after serialized evidence, compatibility review, and transport preservation proof. |
| Contract split | New identities for the split outputs unless each output preserves the original complete contract. | Incompatible for consumers expecting the original aggregate unless an aggregate compatibility contract remains. | New version or new identities. | Consumers choose the new identity or remain on a compatibility aggregate. | Source authority mapping, consumer routing plan, compatibility aggregate owner, fixtures for each output, and rollback to original aggregate. |
| Contract merge | New identity unless one contract becomes a pure additive extension of another accepted identity. | Incompatible when field ownership, null semantics, ordering, or lifecycle obligations differ. | New version or new identity. | Consumers must validate that merged semantics do not change feature behavior. | Authority composition record, source identity mapping, conflict resolution evidence, fixtures, and rollback to separate contracts. |
| Projection split | Contract identity may stay stable only when the externally observable shape and authority obligations remain unchanged. | Compatible when producer boundary preserves the accepted shape. | No version change for preserved contract; new identities for new exposed shapes. | Consumers should not observe internal projection split unless adopting new identities. | Projection ownership evidence, aggregation or forwarding proof, fixture stability, and regression against downstream shape drift. |
| Projection aggregation | New identity when aggregation creates a new externally observable read model. | Compatible only if existing identities remain available or consumers migrate. | New identity or versioned aggregate. | Consumers migrate only after aggregation authority and source projections are documented. | Aggregation authority, source projection list, conflict rules, fixture evidence, and rollback to source contracts. |
| Compatibility-only alias | Same identity while alias derives from authority-owned structure and has a retirement path. | Compatible for named consumers only. | Compatibility lifecycle update, not a semantic version by itself. | Named consumers migrate to the authoritative field. | Owner, consumer list, replacement path, retirement condition, derivation regression, and removal evidence. |

Evolution rules:

- Classify the operation before changing fixtures, generated artifacts, verified artifacts, shell mirrors, TypeScript types, or mocks.
- Shape-compatible changes can still be breaking when they reinterpret semantics, null/empty states, status meaning, lifecycle legality, recovery guidance, severity, health, eligibility, certification, or stream lifecycle behavior.
- Additive changes are not automatically accepted. They need compatibility evidence because verified manual consumers, Rust mirrors, dev mocks, and presentation code may not preserve unknown fields.
- A projection refactor is not a contract change when the same authority-owned serialized shape and obligations remain intact.
- A contract identity may be split, merged, or versioned only through a decision that names affected consumers and the rollback path.

## Contract Compatibility Model

Compatibility is the obligation to keep existing consumers valid while a contract evolves. It is separate from identity and stability: an identity can remain the same while a consumer becomes stale, and a breaking identity change can be made temporarily compatible through a governed bridge.

Compatibility states:

| State | Meaning | Acceptance rule |
| --- | --- | --- |
| Compatible baseline | Existing consumers, fixtures, and verified artifacts match the accepted contract identity. | May be accepted with current Oracle, consumer, freshness, and request-boundary evidence. |
| Reviewed additive | Producer emits an additive field or value that has been reviewed but not yet adopted everywhere. | Requires affected-consumer inventory, fixture or reviewed-addition evidence, and artifact action. |
| Bridged compatibility | A deprecated field, alias, route, command, or mirror keeps old consumers working while the authoritative replacement exists. | Requires owner, consumer list, replacement path, retirement condition, derivation regression, and rollback rule. |
| Consumer-stale | Downstream artifact or mirror no longer matches Oracle truth. | Oracle truth must not weaken; migrate, regenerate, refresh, or quarantine the consumer. |
| Incompatible accepted | A breaking change is accepted because consumers are migrated, retired, or intentionally versioned. | Requires decision evidence, migrated-consumer proof, version or identity action, and rollback readiness. |
| Quarantined exception | Compatibility cannot be resolved in the slice but is bounded and tracked. | Requires owner, reason, risk, retirement criteria, compensating regression, and follow-up milestone. |

Compatibility obligations:

- Every compatibility field, alias, route, command, mirror, or manual artifact must name its owner, consumers, replacement path, retirement condition, evidence location, and rollback path.
- A compatibility bridge must derive from authoritative structure. A bridge that invents meaning is an authority violation, not a compatibility mechanism.
- Verified manual artifacts are compatibility consumers until generated artifacts replace them. Their freshness and consumer verification protect the current baseline but do not make them contract authority.
- Dev mocks are compatibility consumers and development fixtures. They may preserve contract shape for local development but must not become independent product truth.
- Shell Rust mirrors are transitional compatibility artifacts unless the command is explicitly shell-owned. Passive transport must preserve backend JSON without interpreting domain meaning.
- Compatibility removal is complete only after consumers are migrated or retired, regressions prove the replacement path, and rollback behavior is documented.

Compatibility review questions:

- Which contract identity and evolution operation are involved?
- Which producer boundary emits the change?
- Which generated, verified, observational, transforming, testing, presentation, and compatibility consumers are affected?
- Does the change alter request shape, response shape, error envelope, stream payload, stream lifecycle, null/empty semantics, ordering, or semantic meaning?
- Can old and new consumers coexist through an upstream-derived bridge?
- Which fixture, artifact freshness manifest, consumer verifier, request-boundary verifier, or architecture regression proves the accepted state?

## Contract Governance Model

Contract governance is the decision path that prevents contract authority from moving downstream while M1.1 remains model-first and before M1.2 generation exists.

Governance sequence:

1. Identify the contract identity, category, owner, producer boundary, serialization authority, and consumer set.
2. Classify the observed or planned change using the stability model and evolution operation table.
3. Decide whether the change preserves identity, requires a version action, or requires a new identity.
4. Review compatibility impact for generated, verified, observational, transforming, testing, presentation, and compatibility consumers.
5. Choose fixture, artifact, request-boundary, stream, or error-envelope actions.
6. Record decision evidence, rollback path, and affected durable documentation.
7. Update producers before downstream consumers when the backend authority is changing.
8. Refresh generated or verified consumers only after the accepted contract baseline is established.
9. Run the relevant Oracle, consumer verification, artifact freshness, request-boundary, and architecture-regression mechanisms.
10. Update capability, evidence, and handoff records with known limits and next work.

Governance rules:

- Contract decisions must identify semantic owner, shape owner, serialization owner, compatibility owner when applicable, evolution owner, affected consumers, evidence package, and rollback path.
- A fixture update without authority and compatibility evidence is not an accepted contract change.
- A generated artifact, Rust mirror, TypeScript type, dev mock, hook, controller, workspace, or presentation component may not authorize a contract change.
- A shell-owned command can define a contract only when classified as shell-owned and documented separately from backend-owned transport.
- Request, response, error, and stream lifecycle changes must be governed independently because their compatibility risks differ.
- Governance evidence may be procedural in M1.1, but M1.2 and later mechanisms must make generation, versioning, freshness, and drift checks executable.

## M1.1 Accepted Generation Boundary

Milestone 1.1 is accepted and baselined as the canonical contract model foundation for Milestone 1.2 generated contract ecosystem work. Acceptance evidence is recorded in `.agents/milestones/m1.1-canonical-contract-model-acceptance-baseline-slice-0065.md`; model-complete certification evidence is recorded in `.agents/milestones/m1.1-canonical-contract-model-certification-slice-0064.md`.

The accepted M1.1 boundary owns:

- contract identity meaning;
- contract taxonomy categories;
- semantic, shape, serialization, compatibility, version, evolution, deprecation, and consumer ownership rules;
- normalization rules for identifiers, enums, dates, optional values, collections, names, metadata, ordering, evidence, diagnostics, and compatibility fields;
- allowed and forbidden transformations at request, response, error-envelope, stream, transport, generated-artifact, and presentation boundaries;
- compatibility, deprecation, fixture movement, artifact movement, and rollback governance;
- canonical conformance examples for repository, workflow, decision, execution, reasoning, continuity, governance, health, and certification families.

Milestone 1.2 may implement:

- intermediate representation shape;
- schema or manifest extraction mechanics;
- generator location and invocation;
- generated artifact headers and freshness manifests;
- deterministic artifact checks;
- generated TypeScript, mock, shell metadata, or fixture pilot order;
- consumer adoption and rollback mechanics for generated artifacts.

M1.2 may not redefine contract identity, taxonomy, ownership, normalization, compatibility, versioning, or governance through generator implementation. If generation exposes a defect in the accepted model, M1.1 must be reopened through decision governance with evidence that names the defective model rule, affected consumers, rollback path, and required documentation change.

## Canonical Contract Examples

These examples are conformance cases for the M1.1 model. They show how current contract families must be described before M1.2 generation can produce artifacts without inventing architectural rules. They are not fixture expansion, generated artifacts, endpoint certification, or consumer migration.

Each example must be read through the identity, taxonomy, ownership, normalization, stability, compatibility, evolution, and governance models above. If a later implementation slice cannot classify a contract family using these dimensions, the model must be amended through governance before generation or migration proceeds.

### Repository Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Repository dashboard and repository workspace. |
| Category | Public projection response. |
| Authoritative source | Repository authority plus backend-owned execution, continuity, reasoning, artifact, and decision-session summary authorities composed by Middle projections. |
| Shape owner | `RepositoryDashboardProjection[]` and `RepositoryWorkspaceProjection` producers. |
| Serialization owner | Backend HTTP JSON serialization. |
| Producer boundary | `GET /api/repositories` and `GET /api/repositories/{repositoryId}/workspace`, with Tauri commands acting as transport consumers. |
| Version identity | Fixture baseline and unversioned pilot until M1.2 versioning exists. |
| Normalization focus | Repository identifiers, paths, names, availability states, nullable summaries, ordered collections, and nested summary objects preserve backend JSON shape. |
| Stability classification | Repository identity and summary field membership are identity-significant; UI grouping and labels are presentation-local. |
| Compatibility policy | Rust mirrors, manual TypeScript types, and dev mocks are compatibility consumers. Known Rust `decisionSessionSummary` drift remains consumer drift, not Oracle truth. |
| Governance constraint | Additive or renamed repository summary fields require compatibility review before fixture or artifact movement because manual consumers may not preserve unknown fields. |

### Workflow Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Primary workflow projection. |
| Category | Public projection response with related diagnostics, health, and certification sibling contracts when those reports cross boundaries. |
| Authoritative source | Workflow state machine, workflow projection service, gate, recovery, continuation, preparation, health, and certification authorities. |
| Shape owner | `WorkflowInstance` for the primary projection; sibling command results or reports own their own identities. |
| Serialization owner | Backend HTTP JSON serialization. |
| Producer boundary | `GET /api/repositories/{repositoryId}/workflow`; Tauri `get_workflow_projection` remains passive through `serde_json::Value`. |
| Version identity | Fixture baseline and unversioned pilot for the primary projection. |
| Normalization focus | Stage, lifecycle state, eligibility booleans, ordered timeline, gate diagnostics, explicit nulls, empty diagnostic collections, and flattened compatibility fields. |
| Stability classification | Stage, legality, eligibility, diagnostics, recovery, health, and certification facts are semantic and identity-significant when externally observable. |
| Compatibility policy | Manual TypeScript workflow type is a verified compatibility artifact; absent dev mock coverage is a known gap, not an alternate authority. |
| Governance constraint | UI may not infer lifecycle legality, retryability, recovery, or health from weak strings. Missing workflow semantics must be added upstream. |

### Decision Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Decision lifecycle eligibility, proposal browser/review, and decision governance or quality report contracts. |
| Category | Public projection response, command request, command response, diagnostics, and certification depending on the boundary. |
| Authoritative source | Decision discovery, candidate, proposal, review, refinement, resolution, governance, quality, execution influence, and certification services. |
| Shape owner | Decision endpoint request DTOs, proposal/review projections, lifecycle eligibility projections, and certification report results. |
| Serialization owner | Backend HTTP JSON serialization. |
| Producer boundary | `DecisionEndpoints` routes and corresponding Tauri decision commands. |
| Version identity | Unversioned pilot until fixtures and generated artifacts are introduced for each selected identity. |
| Normalization focus | Decision identifiers, proposal lifecycle states, eligibility and blocked-state reasons, recommendation evidence, option comparison, lineage, ratings, findings, and certification results. |
| Stability classification | Eligibility, blocked-state, recommendation rank, severity, review legality, and certification status are semantic facts, not presentation facts. |
| Compatibility policy | Manual TypeScript unions and mock data may preserve shape temporarily; they may not define lifecycle legality or recommendation meaning. |
| Governance constraint | Any new UI-local eligibility or severity computation is an authority violation unless explicitly classified as non-persistent preview behavior through governance. |

### Execution Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Execution context, execution session summary/status, execution command result, prompt manifest, handoff result, and execution event stream. |
| Category | Public projection response, command request, command response, event, streaming event, and stream lifecycle where ordering or reconnection is observable. |
| Authoritative source | Execution context, prompt building, provider execution, monitoring, handoff, recovery, Git status, commit, and push services. |
| Shape owner | Execution endpoint request DTOs, command result projections, session projections, event records, and stream lifecycle producer. |
| Serialization owner | Backend HTTP JSON serialization for request/response payloads; stream-specific serialization for event streams when applicable. |
| Producer boundary | `ExecutionEndpoints` and `ExecutionSessionsEndpoints`, including stream routes and Tauri execution commands. |
| Version identity | Unversioned pilot until fixture, stream trace, and generated artifact baselines exist. |
| Normalization focus | Session identity, execution state, provider result state, prompt manifest references, transparency fields, ordered events, nullable handoff data, and Git action results. |
| Stability classification | Status, retryability, terminal state, event ordering guarantees, and failure scope are identity-significant when observable. |
| Compatibility policy | Rust request/result mirrors and manual TypeScript execution types are compatibility artifacts until generated or passively transported. |
| Governance constraint | Stream event payload identity and stream lifecycle identity must be governed separately when reconnection, ordering, terminal, or retry behavior becomes observable. |

### Reasoning Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Reasoning graph, report, event, trace, query, reconstruction, materialization review, and certification contracts. |
| Category | Public projection response, event, diagnostics, persistence-crossing contract, and certification depending on the boundary. |
| Authoritative source | Reasoning event, thread, relationship, graph, trace, query, reconstruction, materialization review, and certification services. |
| Shape owner | Reasoning projections, records, reports, and endpoint command results. |
| Serialization owner | Backend HTTP JSON serialization for external projections and reports. |
| Producer boundary | `ReasoningEndpoints` and corresponding Tauri reasoning commands. |
| Version identity | Unversioned pilot until representative graph/report fixtures or traces exist. |
| Normalization focus | Schema versions, reasoning record identity, thread and relationship identifiers, trace direction, boundary violation details, diagnostics, report history, and ordered graph elements. |
| Stability classification | Schema version, relationship meaning, trace direction, boundary violation semantics, and certification status are identity-significant. |
| Compatibility policy | Stored reasoning records become contracts only when exposed across runtime, generated-artifact, migration, or compatibility boundaries. |
| Governance constraint | Persistence shape cannot silently become external contract authority; exposed reasoning records need explicit identity and compatibility review. |

### Continuity Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Continuity diagnostics, report history, operational-context proposal, compression, assimilation, and review command contracts. |
| Category | Public projection response, command request, command response, diagnostics, and report contract. |
| Authoritative source | Operational-context parsing, lifecycle, compression, semantic diff, diagnostics, and report services. |
| Shape owner | Continuity endpoint projections, operational-context proposal projections, review request DTOs, and report command results. |
| Serialization owner | Backend HTTP JSON serialization. |
| Producer boundary | `ContinuityEndpoints` and `OperationalContextEndpoints`, with corresponding Tauri commands. |
| Version identity | Unversioned pilot until diagnostics/report fixtures and generated artifacts exist. |
| Normalization focus | Diagnostic groups, severity, trend, markdown content, semantic change summary, proposal state, null/empty report behavior, and ordered report history. |
| Stability classification | Diagnostic severity, trend, proposal lifecycle, assimilation result, and report availability are semantic facts. |
| Compatibility policy | Dev mock and manual TypeScript continuity shapes are compatibility consumers while verified or generated coverage is absent. |
| Governance constraint | Presentation may format markdown and diagnostics, but it may not infer continuity severity, trend, or assimilation legality. |

### Governance Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Decision governance snapshot, decision-session governance snapshot, quality report, compatibility exception report, and architecture decision evidence report. |
| Category | Diagnostics, certification, public projection response, and command response depending on the boundary. |
| Authoritative source | Decision governance, decision-session lifecycle, transfer, recovery, metrics, economics, coherence, compatibility, and certification services. |
| Shape owner | Governance projections, quality report results, certification report results, and endpoint command results. |
| Serialization owner | Backend HTTP JSON serialization for backend-produced governance contracts. |
| Producer boundary | `DecisionEndpoints`, `DecisionSessionEndpoints`, and future architecture-governance endpoints if exposed. |
| Version identity | Unversioned pilot until representative governance fixtures or report baselines exist. |
| Normalization focus | Finding identifiers, severity, evidence references, compatibility state, owner fields, reviewer/certifier identity, timestamps, and report status. |
| Stability classification | Severity, compatibility state, approval state, certification result, and evidence sufficiency are semantic facts owned by governance services. |
| Compatibility policy | Compatibility reports must describe bridges as upstream-derived or quarantined; they cannot bless downstream invented meaning. |
| Governance constraint | Governance contracts must name evidence and rollback expectations because they can authorize changes to other contract identities. |

### Health Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Ping primitive, repository availability, workflow health, and runtime degradation surfaces. |
| Category | Health and diagnostics. |
| Authoritative source | Health/status authority for the specific boundary, repository availability authority, workflow health service, or runtime isolation owner. |
| Shape owner | Health endpoint result, repository projection health fields, workflow health projection, or runtime failure envelope producer. |
| Serialization owner | Backend HTTP JSON serialization unless the health surface is explicitly shell-owned. |
| Producer boundary | Health/status endpoints, repository/workflow projection endpoints, or shell-owned readiness commands when classified separately. |
| Version identity | Unversioned pilot until health fixtures and error-envelope baselines exist. |
| Normalization focus | Availability state, degradation reason, retry guidance, timestamp, nullable detail, status code relationship, and diagnostic evidence. |
| Stability classification | Availability, readiness, degradation, retryability, and health severity are semantic and identity-significant. |
| Compatibility policy | Presentation labels, colors, and icons may change independently, but the typed health facts must remain backend-owned. |
| Governance constraint | Transport must preserve health and error payloads without collapsing structured degradation data into string-only failures. |

### Certification Contract Example

| Dimension | Conformance case |
| --- | --- |
| Representative identity | Workflow certification, decision certification, reasoning certification, governance certification, and architecture evidence certification reports. |
| Category | Certification, diagnostics, and command response depending on how the report is requested. |
| Authoritative source | Certifying service or governance mechanism for the subject capability. |
| Shape owner | Certification report projection or command result emitted by the certifying service. |
| Serialization owner | Backend HTTP JSON serialization for backend-produced reports. |
| Producer boundary | Certification endpoints, report commands, or future generated evidence export boundaries. |
| Version identity | Unversioned pilot until certification report fixtures or evidence export baselines exist. |
| Normalization focus | Capability identity, invariant, evidence package references, command results, known limits, certification result, acceptance state, reviewer/certifier, and rollback readiness. |
| Stability classification | Certification result, evidence sufficiency, acceptance state, known limit, and rollback readiness are semantic facts. |
| Compatibility policy | Evidence reports may reference generated outputs or test artifacts, but the report contract must preserve enough durable context for downstream review. |
| Governance constraint | Certification cannot be inferred from passing tests alone; the contract must expose the evidence and known limits required by the architecture evidence model. |

## Initial Contract Identity Inventory

This inventory seeds M1.1 from the already certified Phase 0 Oracle pilots. It deliberately does not introduce generation, broad endpoint coverage, or new runtime behavior.

| Contract identity | Category | Owning authority | Owning projection or command | Producer boundary | Serialization authority | Version identity | Oracle source | Compatibility obligations |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Repository dashboard | Public projection response | Middle repository dashboard projection composed from repository, execution, continuity, reasoning, and decision-session authorities | `RepositoryDashboardProjection[]` | `GET /api/repositories`; Tauri `list_repositories` | Backend HTTP JSON | Fixture baseline, unversioned pilot | `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.golden.json`; request-boundary verifier; artifact freshness manifest | Rust dashboard mirror has known `decisionSessionSummary` drift; manual TypeScript type and dev mock are verified compatibility consumers until generated artifacts replace them. |
| Repository workspace | Public projection response | Middle repository workspace projection composed from repository, artifact, execution, continuity, reasoning, and decision-session authorities | `RepositoryWorkspaceProjection` | `GET /api/repositories/{repositoryId}/workspace`; Tauri `get_repository_workspace` | Backend HTTP JSON | Fixture baseline, unversioned pilot | `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-workspace.golden.json`; request-boundary verifier; artifact freshness manifest | Rust workspace mirror has known `decisionSessionSummary` drift; manual TypeScript type and dev mock workspace payload are verified compatibility consumers until generated artifacts replace them. |
| Workflow projection | Public projection response | Workflow projection service and workflow state-machine authorities | `WorkflowInstance` | `GET /api/repositories/{repositoryId}/workflow`; Tauri `get_workflow_projection` | Backend HTTP JSON | Fixture baseline, unversioned pilot | `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.golden.json`; request-boundary verifier; artifact freshness manifest | Rust response path is currently passive `serde_json::Value`; manual TypeScript workflow type is a verified compatibility consumer; dev mock response coverage is absent and remains a known gap. |

These three identities are enough to start M1.1 because they demonstrate identity across a collection response, an aggregate workspace response, and a workflow projection response. They do not classify command bodies, mutation results, event streams, diagnostics, health, certification reports, error envelopes, or shell-owned commands — those are extended by the Phase 8 orchestration-loop inventory below.

## Orchestration Loop Contract Inventory (Phase 8 / m8)

Phase 8 hardens the observable contracts the Plan Authoring -> Execution -> Decision loop (m2-m7) added ad-hoc. It mints command-result, structured-error, and event-stream identities (the categories the M1.1 seed deliberately deferred), captures golden fixtures, and binds the existing TypeScript run-event types as verified consumers. Stream identities carry two governed contracts each: the event payload shape and the stream lifecycle (ordering, terminal, failure, reconnect/replay). No knowledge-graph, intelligence, query, or recommendation contracts are introduced.

| Contract identity | Category | Owning authority | Owning projection or command | Producer boundary | Serialization authority | Version identity | Oracle source | Compatibility obligations |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Plan status | Public projection response | Repository orchestrator plan lifecycle | `PlanStatus` (+ `PlanLifecycleState`) | `GET /api/repositories/{repositoryId}/plan/status`; Tauri `get_plan_status` | Backend HTTP JSON | Fixture baseline (m8) | `ContractFixtures/plan-status.golden.json` + `plan-status-authoring.golden.json`; consumer `PlanStatus` TS; request-boundary | Both lifecycle wire strings (`PlanAuthoring`, `ExecutingPlan`) byte-pinned; verified TS `PlanStatus` consumer. |
| Plan run acknowledgement | Command result (202) | Repository orchestrator command boundary | `PlanRunAcknowledgement` | 202 from `POST plan/write\|revise\|execute`, `POST decision/run\|submit` | Backend HTTP JSON | Fixture baseline (m8) | `ContractFixtures/plan-run-acknowledgement.golden.json`; request-boundary | Phase vocabulary `WritePlan\|RevisePlan\|ExecutePlan\|DecisionRun\|SubmitDecisions`. |
| Orchestration error envelope | Structured error response | Orchestration endpoint exception mapping | Anonymous `{ error }` (`Results.NotFound/BadRequest/Conflict`) | Any faulted orchestration command (404/400/409) | Backend HTTP JSON | Fixture baseline (m8) | `ContractFixtures/orchestration-error.golden.json`; live request-boundary error tests | `KeyNotFound`->404, `Argument`->400, `InvalidOperation`/`ObjectDisposed`->409; mirrors the shared `{ error }` envelope. |
| Conversation projection | Public projection response | Repository orchestrator conversation transcript | `ConversationProjection` (+ `ConversationEntry`/`ConversationEntryKind`) | `GET /api/repositories/{repositoryId}/conversation` | Backend HTTP JSON | Fixture baseline (m8) | `ContractFixtures/conversation.golden.json` | Shape-pinned; summary text is opaque (not contract-bound); no UI consumer yet (unconsumed endpoint). |
| Prompt provenance | Backend provenance record | Core prompt authority | `PromptProvenance` (+ `PromptSessionRole`) | Per-turn provenance carried on `ExecutionPromptManifest` | Backend HTTP JSON | Fixture baseline (m8) | `ContractFixtures/prompt-provenance.golden.json`; provenance-certification test | Backend-internal; no UI consumer; all seven fields certified across planning/execution/decision/transfer turns. |
| Plan authoring stream | Streaming event payload + lifecycle | Repository orchestrator planning stream | `PlanningStream` merged events (`turn-started`/`delta`/`completed`/`failed`) | `GET /api/repositories/{repositoryId}/plan/stream` (SSE) | Backend SSE / HTTP JSON | Fixture baseline (m8) | `ContractFixtures/plan-stream.golden.json` + `plan-stream.artifact-freshness.json`; consumer `Plan*Event` TS; lifecycle tests | Payload + lifecycle (ordering/terminal/failure/`Last-Event-ID` replay) governed; verified TS run-event variants. |
| Execution run stream | Streaming event payload + lifecycle | Repository orchestrator execution stream | `ExecutionStream` merged events (`run-started`/`phase`/`delta`/`milestones-extracted`/`committed`/`lifecycle`/`handoff-rotated`/`completed`/`failed`) | `GET /api/repositories/{repositoryId}/execution/stream` (SSE) | Backend SSE / HTTP JSON | Fixture baseline (m8) | `ContractFixtures/execution-stream.golden.json` + `execution-stream.artifact-freshness.json`; consumer `ExecutionRun*Event` TS; lifecycle tests | Payload + lifecycle (intermediate ordering pinned, terminal/failure/replay) governed; verified TS run-event variants. |
| Decision run stream | Streaming event payload + lifecycle | Repository orchestrator decision stream | `DecisionStream` merged events (`run-started`/`diagnostics`/`phase`/`transferred`/`delta`/`completed`/`review-ready`/`submitted`/`failed`) | `GET /api/repositories/{repositoryId}/decision/stream` (SSE) | Backend SSE / HTTP JSON | Fixture baseline (m8) | `ContractFixtures/decision-stream.golden.json` + `decision-stream.artifact-freshness.json`; consumer `DecisionRun*Event` TS; lifecycle tests (incl. Transfer + Submit scenarios) | Payload + lifecycle governed; all nine event types producer-bound; verified TS run-event variants. |

### Loop command and acknowledgement contracts (m11 detail)

The endpoint-level routing, families, status codes, and consumers for the ten loop endpoints plus the repository DELETE teardown are inventoried in `docs/contract-endpoint-catalog.md` (Orchestration Loop Endpoint Inventory). The implementation overview is in `docs/architecture.md` (Orchestration Loop Architecture, "Loop transport and contracts"); governance evidence is in `docs/orchestration-loop-governance.md`. This subsection records the serialized command, error, and stream-event shapes; it does not restate the route table.

Every POST command (`plan/write`, `plan/revise`, `plan/execute`, `decision/run`, `decision/submit`) acknowledges with `202` and a `PlanRunAcknowledgement(string Phase)` body, then runs the turn in the background on the orchestrator lifetime token. The five phase wire strings are identity-bearing: `WritePlan`, `RevisePlan`, `ExecutePlan`, `DecisionRun`, `SubmitDecisions`. `GET plan/status` returns `PlanStatus { planExists, state }` where `state` is the `PlanLifecycleState` enum serialized as a string (`PlanAuthoring` / `ExecutingPlan`). `GET conversation` returns `ConversationProjection { entries[] }` of `ConversationEntry { sequence, kind, iteration, summary, reference }`, where `kind` is `ConversationEntryKind` (`Planning` / `OperationalOutput` / `DecisionOutput` / `Submit` / `Continuation`) and `summary`/`reference` are opaque (not contract-bound).

### Orchestration structured error envelope (m11 detail)

All faulted loop commands serialize a single backend-owned `{ error }` envelope under `JsonSerializerDefaults.Web`. The exception-to-status mapping is uniform across the loop endpoints:

| Exception | HTTP status | Envelope |
| --- | --- | --- |
| `KeyNotFoundException` | `404` | `{ "error": <message> }` |
| `ArgumentException` | `400` | `{ "error": <message> }` |
| `InvalidOperationException` (incl. `ObjectDisposedException`, which derives from it) | `409` | `{ "error": <message> }` |

`plan/execute` and `decision/run` accept no request body and therefore never raise `400`. A disposed orchestrator surfaces as a recoverable `409`, not an opaque `500`. This envelope is governed as the `Orchestration error envelope` identity in the table above and follows the catalog's error-envelope rule: transport must preserve the structured payload rather than collapse it to a message-only failure.

### SSE stream event vocabularies and lifecycle (m11 detail)

Each of the three streams (`plan/stream`, `execution/stream`, `decision/stream`) carries two governed contracts: the **event payload** (the per-event `data:` JSON shape) and the **stream lifecycle** (ordering, terminal events, failure, and `Last-Event-ID` replay). The transport frame is identical for all three: `id: <sequence>`, `event: <type>`, `data: <camelCase JSON>` lines emitted by `OrchestratorStreamChannel`, a single-producer/multi-subscriber broadcast with monotonic sequence ids and a bounded replay buffer. The `event:` name equals the payload's `type` discriminant. Property names follow `JsonSerializerDefaults.Web` (camelCase). The producer (`RepositoryOrchestrator`) and the m8-frozen TypeScript run-event types are both verified against these vocabularies.

**Last-Event-ID replay contract.** A reconnecting client may send a `Last-Event-ID` header; the endpoint parses it (positive integer, else `0`) and replays buffered events with a sequence greater than that id before continuing live, so the replay/live boundary is exactly-once. Sequence ids are monotonic per stream. Replay is bounded by the channel's buffer capacity; a client that has fallen further behind than the buffer cannot fully replay.

**Payload-vs-lifecycle distinction.** The event payload identity governs the field shape of each `type` variant. The stream lifecycle identity governs ordering and terminal semantics: `completed`/`failed` are terminal for a planning turn; `failed` is the single terminal failure variant on every stream; intermediate ordering (for example execution `run-started -> phase -> delta -> ... -> completed`) is pinned by the lifecycle tests, not by the payload contract alone. A consumer may not infer lifecycle guarantees from transport framing beyond what the lifecycle identity documents.

Plan authoring stream (`PlanStreamEvent`) — four variants:

| `type` | Payload fields |
| --- | --- |
| `turn-started` | `phase` (`PlanTurnPhase`: `WritePlan` / `RevisePlan`) |
| `delta` | `text` |
| `completed` | `plan`, `promptTokens`, `outputTokens` |
| `failed` | `reason`, `detail?` |

Execution run stream (`ExecutionRunEvent`) — nine variants:

| `type` | Payload fields |
| --- | --- |
| `run-started` | `phase` (`ExecutePlan` / `ContinueExecution`) |
| `phase` | `phase` (`ExtractMilestones` / `StartExecution` / `ContinueExecution`) |
| `delta` | `phase`, `text` |
| `milestones-extracted` | `count` |
| `committed` | `commitSha` (nullable), `pushed` |
| `lifecycle` | `state` (`ExecutionRunLifecycleState`: `ExecutingPlan`) |
| `handoff-rotated` | `sequence`, `path` |
| `completed` | `commitSha` (nullable), `milestoneCount`, `handoffPath`, `promptTokens`, `outputTokens` |
| `failed` | `phase?`, `reason`, `detail?` |

Decision run stream (`DecisionRunEvent`) — nine variants:

| `type` | Payload fields |
| --- | --- |
| `run-started` | `phase` (`DecisionRun`), `route?` (`DecisionRunRoute`: `Continue` / `Transfer`) |
| `diagnostics` | `sandbox`, `approvals`, `seeded` |
| `phase` | `phase` (`GetNextDecisions` or a `DecisionRunTransferPhase`: `ProduceOperationalDelta` / `UpdateOperationalContext` / `StartDecisionSessionFromTransfer`) |
| `transferred` | `operationalDelta`, `operationalContext` |
| `delta` | `text` |
| `completed` | `promptTokens`, `outputTokens` |
| `review-ready` | `decisions` (the editable proposed-decisions text; persisted only by `decision/submit`) |
| `submitted` | `path`, `sequence?`, `numberedPath?` |
| `failed` | `phase?`, `reason`, `detail?` |

The `commitSha`-nullable `committed`/`completed` execution frames keep their shape when m10's `AutomaticCommitPushAfterExecuteEnabled` flag is off (`commitSha = null`), so the frame contract is flag-stable. The `route` field on decision `run-started` is optional so an older server (no route) reads as `Continue`. These vocabularies are the verified TypeScript run-event types `PlanStreamEvent`, `ExecutionRunEvent`, and `DecisionRunEvent` (the m8-frozen, byte-pinned files under `src/CommandCenter.UI/src/types/`), bound as verified consumers in the table above.

## Initial Contract Relationship Matrix

This matrix records contract families discovered in the first Milestone 0.2 inventory slice. Later slices must expand each family into endpoint-level and field-level entries before fixtures are certified.

| Contract identity | Owning projection or command | Serialization authority | Producer | Consumers | Parallel representations | Compatibility obligations | Planned Oracle fixture | Migration priority |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Repository dashboard | `RepositoryDashboardProjection` | Backend JSON serialization | `RepositoriesEndpoints` | Tauri `list_repositories`, TS `RepositoryDashboardProjection`, dev mock, repository hooks/UI | C# projection, Rust `RepositoryDashboardProjection`, TS type, mock data | Preserve repository identity, path/name, availability, and summary fields while manual consumers exist. | Yes, representative read model. | High |
| Repository workspace | `RepositoryWorkspaceProjection` | Backend JSON serialization | `RepositoriesEndpoints`, artifact rotation endpoints | Tauri workspace commands, TS `RepositoryWorkspaceProjection`, artifact APIs, dev mock, workspace UI | C# projection, Rust `RepositoryWorkspaceProjection`, TS type, mock data | Preserve nested artifact, execution, continuity, reasoning, decision-session, and workspace summary shape. | Yes, representative aggregate workspace. | High |
| Artifact inventory/content | Artifact inventory projection and content commands | Backend JSON serialization for inventory; string payload for content | `ArtifactsEndpoints` | Tauri artifact commands, TS artifact types, editor/workspace UI | C# endpoint result, Rust command result, TS types, mock data | Preserve content as opaque text and inventory classification until generated consumers exist. | Inventory yes; content no unless content envelope is introduced. | Medium |
| Execution context and session | Execution projections and command results | Backend JSON serialization | `ExecutionEndpoints`, `ExecutionSessionsEndpoints` | Tauri execution commands, TS execution types, execution hooks/UI, dev mock | C# projections, Rust `ExecutionSessionSummary` and related mirrors, TS types, mock data | Preserve session identity, state, prompt manifest, transparency, status, events, and handoff result compatibility. | Yes, summary/status and prompt manifest. | High |
| Execution event stream | Execution event stream contract | Backend event serialization | `ExecutionSessionsEndpoints` stream route | Browser/event consumers and future transport passivity tests | C# event records, stream formatting, TS event type | Preserve event order, event payload shape, and failure boundary semantics. | Later, stream-specific fixture or trace. | Medium |
| Git status and execution Git actions | Git status projection and execution Git command results | Backend JSON serialization | `GitEndpoints` | Tauri Git/commit/push commands, TS git/execution types, Git UI, dev mock | C# projections, Rust request/result mirrors, TS types, mock data | Preserve dirty-state, change scope, eligibility, commit preparation, commit, and push result semantics. | Yes, status and action eligibility. | High |
| Operational-context proposals | Operational context proposal projection and review commands | Backend JSON serialization | `OperationalContextEndpoints` | Tauri proposal commands, TS operational context types, continuity UI, dev mock | C# projection, Rust command requests, TS types, mock data | Preserve proposal status, markdown content, semantic change summaries, compression, assimilation, and review states. | Yes, proposal read model. | High |
| Continuity diagnostics/reports | Continuity projections and report command results | Backend JSON serialization | `ContinuityEndpoints` | Tauri continuity commands, TS continuity types, continuity UI, dev mock | C# projections, TS types, mock data | Preserve diagnostic group semantics, trends, report history, and null/empty report behavior. | Yes, diagnostics. | Medium |
| Decision lifecycle eligibility | Decision lifecycle eligibility projection | Backend JSON serialization | `DecisionEndpoints` | Tauri decision commands, TS decision types, decision UI, dev mock | C# projections, TS union/string literals, mock data | Preserve backend-owned eligibility and blocked-state semantics; UI must not infer legality. | Yes, required representative contract. | High |
| Decision proposal browser/review | Decision proposal, browser, review, option, evidence, lineage, revision contracts | Backend JSON serialization | `DecisionEndpoints` | Tauri decision commands, TS decision types, decision UI, dev mock | C# projections, TS types, mock data | Preserve proposal lifecycle, review authority, recommendation evidence, option comparison, source attribution, and lineage fields. | Yes, proposal browser. | High |
| Decision governance/quality/certification | Governance, quality, certification projections and reports | Backend JSON serialization | `DecisionEndpoints` | Tauri decision commands, TS decision types, governance/quality UI, dev mock | C# projections, TS types, mock data | Preserve severity, findings, ratings, evidence, reports, and certification result meaning. | Governance snapshot/report yes. | Medium |
| Decision-session governance | Decision-session lifecycle, analysis, recovery, workflow, certification projections | Backend JSON serialization | `DecisionSessionEndpoints` | Tauri decision-session commands, TS decision-session/workflow types, governance UI, dev mock | C# projections, TS types, mock data | Preserve active/null session semantics, transfer eligibility, recovery diagnostics, workflow influence, and certification reports. | Yes, governance snapshot. | High |
| Reasoning graph/report | Reasoning events, threads, relationships, graph, trace, query, reconstruction, materialization, certification projections | Backend JSON serialization | `ReasoningEndpoints` | Tauri reasoning commands, TS reasoning types, reasoning UI, dev mock | C# projections/records, TS types, mock data | Preserve schema-versioned reasoning records, boundary violations, diagnostics, graph identity, trace direction, and report history. | Yes, graph/report. | High |
| Workflow projection | Workflow instance and related diagnostics, gates, history, recovery, continuation, preparation, health, reports, certification | Backend JSON serialization | `WorkflowEndpoints` | Tauri workflow commands, TS workflow types, workflow UI, dev mock | C# projections, TS types, mock data | Preserve workflow-owned stage, gates, legality, diagnostics, recovery, health, and certification semantics. | Yes, required representative contract. | High |
| Planning projection | Planning milestones projection | Backend JSON serialization | `PlanningEndpoints` | TS planning types and planning UI when consumed | C# projection, TS type | Preserve milestone list shape and ordering. | Later. | Low |
| Error envelope and boundary violation | Backend error response envelope, including `boundaryViolation` when present | Backend JSON serialization | Endpoint exception/error handling | Tauri error channel, TS `TransportError`, UI boundary notices | C# anonymous/envelope shapes, Rust `ErrorResponse`, TS `BoundaryViolationProjection`, API parser | Preserve status, error text, structured boundary violation, null, and unknown fields through transport. | Yes, required representative contract. | High |
| Tauri command request envelope | Shell command arguments and backend request bodies | Shell should be passive transport except shell-owned commands | `src/CommandCenter.Shell/src/main.rs` | TS API wrappers and Tauri runtime | Rust request structs, TS API arg objects, backend request DTOs | Temporary mirrors must be inventoried and either generated, verified, or retired in Milestone 1.3. | No, unless classified as shell-owned. | High |
| Dev Tauri mock contracts | Development mock command responses | Must be generated or Oracle-verified against backend contracts | `src/CommandCenter.UI/src/devTauriMock.ts` | Frontend development and characterization tests | Mock object literals parallel backend, Rust, and TS shapes | Must not become independent contract authority. | No direct fixture; compare through generated or verified mock data later. | High |

## Initial Parallel Truths

The first inventory slice found these active parallel contract truth sources:

- Backend endpoint response shapes under `src/CommandCenter.Backend/Endpoints`.
- Backend projection and command result records across domain projects.
- Rust Tauri command return/request mirrors in `src/CommandCenter.Shell/src/main.rs`.
- Manual TypeScript types in `src/CommandCenter.UI/src/types`.
- TypeScript API command names and generic return types in `src/CommandCenter.UI/src/api`.
- Development mock payloads in `src/CommandCenter.UI/src/devTauriMock.ts`.
- Characterization and backend tests that encode expected shape indirectly.
- Durable docs for operational-context and reasoning repository persistence contracts.

## Endpoint Catalog and Consumer Taxonomy

The endpoint-level inventory is maintained in `docs/contract-endpoint-catalog.md`.

Current catalog scope:

- 177 backend endpoint mappings under `src/CommandCenter.Backend/Endpoints`.
- Consumer taxonomy covering backend tests, Rust shell commands, TypeScript API wrappers, manual TypeScript types, dev Tauri mocks, React consumers, characterization/E2E tests, and durable docs.
- Narrow serialization rules for identifiers, enum-like strings, null versus omitted fields, empty collections, date/time capture, ordering, unknown fields, error envelopes, streams, and compatibility fields.
- Backend JSON serialization observations from `Program.CreateApp`: web defaults plus string enum conversion.
- Repository dashboard field ownership pilot for `GET /api/repositories`, including top-level fields, nested summary fields, nullability, derived status, and known compatibility drift.
- Repository dashboard golden fixture at `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.golden.json`, protected by `ContractOracleFixtureTests.RepositoryDashboardGoldenFixtureMatchesBackendSerialization`.
- Repository dashboard drift policy classification in `ContractOracleFixtureTests`, with structural drift failing immediately and additive field drift requiring explicit compatibility review.
- Repository dashboard Rust, TypeScript, and dev mock consumer verification in `ContractConsumerVerificationTests`, backed by shared test-support infrastructure in `ContractVerification/ContractConsumerVerificationSupport.cs`, which recursively compares the backend golden fixture shape against downstream consumer shapes, reports the known Rust `decisionSessionSummary` omission as downstream consumer drift, verifies the manual TypeScript dashboard type as current, and verifies the `devTauriMock` dashboard entry shape for the pilot fixture.
- Repository dashboard contract artifact freshness verification in `ContractGeneratedArtifactFreshnessTests`, backed by `repository-dashboard.artifact-freshness.json`, which hashes the Oracle fixture and the current TypeScript repository contract artifact to distinguish stale artifacts, unexpected artifact edits, and missing expected artifacts.
- Repository dashboard request-boundary verification in `ContractRequestBoundaryTests`, which pins `GET /api/repositories` as a no-argument backend route, `list_repositories` as a no-argument Rust Tauri command that forwards a GET without request-body construction, and `listRepositories()` as a TypeScript API wrapper that invokes `list_repositories` without command arguments.
- Repository workspace field ownership and golden fixture pilot for `GET /api/repositories/{repositoryId}/workspace`, including top-level workspace fields, artifact inventory, full operational-context projection shape, nested execution, reasoning, and decision-session summaries, and known Rust workspace mirror drift.
- Repository workspace golden fixture at `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-workspace.golden.json`, protected by `ContractOracleFixtureTests.RepositoryWorkspaceGoldenFixtureMatchesBackendSerialization`.
- Repository workspace Rust, TypeScript, and dev mock consumer verification in `ContractConsumerVerificationTests`, using the shared verifier support to report the known Rust `decisionSessionSummary` omission, verify the manual TypeScript workspace type as current, and verify the `devTauriMock` workspace command payload shape through the typed mock workspace store.
- Repository workspace contract artifact freshness verification in `ContractGeneratedArtifactFreshnessTests`, backed by `repository-workspace.artifact-freshness.json`, which hashes the workspace Oracle fixture and the current TypeScript repository contract artifact.
- Repository workspace request-boundary verification in `ContractRequestBoundaryTests`, which pins `GET /api/repositories/{repositoryId}/workspace` as a single route-argument, bodyless backend route, `get_repository_workspace(repository_id)` as a Rust Tauri command that forwards a GET without request-body construction, and `getRepositoryWorkspace(repositoryId)` as a TypeScript API wrapper that passes only the repository id command argument.
- Workflow contract artifact freshness verification in `ContractGeneratedArtifactFreshnessTests`, backed by `workflow-instance.artifact-freshness.json`, which hashes the workflow Oracle fixture and the current TypeScript workflow contract artifact.
- Priority endpoint rows for the first fixture candidates.

The catalog is not a generated schema. It is an inventory and fixture-selection mechanism used to prevent fixtures from certifying accidental or consumer-owned shape.

The repository dashboard pilot currently exposes one executable compatibility finding: the Rust `RepositoryDashboardProjection` mirror omits `decisionSessionSummary`, while the backend, TypeScript dashboard contract, and dev mock dashboard entry include it. `ContractConsumerVerificationTests` records this as downstream consumer drift and separately verifies the manual TypeScript `RepositoryDashboardProjection` shape and `devTauriMock` dashboard entry shape against the same Oracle fixture. This is evidence for the Oracle and a later shell/manual-mirror migration; the Oracle fixture does not treat any downstream mirror as contract authority.

## Fixture Gating Rule

Golden fixtures may be introduced only after the target contract has:

- a contract identity,
- an owning backend projection or command result,
- a producer endpoint,
- a known consumer set,
- known parallel representations,
- compatibility obligations,
- serialization rules relevant to the contract,
- and an update workflow for fixture review and consumer regeneration.

The first fixture candidates are repository dashboard, repository workspace, workflow projection, decision lifecycle eligibility, decision proposal browser, decision-session governance snapshot, reasoning graph/report, continuity diagnostics, execution summary/status, and error envelope.

The repository dashboard candidate now has the first golden fixture and recursive backend serialization comparison. The fixture intentionally covers explicit nulls, populated arrays, non-empty execution summary and history, decision-session summary, timestamps, durations, enum strings, and nested summary objects. Empty-array coverage remains represented by nested zero-count reasoning fields and will need a second dashboard variant or another fixture if empty collection serialization must be pinned for this contract specifically.

The repository workspace candidate now has the second golden fixture, recursive backend serialization comparison, consumer verification against Rust, TypeScript, and dev mock downstream shapes, artifact freshness verification for the shared TypeScript repository contract artifact, and request-boundary verification for the primary workspace GET path. The fixture intentionally covers artifact inventory nulls and populated arrays, full operational-context item arrays, proposal summary enum/null/date fields, execution summary accepted/commit/push fields, empty decision-session arrays, and the backend-owned `decisionSessionSummary` field that is missing from the Rust workspace mirror. This proves the Oracle pattern can repeat across a second contract family, and local repository workspace Oracle certification is recorded in `.agents/milestones/m0.2-repository-workspace-oracle-certification-slice-0024.md`.

Initial cross-pilot repeatability evidence is recorded in `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0025.md`. The first repeatability claim was limited to the repository dashboard and repository workspace pilots: both used the same field-inventory, golden-fixture, drift-classification, consumer-verification, artifact-freshness, request-boundary, and local-certification lifecycle without an Oracle framework redesign.

Workflow projection coverage started with gated field inventory in `.agents/milestones/m0.2-workflow-projection-field-inventory-slice-0026.md`. The workflow inventory identifies `WorkflowInstance` as the primary contract for `GET /api/repositories/{repositoryId}/workflow`, maps the backend producer, shell and TypeScript request boundary, manual TypeScript response mirror, UI consumers, absent dev mock command handler, and semantic lifecycle field groups that must be represented before a workflow golden fixture can be approved. Field-role classification for the primary workflow fixture candidate is recorded in `.agents/milestones/m0.2-workflow-fixture-field-classification-slice-0027.md`; it classifies each top-level `WorkflowInstance` field, establishes nested field classification rules, and keeps flattened status/eligibility fields under compatibility review before fixture capture. The first workflow golden fixture is `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.golden.json`, protected by `ContractOracleFixtureTests.WorkflowInstanceGoldenFixtureMatchesBackendSerialization`; it covers explicit nulls, empty and non-empty diagnostics arrays, backend-owned eligibility booleans, ordered timeline/transition/gate arrays, flattened compatibility fields, and `decisionSession: null`. Workflow TypeScript consumer verification is recorded in `.agents/milestones/m0.2-workflow-typescript-consumer-verification-slice-0029.md`; `workflowContractFixture.test.ts` reads the backend golden fixture and verifies the manual TypeScript `WorkflowInstance` shape for the represented fixture variant. Workflow request-boundary verification is recorded in `.agents/milestones/m0.2-workflow-request-boundary-slice-0030.md`; `ContractRequestBoundaryTests` now verifies the backend route, passive Rust command, and TypeScript command argument shape for the primary workflow projection. Workflow artifact freshness is recorded in `.agents/milestones/m0.2-workflow-artifact-freshness-slice-0031.md`; `workflow-instance.artifact-freshness.json` now ties the workflow golden fixture to the manual TypeScript workflow contract artifact. Local workflow Oracle certification is recorded in `.agents/milestones/m0.2-workflow-oracle-certification-slice-0032.md`. Populated `decisionSession` coverage and dev mock workflow handler coverage are accepted gaps for the initial workflow pilot; sibling workflow endpoint fixtures remain pending.

Cross-family repeatability evidence across repository dashboard, repository workspace, and primary workflow projection is recorded in `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0033.md`. The repeatability claim covers two repository read models and one richer semantic workflow family using the same Oracle lifecycle without framework redesign. Milestone-level certification review is recorded in `.agents/milestones/m0.2-oracle-certification-review-slice-0034.md`; it certifies the Phase 0 Contract Oracle foundation with explicit accepted limitations and does not claim full contract-surface coverage, generated contract lifecycle, or passive transport certification. Formal acceptance and baseline evidence is recorded in `.agents/milestones/m0.2-oracle-acceptance-baseline-slice-0035.md`; M0.2 is accepted as a scoped Phase 0 foundation, not as complete contract-surface coverage. Decision lifecycle eligibility remains the preferred fourth family only if a future review identifies a concrete uncovered backend-owned eligibility property.

## Initial Oracle Fixture Workflow

The initial executable workflow for the repository dashboard and repository workspace fixture pilots is:

1. Build representative backend projection data with stable identifiers and timestamps.
2. Serialize with `JsonSerializerDefaults.Web` plus `JsonStringEnumConverter`.
3. Compare serialized JSON recursively against the golden fixture while treating object property ordering as non-semantic.
4. Fail structural drift immediately for missing fields, type changes, null/object changes, array/scalar changes, changed values, or array length changes.
5. Classify additive backend fields as compatibility-review drift unless their exact JSON path is explicitly recorded as a reviewed compatibility addition for that fixture.
6. Review fixture updates through Milestone 0.2 evidence before downstream Rust, TypeScript, mock, or generated consumers are changed.

This workflow is a pilot mechanism, not the full generated Oracle lifecycle. It still needs fixture update tooling, consumer regeneration or verification, and broader endpoint coverage before full contract-surface certification. Milestone 0.2 is accepted as the scoped Phase 0 Oracle foundation and representative fixture lifecycle.

## Drift Policy Classification

The repository dashboard Oracle pilot currently recognizes these drift categories:

| Category | Examples | Oracle behavior |
| --- | --- | --- |
| Structural drift | Missing fixture field, type change, null/object mismatch, array/scalar mismatch, changed value, array length change, serializer behavior change. | Hard failure. The backend serialized shape no longer matches the accepted contract fixture. |
| Compatibility-review drift | Additive backend field not present in the fixture. | Failure until the field is reviewed, documented, and either added to the fixture or explicitly recorded as a reviewed compatibility addition. |
| Consumer drift | Rust, TypeScript, mock, or characterization representation differs from backend Oracle truth. | Must be surfaced by consumer verification. It must not weaken the backend Oracle fixture. |

Reviewed compatibility additions are path-specific. A reviewed additive field does not make the consumer current; it only records that the backend fixture comparison may permit that additive field while the compatibility path is handled.

## Consumer Verification Pilot

Consumer verification is separate from Oracle fixture comparison. The Oracle compares backend serialization to accepted backend-owned fixture truth. Consumer verification compares downstream representations against that Oracle-observed truth and reports where a consumer is stale, invented, or structurally incompatible.

The consumer verification pilot now uses shared test-support infrastructure with a reusable verifier specification, recursive comparison engine, consumer shape model, and source-specific shape providers. The Rust shape provider parses `src/CommandCenter.Shell/src/main.rs`, follows nested struct references, unwraps `Option<T>` nullability, honors explicit `#[serde(rename = "...")]` field names before camel-casing, compares `Vec<T>` array item shape when the fixture contains an item, and treats `serde_json::Value` as opaque transport shape. The TypeScript shape provider parses exported type aliases under `src/CommandCenter.UI/src/types`, resolves imported/manual aliases through the shared type folder, treats string-literal unions as string-valued contracts, unwraps nullable unions, and compares array item shape when the fixture contains an item. The dev mock shape provider parses `src/CommandCenter.UI/src/devTauriMock.ts`, extracts the `dashboardEntry(workspace)` returned object shape, resolves `workspace.*` references through the TypeScript workspace type, recognizes inline object literals, treats `.length` projections as numeric fields, and verifies that the repository workspace mock command returns the typed `state.workspaces[repositoryId]` payload.

Consumer categories currently reported by the verifier are:

| Consumer | Category |
| --- | --- |
| Rust `RepositoryDashboardProjection` mirror | Runtime consumer |
| Rust `RepositoryWorkspaceProjection` mirror | Runtime consumer |
| TypeScript `RepositoryDashboardProjection` type | Compile-time consumer |
| TypeScript `RepositoryWorkspaceProjection` type | Compile-time consumer |
| `devTauriMock` `dashboardEntry` object | Development/test consumer |
| `devTauriMock` `get_repository_workspace` payload | Development/test consumer |

It classifies:

| Consumer drift kind | Meaning |
| --- | --- |
| Missing downstream field | Backend serialized field exists in the Oracle fixture, but the consumer mirror omits it. |
| Extra downstream field | Consumer mirror declares a field not present in the backend Oracle fixture. |
| Value-kind changed | Backend serialized value kind is not accepted by the downstream mirror shape. |

Current finding:

- `src/CommandCenter.Shell/src/main.rs` omits `$[].decisionSessionSummary` from `RepositoryDashboardProjection`.
- `src/CommandCenter.Shell/src/main.rs` omits `$.decisionSessionSummary` from `RepositoryWorkspaceProjection`.
- `src/CommandCenter.UI/src/types/repositories.ts` currently matches the repository dashboard Oracle fixture shape, including imported execution summaries and nested decision-session summary arrays.
- `src/CommandCenter.UI/src/types/repositories.ts` currently matches the repository workspace Oracle fixture shape, including artifact inventory, operational-context, reasoning, execution, and decision-session summary shapes.
- `src/CommandCenter.UI/src/devTauriMock.ts` currently matches the repository dashboard Oracle fixture shape for the `dashboardEntry(workspace)` mock projection, including inline continuity summary fields and workspace-derived reasoning and decision-session summaries.
- `src/CommandCenter.UI/src/devTauriMock.ts` currently returns typed workspace mock payloads for `get_repository_workspace` that match the repository workspace Oracle fixture shape.

Current protection:

- `RepositoryDashboardRustMirrorReportsKnownDecisionSessionSummaryOmission` keeps the known root-level Rust omission executable.
- `RepositoryDashboardRustMirrorRecursivelyVerifiesMirroredNestedShape` proves the Rust mirror's existing nested repository, execution summary/history, continuity summary, and reasoning summary shapes still conform to the backend fixture.
- `RepositoryDashboardTypeScriptTypeMatchesGoldenFixture` proves the manual TypeScript dashboard type has no missing, extra, or value-kind drift against the pilot fixture.
- `RepositoryDashboardTypeScriptTypeRecursivelyVerifiesImportedNestedShape` proves imported execution summary aliases and nested decision-session summary arrays are resolved by the shared verifier pipeline.
- `RepositoryDashboardDevTauriMockMatchesGoldenFixture` proves the dev mock dashboard entry has no missing, extra, or value-kind drift against the pilot fixture.
- `RepositoryDashboardDevTauriMockRecursivelyVerifiesInlineContinuityShape` proves inline mock object literals and workspace-derived nested summaries participate in the shared verifier pipeline.
- `RepositoryWorkspaceRustMirrorReportsKnownDecisionSessionSummaryOmission` keeps the known root-level Rust workspace omission executable.
- `RepositoryWorkspaceRustMirrorRecursivelyVerifiesMirroredNestedShape` proves the Rust mirror's existing nested repository, execution, artifact inventory, operational-context, and reasoning shapes still conform to the backend fixture.
- `RepositoryWorkspaceTypeScriptTypeMatchesGoldenFixture` proves the manual TypeScript workspace type has no missing, extra, or value-kind drift against the pilot fixture.
- `RepositoryWorkspaceDevTauriMockPayloadMatchesGoldenFixture` proves the dev mock workspace command payload has no missing, extra, or value-kind drift against the pilot fixture.
- `ConsumerVerifierReportsNestedMissingFields` protects recursive missing-field behavior independent of the Rust parser.

This pilot does not yet compare non-empty command argument bodies, additional mock command payloads, or semantic reinterpretation. Those remain later M1.x, passive transport, runtime isolation, and architectural-regression work.

## Request Boundary Verification Pilot

Request boundary verification is separate from response fixture comparison and downstream response-shape consumer verification. It asks whether the backend endpoint, shell command, and TypeScript API wrapper still agree on the externally observable request shape for a contract boundary.

The repository dashboard request boundary is intentionally no-argument:

| Boundary participant | Expected request shape |
| --- | --- |
| Backend endpoint | `GET /api/repositories`, no route parameters, no body metadata. |
| Rust Tauri command | `list_repositories()`, no command parameters, backend `GET /api/repositories`, no client request-body construction. |
| TypeScript API wrapper | `listRepositories()`, invokes `list_repositories` without an argument object. |

The repository workspace request boundary is the first non-empty request-boundary pilot:

| Boundary participant | Expected request shape |
| --- | --- |
| Backend endpoint | `GET /api/repositories/{repositoryId:guid}/workspace`, one required `repositoryId` GUID route parameter, no body metadata. |
| Rust Tauri command | `get_repository_workspace(repository_id: String)`, backend `GET /api/repositories/{repository_id}/workspace`, no client request-body construction. |
| TypeScript API wrapper | `getRepositoryWorkspace(repositoryId: string)`, invokes `get_repository_workspace` with `{ repositoryId }` and no additional command fields. |

The primary workflow projection request boundary now repeats that single-route-argument pattern while preserving passive Rust transport:

| Boundary participant | Expected request shape |
| --- | --- |
| Backend endpoint | `GET /api/repositories/{repositoryId:guid}/workflow`, one required `repositoryId` GUID route parameter, no body metadata. |
| Rust Tauri command | `get_workflow_projection(repository_id: String)`, backend `GET /api/repositories/{repository_id}/workflow` through `backend_get_value`, no client request-body construction. |
| TypeScript API wrapper | `getWorkflowProjection(repositoryId: string)`, invokes `get_workflow_projection` with `{ repositoryId }` and no additional command fields. |

Current protection:

- `RepositoryDashboardBackendEndpointHasNoRequestArguments` verifies the backend route method, pattern, route parameters, and body metadata.
- `RepositoryDashboardRustCommandHasNoCommandArgumentsAndForwardsGetWithoutBody` verifies the Rust command signature and GET forwarding path.
- `RepositoryDashboardTypeScriptApiInvokesCommandWithoutArguments` verifies the TypeScript command invocation has no argument object.
- `RepositoryWorkspaceBackendEndpointHasRepositoryIdRouteArgumentAndNoBody` verifies the backend route method, pattern, required GUID route parameter, and absence of body metadata.
- `RepositoryWorkspaceRustCommandHasRepositoryIdArgumentAndForwardsGetWithoutBody` verifies the Rust command accepts a repository id, forwards the backend GET path, and constructs no request body.
- `RepositoryWorkspaceTypeScriptApiInvokesCommandWithRepositoryIdArgument` verifies the TypeScript wrapper invokes `get_repository_workspace` with the expected repository id command argument.
- `WorkflowProjectionBackendEndpointHasRepositoryIdRouteArgumentAndNoBody` verifies the backend workflow route method, required GUID route parameter, and absence of body metadata.
- `WorkflowProjectionRustCommandHasRepositoryIdArgumentAndForwardsGetWithoutBody` verifies the Rust workflow command accepts a repository id, forwards through `backend_get_value`, and constructs no request body.
- `WorkflowProjectionTypeScriptApiInvokesCommandWithRepositoryIdArgument` verifies the TypeScript workflow wrapper invokes `get_workflow_projection` with only the expected repository id command argument.

This is still a narrow pilot check. It does not introduce a general request-contract model, does not verify non-empty command DTOs, and does not classify request compatibility for route, query, or body evolution.

## Contract Artifact Freshness Pilot

Artifact freshness verification is separate from both Oracle fixture comparison and consumer verification.

The Oracle fixture comparison asks whether backend serialization still matches accepted backend-owned fixture truth. Consumer verification asks whether downstream shapes conform to that Oracle-observed truth. Artifact freshness asks whether a tracked contract artifact baseline has moved in lockstep with the Oracle source that justifies it.

The repository dashboard pilot stores its freshness manifest at `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.artifact-freshness.json`, the repository workspace pilot stores its manifest at `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-workspace.artifact-freshness.json`, and the workflow pilot stores its manifest at `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.artifact-freshness.json`. Each manifest records:

- contract identity,
- Oracle source path and SHA-256,
- expected contract artifact path,
- artifact kind,
- expected artifact SHA-256.

Current artifact coverage:

| Contract | Oracle source | Artifact | Artifact kind |
| --- | --- | --- | --- |
| Repository dashboard | `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.golden.json` | `src/CommandCenter.UI/src/types/repositories.ts` | Phase 0 verified contract artifact |
| Repository workspace | `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-workspace.golden.json` | `src/CommandCenter.UI/src/types/repositories.ts` | Phase 0 verified contract artifact |
| Workflow instance | `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.golden.json` | `src/CommandCenter.UI/src/types/workflow.ts` | Phase 0 verified contract artifact |

This artifact is still a manual TypeScript contract file, not a generated Milestone 1.2 output. The freshness verifier intentionally treats it as a Phase 0 verified contract artifact so stale/missing/manual-edit failure semantics exist before the generated contract ecosystem is introduced.

Freshness failure modes:

| Failure mode | Meaning | Remediation |
| --- | --- | --- |
| Stale generated artifact | Oracle source hash changed while the artifact still matches the previous baseline, or both source and artifact changed without updated regeneration evidence. | Review the Oracle change, regenerate or update the artifact through the approved workflow, then update the freshness manifest with evidence. |
| Unexpected manual artifact modification | Artifact hash changed while the Oracle source baseline did not change. | Revert or justify the artifact change through decision/evidence governance before accepting the new baseline. |
| Missing expected artifact | The manifest names an artifact that no longer exists. | Restore the artifact, update the manifest after an approved relocation, or retire the artifact through the compatibility path. |

This pilot does not generate TypeScript, prove artifact determinism, detect manual edits inside generated headers, compare command argument bodies, or certify the generated ecosystem. Those remain Milestone 1.2 responsibilities.

## Generated Contract Pipeline Pilot

Milestone 1.2 begins with a generation-pipeline validation slice for the `repository-dashboard` Oracle family. The pilot consumes the accepted M1.1 model boundary and does not redefine contract identity, taxonomy, normalization, compatibility, or ownership.

The first executable path is:

```text
repository-dashboard.golden.json
  -> repository-dashboard.contract-ir.json
  -> src/CommandCenter.UI/src/contracts/generated/repository-dashboard.generated.ts
  -> repository-dashboard.generated-artifact-freshness.json
```

The generated TypeScript artifact now contains contract metadata, raw generated aliases, and a production-consumer candidate for the `repository-dashboard` family. The metadata records the Oracle-observed field paths, shape kinds, TypeScript primitive categories, and governed field facts needed by the candidate. The raw aliases, including `RepositoryDashboardGeneratedProjection`, are fixture-observed contract shapes generated from the same IR and verified against the Oracle fixture by the TypeScript consumer-verification pipeline. The candidate alias, `RepositoryDashboardConsumerCandidateProjection`, is generated from governed metadata and verified against the manual compatibility wrapper before production adoption. The artifact is protected by `ContractGeneratedArtifactPipelineTests`, which regenerates the IR and TypeScript artifact in memory during normal test runs and supports explicit refresh only when `COMMANDCENTER_UPDATE_GENERATED_CONTRACTS=1` is set.

Current M1.2 pilot boundaries:

- Source authority remains the accepted Oracle fixture and backend serialization path.
- The IR contains contract identity, contract name, root shape, observed fields, shape kinds, TypeScript primitive categories, and governed field metadata for selected production-candidate facts.
- Generated output lives under `src/CommandCenter.UI/src/contracts/generated/`; raw aliases and the production-consumer candidate are verified generated compile-time consumers but are not imported by product code yet.
- Freshness verification now covers the generated TypeScript contract artifact through `repository-dashboard.generated-artifact-freshness.json`.
- Manual TypeScript types, Rust mirrors, and dev Tauri mock payloads remain verified or transitional compatibility consumers until later migration slices.

This pilot does not authorize production imports, Rust command metadata, mock data, command-body schemas, or contract versioning. The raw generated aliases are not schema-complete: enum-like semantic fields are represented as strings, and nullable fields reflect only the observed fixture value. The generated production-consumer candidate is emitted from governed metadata and verified against the manual compatibility wrapper; it remains a replacement candidate until a later migration slice proves production adoption and rollback. If later generation requires concepts absent from M1.1, M1.1 must be reopened through decision governance rather than extending the IR ad hoc.

## Generated TypeScript Consumer Policy

Generated TypeScript output has two distinct categories until the schema model is complete:

| Category | Source | Allowed use | Forbidden use |
| --- | --- | --- | --- |
| Raw observed alias | Oracle fixture through the current generation IR. | Deterministic evidence, freshness verification, consumer-shape comparison, and generator transparency checks. | Production UI imports, semantic enum claims, nullable union claims, compatibility retirement, or runtime validation claims. |
| Production consumer type | Governed contract schema with explicit structural and semantic property metadata. | Replacement target for compatibility wrappers after consumer verification and migration evidence. | Inferring missing schema facts from one fixture, generator-specific exceptions, or hidden downstream strengthening. |
| Compatibility wrapper | Manual or transforming TypeScript artifact tied to an upstream contract identity. | Transitional production imports, semantic strengthening that is visible and verified, and consumer migration staging. | Becoming permanent contract authority or hiding schema policy inside ad hoc adapters. |

Production consumer generation requires explicit policy for each field:

| Property concept | Rule |
| --- | --- |
| Structural JSON kind | May be observed from Oracle fixtures but must remain tied to the accepted contract identity and serialization authority. |
| Nullable by contract | Must be explicit before a generated production type may emit `T | null`; a single non-null or null fixture value is not sufficient evidence. |
| Omitted by contract | Must be explicit before a generated production type may mark a property optional; absence in one fixture is not enough to distinguish omission from an unexercised variant. |
| Semantic enum domain | Must come from backend-owned model metadata, schema policy, or governed contract evidence; it must not be inferred from observed strings. |
| Opaque identity | Must be modeled as an identity-like string only when the contract owner identifies the field as an opaque id or reference. |
| Arbitrary text | Remains plain `string` unless a backend authority exposes a stronger semantic domain. |
| Array ordering | Must identify whether order is semantic, stable-by-projection, or observational only before consumers may rely on it beyond rendering the received order. |
| Empty collection | Empty arrays are preserved as arrays; they do not prove item shape unless schema or another governed fixture variant supplies it. |
| Date/time and duration strings | Remain serialized strings until the contract schema identifies the format and parsing responsibility. |

The approved migration path for the repository dashboard family is:

```text
Raw generated observed alias
  -> governed schema/nullability/semantic metadata
  -> generated production consumer type
  -> compatibility wrapper alias or adapter
  -> existing production consumers
  -> compatibility wrapper retirement evidence
```

This policy keeps the generator transparent: it may project accepted schema facts, but it may not invent semantic domains, nullable unions, optional properties, ordering guarantees, identity meaning, or compatibility rules. If a production consumer needs a stronger type than the current IR can justify, the missing fact must be added to the canonical contract model or recorded as an explicit compatibility-wrapper responsibility before migration.

### Repository Dashboard Schema Metadata Pilot

The first governed schema metadata pilot extends the `repository-dashboard` generation IR and generated TypeScript metadata without changing production UI imports. The pilot is intentionally a metadata step between raw observed aliases and production consumer types.

The metadata records explicit field facts for selected production-migration blockers:

| Fact | Pilot coverage |
| --- | --- |
| Presence | Current selected paths are marked required; optional-by-contract remains unclaimed until a field is explicitly authorized as omitted. |
| Nullability | Nullable fields that appeared as `null` in the single Oracle fixture are modeled as nullable contract facts rather than inferred as `null`-only TypeScript fields. |
| Semantic enum domains | Repository availability, execution readiness, repository execution state, and decision-session lifecycle/transfer fields carry backend-owned enum domains and value sets. |
| Identity roles | Repository id, decision-session id, and continuity artifact id paths carry opaque identity roles. |
| Array ordering | Execution history, decision-session health dimensions, recent transfer lineage, and diagnostics are marked stable by projection for received-order preservation. |
| String formats | Date/time and duration strings remain serialized strings but carry explicit format metadata for future generated production types or validators. |

This pilot does not authorize broad production imports from `src/CommandCenter.UI/src/contracts/generated/repository-dashboard.generated.ts`. The generated raw aliases remain evidence-only. The generated `RepositoryDashboardConsumerCandidateProjection` is now consumed by the production `RepositoryDashboardProjection` compatibility wrapper in `src/CommandCenter.UI/src/types/repositories.ts`, preserving the existing product import boundary while making the wrapper source its dashboard shape from generated contract metadata. `ContractConsumerVerificationTests.RepositoryDashboardProductionConsumerCandidateStructurallyMatchesCompatibilityWrapper` verifies fields, nesting, nullability, primitive kind, and collection shape against the compatibility wrapper. `ContractConsumerVerificationTests.RepositoryDashboardProductionConsumerCandidateCarriesSemanticCompatibilityMetadata` separately verifies representative semantic compatibility for enum domains, nullable summary references, execution repository state, and decision-session status/nullability before direct generated imports or wrapper retirement.

The Slice 0074 compatibility bridge also narrows the repository decision-session summary wrapper aliases to generated candidate subtypes because dashboard and workspace consumers share `src/CommandCenter.UI/src/types/repositories.ts`. This is a compatibility-wrapper bridge, not wrapper retirement: production imports still target `../types`, and direct generated imports remain reserved for a later migration slice with rollback evidence.

Slice 0075 extends the repository-dashboard generated-consumer bridge to the development mock boundary. `src/CommandCenter.UI/src/devTauriMock.ts` now types `dashboardEntry(workspace)` as `RepositoryDashboardConsumerCandidateProjection` imported from the generated repository-dashboard artifact while preserving the existing manual mock payload construction. `ContractConsumerVerificationTests.RepositoryDashboardDevTauriMockUsesGeneratedConsumerCandidateType` guards the bridge, and the existing dev mock Oracle shape-verification tests continue to compare the dashboard entry shape against the backend fixture. This is generated-candidate-typed mock verification, not generated mock artifact replacement.

## Oracle Change Workflow

The Oracle change workflow governs how a detected contract drift becomes an accepted contract baseline. It is procedural during Milestone 0.2 so the review path is explicit before generation, regeneration, and lifecycle automation are introduced in later milestones.

The workflow applies to Oracle-managed contracts when one of these mechanisms reports drift:

- golden fixture comparison,
- downstream consumer verification,
- contract artifact freshness verification.
- request-boundary verification.

### Required Change Record

Every accepted Oracle change must produce evidence that records:

| Field | Requirement |
| --- | --- |
| Contract identity | Stable contract family or endpoint identity affected by the change. |
| Oracle source | Backend projection or command result and serialized fixture path. |
| Drift source | Fixture comparison, consumer verification, artifact freshness, or manual inventory finding. |
| Drift classification | Structural drift, compatibility-review drift, consumer drift, stale artifact, unexpected artifact modification, or missing artifact. |
| Authority owner | Backend authority responsible for the semantic or structural contract change. |
| Affected consumers | Rust shell, TypeScript types/API wrappers, dev Tauri mock, UI resources/hooks/components, tests, docs, or generated artifacts. |
| Compatibility path | Required compatibility field, version rule, consumer migration, or explicit statement that no compatibility path is required. |
| Fixture action | Preserve, update, add, split, or retire the fixture. |
| Artifact action | Preserve, refresh, regenerate, relocate, or retire each verified artifact. |
| Verification | Commands and test results proving the accepted baseline. |
| Rollback path | How to restore the prior accepted contract or compatibility behavior if downstream validation fails. |

### Canonical Sequence

1. Run the relevant Oracle verifier and capture the failing mechanism.
2. Classify the drift before changing fixtures or consumers.
3. Identify whether the backend projection or command result is the intended authority change.
4. If the backend change is not authoritative, repair the producer and keep the existing fixture baseline.
5. If the backend change is authoritative, record compatibility impact and affected consumers before updating any downstream artifact.
6. Update the golden fixture only after the authority and compatibility review is complete.
7. Refresh or regenerate verified consumer artifacts from the accepted fixture path. During Phase 0, manual artifacts may be updated only as verified contract artifacts with evidence.
8. Re-run fixture comparison, consumer verification, artifact freshness, and request-boundary verification for the affected contract family where applicable.
9. Update milestone evidence with the drift classification, fixture/artifact actions, verification commands, and rollback path.
10. Update durable contract documentation when the change alters contract lifecycle, versioning, compatibility, authority ownership, or consumer obligations.

### Classification Rules

| Drift classification | Acceptance rule |
| --- | --- |
| Structural drift | Do not update fixtures first. Prove the backend change is authoritative or restore the previous serialized shape. |
| Compatibility-review drift | Record additive path, consumer impact, compatibility owner, and retirement condition before allowing the fixture baseline to move. |
| Consumer drift | Keep the Oracle fixture unchanged. Update or quarantine the stale consumer with owner, risk, and retirement criteria. |
| Stale artifact | Refresh or regenerate the artifact through the approved workflow, then update the freshness manifest with evidence. |
| Unexpected manual artifact modification | Revert the artifact or accept it only with explicit authority, consumer, verification, and rollback evidence. |
| Missing expected artifact | Restore the artifact, approve relocation, or retire it through a documented compatibility path. |

### Fixture Pilot Workflow

For the current fixture pilots, the minimum fixture comparison command is:

```powershell
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractOracleFixtureTests
```

For the locally certified repository dashboard, repository workspace, and primary workflow projection pilots, the minimum backend acceptance command set also includes:

```powershell
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractConsumerVerificationTests
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractGeneratedArtifactFreshnessTests
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractRequestBoundaryTests
```

The repository workspace local certification used the combined Oracle mechanism filter:

```powershell
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter "FullyQualifiedName~ContractRequestBoundaryTests|FullyQualifiedName~ContractGeneratedArtifactFreshnessTests|FullyQualifiedName~ContractConsumerVerificationTests|FullyQualifiedName~ContractOracleFixtureTests"
```

The full backend test project remains the certification check before accepting a Milestone 0.2 checkpoint.

This workflow is not yet automation. It does not generate artifacts, assign contract versions mechanically, update manifests automatically, or certify additional contract families. Those remain pending Oracle lifecycle and generated ecosystem work.
