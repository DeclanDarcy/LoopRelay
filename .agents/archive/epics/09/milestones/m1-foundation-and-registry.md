# Milestone 1: Foundation And Registry

Objective: establish the decision-session domain, persistence, registry authority, and recovery validation.

### Domain

Add primitives:

- [x] `DecisionSessionId`: immutable strongly typed identity around `Guid`.
- [x] `DecisionSessionState`: `Created`, `Active`, `TransferPending`, `Transferred`, `Retired`.
- [x] `DecisionSessionOwnership`: repository id, created by, created at.

Add models:

- [x] `DecisionSession`
- [x] `DecisionSessionMetadata`
- [x] `DecisionSessionDiagnostics`
- [x] `DecisionSessionRecord`
- [x] `DecisionSessionProjection`

`DecisionSession` fields:

- [x] `DecisionSessionId Id`
- [x] `Guid RepositoryId`
- [x] `DecisionSessionState State`
- [x] `DateTimeOffset CreatedAt`
- [x] `DateTimeOffset? ActivatedAt`
- [x] `DateTimeOffset? RetiredAt`
- [x] `DecisionSessionOwnership Ownership`
- [x] `DecisionSessionMetadata Metadata`

Do not add metrics, economics, coherence, reuse score, transfer score, token count, or transfer metadata to the aggregate.

### Contracts

Repository operations:

- [x] `Task<DecisionSession> CreateAsync(Repository repository, DecisionSession session)`
- [x] `Task<DecisionSession> UpdateAsync(Repository repository, DecisionSession session)`
- [x] `Task<DecisionSession?> GetAsync(Repository repository, DecisionSessionId sessionId)`
- [x] `Task<DecisionSession?> GetActiveAsync(Repository repository)`
- [x] `Task<IReadOnlyList<DecisionSession>> ListAsync(Repository repository)`

Registry operations:

- [x] `Task<DecisionSession> CreateSessionAsync(Guid repositoryId, string createdBy)`
- [x] `Task<DecisionSession> ActivateSessionAsync(Guid repositoryId, DecisionSessionId sessionId)`
- [x] `Task<DecisionSession> MarkTransferPendingAsync(Guid repositoryId, DecisionSessionId sessionId, string reason)`
- [x] `Task<DecisionSession> MarkTransferredAsync(Guid repositoryId, DecisionSessionId sourceSessionId, DecisionSessionId targetSessionId, string reason)`
- [x] `Task<DecisionSession> RetireSessionAsync(Guid repositoryId, DecisionSessionId sessionId, string reason)`
- [x] `Task<DecisionSession?> GetActiveSessionAsync(Guid repositoryId)`

Recovery operations:

- [x] Load persisted sessions.
- [x] Validate duplicate ids and active-session count.
- [x] Validate timestamp consistency.
- [x] Produce diagnostics.
- [x] Do not repair state in the initial implementation.

### Registry Rules

- [x] `CreateSessionAsync` creates a `Created` session.
- [x] `ActivateSessionAsync` allows `Created -> Active`.
- [x] Activating a session fails if another session is already active in the same repository.
- [x] Activating an already active session fails.
- [x] Activating `Transferred` or `Retired` fails.
- [x] `MarkTransferPendingAsync` allows `Active -> TransferPending`.
- [x] `RetireSessionAsync` allows `Active -> Retired` and `TransferPending -> Retired`.
- [x] `MarkTransferredAsync` is only used by transfer execution after replacement session creation.
- [x] `GetActiveSessionAsync` returns null for zero active sessions, returns the active session for one active session, and fails with diagnostics for more than one active session.

### Persistence

Implement:

- [x] `FileSystemDecisionSessionRepository`
- [x] `DecisionSessionValidationResult`
- [x] `DecisionSessionRegistryDiagnostics`
- [x] `DecisionSessionRecoveryService`

Persistence rules:

- [x] Store all sessions in `.agents/decision-sessions/registry.json`.
- [x] Keep records deterministic and ordered by creation time, then session id.
- [x] Reject duplicate ids.
- [x] Reject records whose `RepositoryId` does not match the repository being read.
- [x] Reject invalid timestamp relationships such as `ActivatedAt > RetiredAt`.

### Backend Endpoints

- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/active`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/diagnostics`

Use a route group and shared `HandleAsync` error mapping similar to `ReasoningEndpoints`.

### Tests

- [x] Session id stability.
- [x] State enum round-trip through JSON.
- [x] Aggregate creation.
- [x] Repository ownership validation.
- [x] Repository save/load/list.
- [x] Create, activate, and retire sessions.
- [x] Zero active sessions allowed.
- [x] One active session allowed.
- [x] Two active sessions rejected.
- [x] Duplicate ids rejected.
- [x] Invalid timestamp state produces validation diagnostics.
- [x] Unsupported schema version rejected.
- [x] Cross-repository payload rejected.
- [x] Endpoints return list, active session, and diagnostics.

### Exit Criteria

- [x] Domain compiles.
- [x] Service registration exists.
- [x] Registry is operational.
- [x] Persistence is operational.
- [x] Recovery validates persisted state.
- [x] The single-active-session invariant is enforced.
- [x] The system can answer which governance session is active, or that none is active.




