# Milestone 1: Foundation And Registry

Objective: establish the decision-session domain, persistence, registry authority, and recovery validation.

### Domain

Add primitives:

- [ ] `DecisionSessionId`: immutable strongly typed identity around `Guid`.
- [ ] `DecisionSessionState`: `Created`, `Active`, `TransferPending`, `Transferred`, `Retired`.
- [ ] `DecisionSessionOwnership`: repository id, created by, created at.

Add models:

- [ ] `DecisionSession`
- [ ] `DecisionSessionMetadata`
- [ ] `DecisionSessionDiagnostics`
- [ ] `DecisionSessionRecord`
- [ ] `DecisionSessionProjection`

`DecisionSession` fields:

- [ ] `DecisionSessionId Id`
- [ ] `Guid RepositoryId`
- [ ] `DecisionSessionState State`
- [ ] `DateTimeOffset CreatedAt`
- [ ] `DateTimeOffset? ActivatedAt`
- [ ] `DateTimeOffset? RetiredAt`
- [ ] `DecisionSessionOwnership Ownership`
- [ ] `DecisionSessionMetadata Metadata`

Do not add metrics, economics, coherence, reuse score, transfer score, token count, or transfer metadata to the aggregate.

### Contracts

Repository operations:

- [ ] `Task<DecisionSession> CreateAsync(Repository repository, DecisionSession session)`
- [ ] `Task<DecisionSession> UpdateAsync(Repository repository, DecisionSession session)`
- [ ] `Task<DecisionSession?> GetAsync(Repository repository, DecisionSessionId sessionId)`
- [ ] `Task<DecisionSession?> GetActiveAsync(Repository repository)`
- [ ] `Task<IReadOnlyList<DecisionSession>> ListAsync(Repository repository)`

Registry operations:

- [ ] `Task<DecisionSession> CreateSessionAsync(Guid repositoryId, string createdBy)`
- [ ] `Task<DecisionSession> ActivateSessionAsync(Guid repositoryId, DecisionSessionId sessionId)`
- [ ] `Task<DecisionSession> MarkTransferPendingAsync(Guid repositoryId, DecisionSessionId sessionId, string reason)`
- [ ] `Task<DecisionSession> MarkTransferredAsync(Guid repositoryId, DecisionSessionId sourceSessionId, DecisionSessionId targetSessionId, string reason)`
- [ ] `Task<DecisionSession> RetireSessionAsync(Guid repositoryId, DecisionSessionId sessionId, string reason)`
- [ ] `Task<DecisionSession?> GetActiveSessionAsync(Guid repositoryId)`

Recovery operations:

- [ ] Load persisted sessions.
- [ ] Validate duplicate ids and active-session count.
- [ ] Validate timestamp consistency.
- [ ] Produce diagnostics.
- [ ] Do not repair state in the initial implementation.

### Registry Rules

- [ ] `CreateSessionAsync` creates a `Created` session.
- [ ] `ActivateSessionAsync` allows `Created -> Active`.
- [ ] Activating a session fails if another session is already active in the same repository.
- [ ] Activating an already active session fails.
- [ ] Activating `Transferred` or `Retired` fails.
- [ ] `MarkTransferPendingAsync` allows `Active -> TransferPending`.
- [ ] `RetireSessionAsync` allows `Active -> Retired` and `TransferPending -> Retired`.
- [ ] `MarkTransferredAsync` is only used by transfer execution after replacement session creation.
- [ ] `GetActiveSessionAsync` returns null for zero active sessions, returns the active session for one active session, and fails with diagnostics for more than one active session.

### Persistence

Implement:

- [ ] `FileSystemDecisionSessionRepository`
- [ ] `DecisionSessionValidationResult`
- [ ] `DecisionSessionRegistryDiagnostics`
- [ ] `DecisionSessionRecoveryService`

Persistence rules:

- [ ] Store all sessions in `.agents/decision-sessions/registry.json`.
- [ ] Keep records deterministic and ordered by creation time, then session id.
- [ ] Reject duplicate ids.
- [ ] Reject records whose `RepositoryId` does not match the repository being read.
- [ ] Reject invalid timestamp relationships such as `ActivatedAt > RetiredAt`.

### Backend Endpoints

- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/active`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/diagnostics`

Use a route group and shared `HandleAsync` error mapping similar to `ReasoningEndpoints`.

### Tests

- [ ] Session id stability.
- [ ] State enum round-trip through JSON.
- [ ] Aggregate creation.
- [ ] Repository ownership validation.
- [ ] Repository save/load/list.
- [ ] Create, activate, and retire sessions.
- [ ] Zero active sessions allowed.
- [ ] One active session allowed.
- [ ] Two active sessions rejected.
- [ ] Duplicate ids rejected.
- [ ] Invalid timestamp state produces validation diagnostics.
- [ ] Unsupported schema version rejected.
- [ ] Cross-repository payload rejected.
- [ ] Endpoints return list, active session, and diagnostics.

### Exit Criteria

- [ ] Domain compiles.
- [ ] Service registration exists.
- [ ] Registry is operational.
- [ ] Persistence is operational.
- [ ] Recovery validates persisted state.
- [ ] The single-active-session invariant is enforced.
- [ ] The system can answer which governance session is active, or that none is active.




