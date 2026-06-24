using System.Text.Json;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Services;

public sealed class FileSystemDecisionSessionRepository(IArtifactStore artifactStore) : IDecisionSessionRepository
{
    public async Task<DecisionSession> CreateAsync(Repository repository, DecisionSession session)
    {
        ValidateRepositoryOwnership(repository, session);
        List<DecisionSession> sessions = (await ListAsync(repository)).ToList();
        if (sessions.Any(existing => existing.Id == session.Id))
        {
            throw new DecisionSessionConflictException($"Decision session already exists: {session.Id}");
        }

        sessions.Add(session);
        await WriteRegistryAsync(repository, sessions);
        return session;
    }

    public async Task<DecisionSession> UpdateAsync(Repository repository, DecisionSession session)
    {
        ValidateRepositoryOwnership(repository, session);
        List<DecisionSession> sessions = (await ListAsync(repository)).ToList();
        int index = sessions.FindIndex(existing => existing.Id == session.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Decision session was not found: {session.Id}");
        }

        sessions[index] = session;
        await WriteRegistryAsync(repository, sessions);
        return session;
    }

    public async Task<DecisionSession?> GetAsync(Repository repository, DecisionSessionId sessionId)
    {
        return (await ListAsync(repository)).FirstOrDefault(session => session.Id == sessionId);
    }

    public async Task<DecisionSession?> GetActiveAsync(Repository repository)
    {
        IReadOnlyList<DecisionSession> active = (await ListAsync(repository))
            .Where(session => session.State == DecisionSessionState.Active)
            .ToArray();
        return active.Count switch
        {
            0 => null,
            1 => active[0],
            _ => throw new DecisionSessionConflictException("More than one active decision session exists for this repository.")
        };
    }

    public async Task<IReadOnlyList<DecisionSession>> ListAsync(Repository repository)
    {
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.RegistryJson());
        string? json = await artifactStore.ReadAsync(path);
        if (json is null)
        {
            return [];
        }

        DecisionSessionArtifactDocument<IReadOnlyList<DecisionSessionRecord>>? document =
            JsonSerializer.Deserialize<DecisionSessionArtifactDocument<IReadOnlyList<DecisionSessionRecord>>>(
                json,
                DecisionSessionJson.Options);
        if (document is null)
        {
            throw new DecisionSessionValidationException("Decision session registry could not be deserialized.");
        }

        if (!string.Equals(document.SchemaVersion, DecisionSessionArtifactPaths.SchemaVersion, StringComparison.Ordinal))
        {
            throw new DecisionSessionValidationException($"Unsupported decision session schema version '{document.SchemaVersion}'.");
        }

        if (document.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session registry belongs to a different repository.");
        }

        DecisionSession[] sessions = document.Payload.Select(record => record.Session).ToArray();
        DecisionSessionValidationResult validation = Validate(repository, sessions);
        if (!validation.IsValid)
        {
            throw new DecisionSessionValidationException(string.Join("; ", validation.Errors));
        }

        return Sort(sessions);
    }

    internal async Task<DecisionSessionValidationResult> ValidateAsync(Repository repository)
    {
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.RegistryJson());
        string? json = await artifactStore.ReadAsync(path);
        if (json is null)
        {
            return DecisionSessionValidationResult.Valid;
        }

        try
        {
            DecisionSessionArtifactDocument<IReadOnlyList<DecisionSessionRecord>>? document =
                JsonSerializer.Deserialize<DecisionSessionArtifactDocument<IReadOnlyList<DecisionSessionRecord>>>(
                    json,
                    DecisionSessionJson.Options);
            if (document is null)
            {
                return new DecisionSessionValidationResult(false, ["Decision session registry could not be deserialized."], []);
            }

            if (!string.Equals(document.SchemaVersion, DecisionSessionArtifactPaths.SchemaVersion, StringComparison.Ordinal))
            {
                return new DecisionSessionValidationResult(false, [$"Unsupported decision session schema version '{document.SchemaVersion}'."], []);
            }

            if (document.RepositoryId != repository.Id)
            {
                return new DecisionSessionValidationResult(false, ["Decision session registry belongs to a different repository."], []);
            }

            return Validate(repository, document.Payload.Select(record => record.Session).ToArray());
        }
        catch (JsonException exception)
        {
            return new DecisionSessionValidationResult(false, [$"Decision session registry JSON is invalid: {exception.Message}"], []);
        }
    }

    private async Task WriteRegistryAsync(Repository repository, IReadOnlyList<DecisionSession> sessions)
    {
        DecisionSessionValidationResult validation = Validate(repository, sessions);
        if (!validation.IsValid)
        {
            throw new DecisionSessionValidationException(string.Join("; ", validation.Errors));
        }

        DecisionSessionRecord[] records = Sort(sessions)
            .Select(session => new DecisionSessionRecord(session))
            .ToArray();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var document = new DecisionSessionArtifactDocument<IReadOnlyList<DecisionSessionRecord>>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            records.Select(record => record.Session.CreatedAt).DefaultIfEmpty(now).Min(),
            now,
            records);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.RegistryJson());
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    private static DecisionSessionValidationResult Validate(Repository repository, IReadOnlyList<DecisionSession> sessions)
    {
        var errors = new List<string>();
        foreach (IGrouping<DecisionSessionId, DecisionSession> duplicate in sessions.GroupBy(session => session.Id).Where(group => group.Count() > 1))
        {
            errors.Add($"Duplicate decision session id: {duplicate.Key}");
        }

        foreach (DecisionSession session in sessions)
        {
            if (session.RepositoryId != repository.Id || session.Ownership.RepositoryId != repository.Id)
            {
                errors.Add($"Decision session belongs to a different repository: {session.Id}");
            }

            if (session.ActivatedAt is not null && session.ActivatedAt < session.CreatedAt)
            {
                errors.Add($"Decision session was activated before creation: {session.Id}");
            }

            if (session.RetiredAt is not null && session.RetiredAt < session.CreatedAt)
            {
                errors.Add($"Decision session was retired before creation: {session.Id}");
            }

            if (session.ActivatedAt is not null && session.RetiredAt is not null && session.ActivatedAt > session.RetiredAt)
            {
                errors.Add($"Decision session activation is after retirement: {session.Id}");
            }
        }

        int activeCount = sessions.Count(session => session.State == DecisionSessionState.Active);
        if (activeCount > 1)
        {
            errors.Add("More than one active decision session exists for this repository.");
        }

        return errors.Count == 0
            ? DecisionSessionValidationResult.Valid
            : new DecisionSessionValidationResult(false, errors, []);
    }

    private static IReadOnlyList<DecisionSession> Sort(IReadOnlyList<DecisionSession> sessions)
    {
        return sessions
            .OrderBy(session => session.CreatedAt)
            .ThenBy(session => session.Id.Value)
            .ToArray();
    }

    private static void ValidateRepositoryOwnership(Repository repository, DecisionSession session)
    {
        if (session.RepositoryId != repository.Id || session.Ownership.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision session belongs to a different repository.");
        }
    }
}
