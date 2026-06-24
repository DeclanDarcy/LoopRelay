using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionRecoveryService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository) : IDecisionSessionRecoveryService
{
    public async Task<DecisionSessionDiagnostics> GetDiagnosticsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSessionValidationResult validation = sessionRepository is FileSystemDecisionSessionRepository fileSystemRepository
            ? await fileSystemRepository.ValidateAsync(repository)
            : await ValidateFromRepositoryAsync(repository);

        int sessionCount = 0;
        int activeSessionCount = 0;
        if (validation.IsValid)
        {
            IReadOnlyList<DecisionSession> sessions = await sessionRepository.ListAsync(repository);
            sessionCount = sessions.Count;
            activeSessionCount = sessions.Count(session => session.State == DecisionSessionState.Active);
        }

        return new DecisionSessionDiagnostics(
            repositoryId,
            validation.IsValid,
            sessionCount,
            activeSessionCount,
            validation.Errors,
            validation.Warnings,
            DateTimeOffset.UtcNow);
    }

    private async Task<DecisionSessionValidationResult> ValidateFromRepositoryAsync(Repository repository)
    {
        try
        {
            IReadOnlyList<DecisionSession> sessions = await sessionRepository.ListAsync(repository);
            int activeCount = sessions.Count(session => session.State == DecisionSessionState.Active);
            return activeCount > 1
                ? new DecisionSessionValidationResult(false, ["More than one active decision session exists for this repository."], [])
                : DecisionSessionValidationResult.Valid;
        }
        catch (InvalidOperationException exception)
        {
            return new DecisionSessionValidationResult(false, [exception.Message], []);
        }
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }
}
