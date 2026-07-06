using LoopRelay.Core.Repositories;
using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Services;

public sealed class DecisionSessionRegistry(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository) : IDecisionSessionRegistry
{
    public async Task<DecisionSession> CreateSessionAsync(Guid repositoryId, string createdBy)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await sessionRepository.CreateAsync(
            repository,
            DecisionSession.Create(repositoryId, createdBy, DateTimeOffset.UtcNow));
    }

    public async Task<DecisionSession> ActivateSessionAsync(Guid repositoryId, DecisionSessionId sessionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSession session = await GetRequiredAsync(repository, sessionId);
        if (session.State != DecisionSessionState.Created)
        {
            throw new DecisionSessionConflictException($"Only created decision sessions can be activated: {sessionId}");
        }

        DecisionSession? active = await sessionRepository.GetActiveAsync(repository);
        if (active is not null)
        {
            throw new DecisionSessionConflictException($"An active decision session already exists: {active.Id}");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return await sessionRepository.UpdateAsync(
            repository,
            session with
            {
                State = DecisionSessionState.Active,
                ActivatedAt = now,
                Metadata = session.Metadata with { UpdatedAt = now }
            });
    }

    public async Task<DecisionSession> MarkTransferPendingAsync(Guid repositoryId, DecisionSessionId sessionId, string reason)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSession session = await GetRequiredAsync(repository, sessionId);
        if (session.State != DecisionSessionState.Active)
        {
            throw new DecisionSessionConflictException($"Only active decision sessions can be marked transfer pending: {sessionId}");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return await sessionRepository.UpdateAsync(
            repository,
            session with
            {
                State = DecisionSessionState.TransferPending,
                Metadata = session.Metadata with { TransferReason = reason, UpdatedAt = now }
            });
    }

    public async Task<DecisionSession> MarkTransferredAsync(Guid repositoryId, DecisionSessionId sourceSessionId, DecisionSessionId targetSessionId, string reason)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSession source = await GetRequiredAsync(repository, sourceSessionId);
        DecisionSession target = await GetRequiredAsync(repository, targetSessionId);
        if (source.State != DecisionSessionState.TransferPending)
        {
            throw new DecisionSessionConflictException($"Only transfer-pending decision sessions can be transferred: {sourceSessionId}");
        }

        if (target.State != DecisionSessionState.Active)
        {
            throw new DecisionSessionConflictException($"Transfer target must be active: {targetSessionId}");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return await sessionRepository.UpdateAsync(
            repository,
            source with
            {
                State = DecisionSessionState.Transferred,
                RetiredAt = now,
                Metadata = source.Metadata with
                {
                    TransferReason = reason,
                    TransferredToSessionId = targetSessionId,
                    UpdatedAt = now
                }
            });
    }

    public async Task<DecisionSession> RetireSessionAsync(Guid repositoryId, DecisionSessionId sessionId, string reason)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSession session = await GetRequiredAsync(repository, sessionId);
        if (session.State is not (DecisionSessionState.Active or DecisionSessionState.TransferPending))
        {
            throw new DecisionSessionConflictException($"Only active or transfer-pending decision sessions can be retired: {sessionId}");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return await sessionRepository.UpdateAsync(
            repository,
            session with
            {
                State = DecisionSessionState.Retired,
                RetiredAt = now,
                Metadata = session.Metadata with { TransferReason = reason, UpdatedAt = now }
            });
    }

    public async Task<DecisionSession?> GetActiveSessionAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await sessionRepository.GetActiveAsync(repository);
    }

    private async Task<DecisionSession> GetRequiredAsync(Repository repository, DecisionSessionId sessionId)
    {
        return await sessionRepository.GetAsync(repository, sessionId)
            ?? throw new KeyNotFoundException($"Decision session was not found: {sessionId}");
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }
}
