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

    public async Task<DecisionSessionMetricsSnapshot?> ReadMetricsSnapshotAsync(Repository repository)
    {
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.MetricsSnapshotJson());
        string? json = await artifactStore.ReadAsync(path);
        if (json is null)
        {
            return null;
        }

        DecisionSessionArtifactDocument<DecisionSessionMetricsSnapshot>? document =
            JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionMetricsSnapshot>>(
                json,
                DecisionSessionJson.Options);
        if (document is null)
        {
            throw new DecisionSessionValidationException("Decision session metrics snapshot could not be deserialized.");
        }

        ValidateDocument(repository, document, "Decision session metrics snapshot");
        if (document.Payload.RepositoryId != repository.Id || document.Payload.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session metrics snapshot belongs to a different repository.");
        }

        return document.Payload;
    }

    public async Task WriteMetricsSnapshotAsync(Repository repository, DecisionSessionMetricsSnapshot snapshot)
    {
        if (snapshot.RepositoryId != repository.Id || snapshot.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session metrics snapshot belongs to a different repository.");
        }

        var document = new DecisionSessionArtifactDocument<DecisionSessionMetricsSnapshot>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            snapshot.GeneratedAt,
            DateTimeOffset.UtcNow,
            snapshot);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.MetricsSnapshotJson());
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    public async Task<DecisionSessionEconomicsSnapshot?> ReadEconomicsSnapshotAsync(Repository repository)
    {
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.EconomicsSnapshotJson());
        string? json = await artifactStore.ReadAsync(path);
        if (json is null)
        {
            return null;
        }

        DecisionSessionArtifactDocument<DecisionSessionEconomicsSnapshot>? document =
            JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionEconomicsSnapshot>>(
                json,
                DecisionSessionJson.Options);
        if (document is null)
        {
            throw new DecisionSessionValidationException("Decision session economics snapshot could not be deserialized.");
        }

        ValidateDocument(repository, document, "Decision session economics snapshot");
        if (document.Payload.RepositoryId != repository.Id || document.Payload.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session economics snapshot belongs to a different repository.");
        }

        return document.Payload;
    }

    public async Task WriteEconomicsSnapshotAsync(Repository repository, DecisionSessionEconomicsSnapshot snapshot)
    {
        if (snapshot.RepositoryId != repository.Id || snapshot.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session economics snapshot belongs to a different repository.");
        }

        var document = new DecisionSessionArtifactDocument<DecisionSessionEconomicsSnapshot>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            snapshot.GeneratedAt,
            DateTimeOffset.UtcNow,
            snapshot);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.EconomicsSnapshotJson());
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    public async Task<DecisionSessionCoherenceSnapshot?> ReadCoherenceSnapshotAsync(Repository repository)
    {
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.CoherenceSnapshotJson());
        string? json = await artifactStore.ReadAsync(path);
        if (json is null)
        {
            return null;
        }

        DecisionSessionArtifactDocument<DecisionSessionCoherenceSnapshot>? document =
            JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionCoherenceSnapshot>>(
                json,
                DecisionSessionJson.Options);
        if (document is null)
        {
            throw new DecisionSessionValidationException("Decision session coherence snapshot could not be deserialized.");
        }

        ValidateDocument(repository, document, "Decision session coherence snapshot");
        if (document.Payload.RepositoryId != repository.Id || document.Payload.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session coherence snapshot belongs to a different repository.");
        }

        return document.Payload;
    }

    public async Task WriteCoherenceSnapshotAsync(Repository repository, DecisionSessionCoherenceSnapshot snapshot)
    {
        if (snapshot.RepositoryId != repository.Id || snapshot.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session coherence snapshot belongs to a different repository.");
        }

        var document = new DecisionSessionArtifactDocument<DecisionSessionCoherenceSnapshot>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            snapshot.GeneratedAt,
            DateTimeOffset.UtcNow,
            snapshot);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.CoherenceSnapshotJson());
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    public async Task<DecisionSessionLifecycleSnapshot?> ReadLifecyclePolicySnapshotAsync(Repository repository)
    {
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.LifecyclePolicySnapshotJson());
        string? json = await artifactStore.ReadAsync(path);
        if (json is null)
        {
            return null;
        }

        DecisionSessionArtifactDocument<DecisionSessionLifecycleSnapshot>? document =
            JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionLifecycleSnapshot>>(
                json,
                DecisionSessionJson.Options);
        if (document is null)
        {
            throw new DecisionSessionValidationException("Decision session lifecycle policy snapshot could not be deserialized.");
        }

        ValidateDocument(repository, document, "Decision session lifecycle policy snapshot");
        if (document.Payload.RepositoryId != repository.Id || document.Payload.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session lifecycle policy snapshot belongs to a different repository.");
        }

        if (document.Payload.Diagnostics.Inputs.Session.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session lifecycle policy snapshot belongs to a different repository.");
        }

        return document.Payload;
    }

    public async Task WriteLifecyclePolicySnapshotAsync(Repository repository, DecisionSessionLifecycleSnapshot snapshot)
    {
        if (snapshot.RepositoryId != repository.Id ||
            snapshot.Diagnostics.RepositoryId != repository.Id ||
            snapshot.Diagnostics.Inputs.Session.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session lifecycle policy snapshot belongs to a different repository.");
        }

        var document = new DecisionSessionArtifactDocument<DecisionSessionLifecycleSnapshot>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            snapshot.GeneratedAt,
            DateTimeOffset.UtcNow,
            snapshot);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.LifecyclePolicySnapshotJson());
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    public async Task<DecisionSessionTransferEligibilitySnapshot?> ReadTransferEligibilitySnapshotAsync(Repository repository)
    {
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.TransferEligibilitySnapshotJson());
        string? json = await artifactStore.ReadAsync(path);
        if (json is null)
        {
            return null;
        }

        DecisionSessionArtifactDocument<DecisionSessionTransferEligibilitySnapshot>? document =
            JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionTransferEligibilitySnapshot>>(
                json,
                DecisionSessionJson.Options);
        if (document is null)
        {
            throw new DecisionSessionValidationException("Decision session transfer eligibility snapshot could not be deserialized.");
        }

        ValidateDocument(repository, document, "Decision session transfer eligibility snapshot");
        if (document.Payload.RepositoryId != repository.Id || document.Payload.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session transfer eligibility snapshot belongs to a different repository.");
        }

        return document.Payload;
    }

    public async Task WriteTransferEligibilitySnapshotAsync(Repository repository, DecisionSessionTransferEligibilitySnapshot snapshot)
    {
        if (snapshot.RepositoryId != repository.Id || snapshot.Diagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session transfer eligibility snapshot belongs to a different repository.");
        }

        if (snapshot.Diagnostics.Inputs.ActiveSession is not null &&
            snapshot.Diagnostics.Inputs.ActiveSession.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session transfer eligibility snapshot belongs to a different repository.");
        }

        if (snapshot.Diagnostics.Inputs.Evidence is not null &&
            snapshot.Diagnostics.Inputs.Evidence.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session transfer eligibility snapshot belongs to a different repository.");
        }

        var document = new DecisionSessionArtifactDocument<DecisionSessionTransferEligibilitySnapshot>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            snapshot.GeneratedAt,
            DateTimeOffset.UtcNow,
            snapshot);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.TransferEligibilitySnapshotJson());
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    public async Task<IReadOnlyList<DecisionSessionContinuityArtifact>> ListContinuityArtifactsAsync(Repository repository)
    {
        string root = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.ContinuityArtifactsDirectory());
        IReadOnlyList<string> files = (await artifactStore.ListAsync(root, "*.json"))
            .Where(file => Path.GetFileName(file).StartsWith("continuity.", StringComparison.Ordinal))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var artifacts = new List<DecisionSessionContinuityArtifact>();
        foreach (string file in files)
        {
            string? json = await artifactStore.ReadAsync(file);
            if (json is null)
            {
                continue;
            }

            DecisionSessionArtifactDocument<DecisionSessionContinuityArtifact>? document =
                JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionContinuityArtifact>>(
                    json,
                    DecisionSessionJson.Options);
            if (document is null)
            {
                throw new DecisionSessionValidationException("Decision session continuity artifact could not be deserialized.");
            }

            ValidateDocument(repository, document, "Decision session continuity artifact");
            ValidateContinuityArtifact(repository, document.Payload);
            artifacts.Add(document.Payload);
        }

        return artifacts
            .OrderBy(artifact => artifact.CreatedAt)
            .ThenBy(artifact => artifact.ArtifactId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<DecisionSessionContinuityArtifact?> ReadContinuityArtifactAsync(Repository repository, string artifactId)
    {
        ValidateContinuityArtifactId(artifactId);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.ContinuityArtifactJson(artifactId));
        string? json = await artifactStore.ReadAsync(path);
        if (json is null)
        {
            return null;
        }

        DecisionSessionArtifactDocument<DecisionSessionContinuityArtifact>? document =
            JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionContinuityArtifact>>(
                json,
                DecisionSessionJson.Options);
        if (document is null)
        {
            throw new DecisionSessionValidationException("Decision session continuity artifact could not be deserialized.");
        }

        ValidateDocument(repository, document, "Decision session continuity artifact");
        ValidateContinuityArtifact(repository, document.Payload);
        if (!string.Equals(document.Payload.ArtifactId, artifactId, StringComparison.Ordinal))
        {
            throw new DecisionSessionValidationException("Decision session continuity artifact id does not match its path.");
        }

        return document.Payload;
    }

    public async Task WriteContinuityArtifactAsync(Repository repository, DecisionSessionContinuityArtifact artifact)
    {
        ValidateContinuityArtifact(repository, artifact);
        var document = new DecisionSessionArtifactDocument<DecisionSessionContinuityArtifact>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            artifact.CreatedAt,
            DateTimeOffset.UtcNow,
            artifact);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.ContinuityArtifactJson(artifact.ArtifactId));
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    public async Task<IReadOnlyList<DecisionSessionTransfer>> ListTransfersAsync(Repository repository)
    {
        string root = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.TransfersDirectory());
        IReadOnlyList<string> files = (await artifactStore.ListAsync(root, "*.json"))
            .Where(file => Path.GetFileName(file).StartsWith("transfer.", StringComparison.Ordinal))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var transfers = new List<DecisionSessionTransfer>();
        foreach (string file in files)
        {
            string? json = await artifactStore.ReadAsync(file);
            if (json is null)
            {
                continue;
            }

            DecisionSessionArtifactDocument<DecisionSessionTransfer>? document =
                JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionTransfer>>(
                    json,
                    DecisionSessionJson.Options);
            if (document is null)
            {
                throw new DecisionSessionValidationException("Decision session transfer could not be deserialized.");
            }

            ValidateDocument(repository, document, "Decision session transfer");
            ValidateTransfer(repository, document.Payload);
            if (!string.Equals(Path.GetFileName(file), document.Payload.TransferId, StringComparison.Ordinal))
            {
                throw new DecisionSessionValidationException("Decision session transfer id does not match its path.");
            }

            transfers.Add(document.Payload);
        }

        return transfers
            .OrderBy(transfer => transfer.StartedAt)
            .ThenBy(transfer => transfer.TransferId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task WriteTransferAsync(Repository repository, DecisionSessionTransfer transfer)
    {
        ValidateTransfer(repository, transfer);
        var document = new DecisionSessionArtifactDocument<DecisionSessionTransfer>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            transfer.StartedAt,
            DateTimeOffset.UtcNow,
            transfer);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.TransferJson(transfer.TransferId));
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    public async Task<IReadOnlyList<DecisionSessionRecoveryResult>> ListRecoveryResultsAsync(Repository repository)
    {
        string root = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.RecoveryDirectory());
        IReadOnlyList<string> files = (await artifactStore.ListAsync(root, "*.json"))
            .Where(file => Path.GetFileName(file).StartsWith("recovery.", StringComparison.Ordinal))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new List<DecisionSessionRecoveryResult>();
        foreach (string file in files)
        {
            string? json = await artifactStore.ReadAsync(file);
            if (json is null)
            {
                continue;
            }

            DecisionSessionArtifactDocument<DecisionSessionRecoveryResult>? document =
                JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionRecoveryResult>>(
                    json,
                    DecisionSessionJson.Options);
            if (document is null)
            {
                throw new DecisionSessionValidationException("Decision session recovery result could not be deserialized.");
            }

            ValidateDocument(repository, document, "Decision session recovery result");
            ValidateRecoveryResult(repository, document.Payload);
            if (!string.Equals(Path.GetFileName(file), document.Payload.RecoveryId, StringComparison.Ordinal))
            {
                throw new DecisionSessionValidationException("Decision session recovery id does not match its path.");
            }

            results.Add(document.Payload);
        }

        return results
            .OrderBy(result => result.RecoveredAt)
            .ThenBy(result => result.RecoveryId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task WriteRecoveryResultAsync(Repository repository, DecisionSessionRecoveryResult result)
    {
        ValidateRecoveryResult(repository, result);
        var document = new DecisionSessionArtifactDocument<DecisionSessionRecoveryResult>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            result.RecoveredAt,
            DateTimeOffset.UtcNow,
            result);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.RecoveryJson(result.RecoveryId));
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
    }

    public async Task<IReadOnlyList<DecisionSessionCertificationReport>> ListCertificationReportsAsync(Repository repository)
    {
        string root = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.CertificationDirectory());
        IReadOnlyList<string> files = (await artifactStore.ListAsync(root, "*.json"))
            .Where(file => Path.GetFileName(file).StartsWith("certification.", StringComparison.Ordinal))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var reports = new List<DecisionSessionCertificationReport>();
        foreach (string file in files)
        {
            string? json = await artifactStore.ReadAsync(file);
            if (json is null)
            {
                continue;
            }

            DecisionSessionArtifactDocument<DecisionSessionCertificationReport>? document =
                JsonSerializer.Deserialize<DecisionSessionArtifactDocument<DecisionSessionCertificationReport>>(
                    json,
                    DecisionSessionJson.Options);
            if (document is null)
            {
                throw new DecisionSessionValidationException("Decision session certification report could not be deserialized.");
            }

            ValidateDocument(repository, document, "Decision session certification report");
            ValidateCertificationReport(repository, document.Payload);
            if (!string.Equals(Path.GetFileName(file), document.Payload.ReportId, StringComparison.Ordinal))
            {
                throw new DecisionSessionValidationException("Decision session certification report id does not match its path.");
            }

            reports.Add(document.Payload);
        }

        return reports
            .OrderBy(report => report.GeneratedAt)
            .ThenBy(report => report.ReportId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task WriteCertificationReportAsync(Repository repository, DecisionSessionCertificationReport report)
    {
        ValidateCertificationReport(repository, report);
        var document = new DecisionSessionArtifactDocument<DecisionSessionCertificationReport>(
            DecisionSessionArtifactPaths.SchemaVersion,
            repository.Id,
            report.GeneratedAt,
            DateTimeOffset.UtcNow,
            report);
        string path = DecisionSessionArtifactPaths.Resolve(repository, DecisionSessionArtifactPaths.CertificationJson(report.ReportId));
        await artifactStore.WriteAsync(path, JsonSerializer.Serialize(document, DecisionSessionJson.Options));
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

    private static void ValidateDocument<T>(Repository repository, DecisionSessionArtifactDocument<T> document, string documentName)
    {
        if (!string.Equals(document.SchemaVersion, DecisionSessionArtifactPaths.SchemaVersion, StringComparison.Ordinal))
        {
            throw new DecisionSessionValidationException($"Unsupported decision session schema version '{document.SchemaVersion}'.");
        }

        if (document.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException($"{documentName} belongs to a different repository.");
        }
    }

    private static void ValidateContinuityArtifact(Repository repository, DecisionSessionContinuityArtifact artifact)
    {
        ValidateContinuityArtifactId(artifact.ArtifactId);
        if (artifact.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session continuity artifact belongs to a different repository.");
        }

        if (!artifact.ArtifactId.Contains(artifact.SourceSessionId.ToString(), StringComparison.Ordinal))
        {
            throw new DecisionSessionValidationException("Decision session continuity artifact id does not include its source session id.");
        }

        if (string.IsNullOrWhiteSpace(artifact.ContinuityFingerprint))
        {
            throw new DecisionSessionValidationException("Decision session continuity artifact fingerprint is required.");
        }

        if (artifact.DecisionReferences.Count == 0 ||
            artifact.ReasoningReferences.Count == 0 ||
            artifact.OperationalContextReferences.Count == 0)
        {
            throw new DecisionSessionValidationException("Decision session continuity artifact must include decision, reasoning, and operational context references.");
        }
    }

    private static void ValidateContinuityArtifactId(string artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId) ||
            artifactId.Contains(Path.DirectorySeparatorChar) ||
            artifactId.Contains(Path.AltDirectorySeparatorChar) ||
            !artifactId.StartsWith("continuity.", StringComparison.Ordinal) ||
            !artifactId.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new ArgumentException("Continuity artifact id is invalid.", nameof(artifactId));
        }
    }

    private static void ValidateTransfer(Repository repository, DecisionSessionTransfer transfer)
    {
        ValidateTransferId(transfer.TransferId);
        if (transfer.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session transfer belongs to a different repository.");
        }

        foreach (DecisionSessionTransferEvent transferEvent in transfer.Events)
        {
            if (transferEvent.RepositoryId != repository.Id)
            {
                throw new DecisionSessionValidationException("Decision session transfer event belongs to a different repository.");
            }

            if (transferEvent.SourceSessionId != transfer.SourceSessionId)
            {
                throw new DecisionSessionValidationException("Decision session transfer event source does not match transfer source.");
            }
        }
    }

    private static void ValidateTransferId(string transferId)
    {
        if (string.IsNullOrWhiteSpace(transferId) ||
            transferId.Contains(Path.DirectorySeparatorChar) ||
            transferId.Contains(Path.AltDirectorySeparatorChar) ||
            !transferId.StartsWith("transfer.", StringComparison.Ordinal) ||
            !transferId.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new ArgumentException("Decision session transfer id is invalid.", nameof(transferId));
        }
    }

    private static void ValidateRecoveryResult(Repository repository, DecisionSessionRecoveryResult result)
    {
        ValidateRecoveryId(result.RecoveryId);
        if (result.RepositoryId != repository.Id ||
            result.Diagnostics.RepositoryId != repository.Id ||
            result.Diagnostics.RegistryDiagnostics.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session recovery result belongs to a different repository.");
        }

        foreach (DecisionSessionRecoveryEvent recoveryEvent in result.Events)
        {
            if (recoveryEvent.RepositoryId != repository.Id)
            {
                throw new DecisionSessionValidationException("Decision session recovery event belongs to a different repository.");
            }
        }
    }

    private static void ValidateRecoveryId(string recoveryId)
    {
        if (string.IsNullOrWhiteSpace(recoveryId) ||
            recoveryId.Contains(Path.DirectorySeparatorChar) ||
            recoveryId.Contains(Path.AltDirectorySeparatorChar) ||
            !recoveryId.StartsWith("recovery.", StringComparison.Ordinal) ||
            !recoveryId.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new ArgumentException("Decision session recovery id is invalid.", nameof(recoveryId));
        }
    }

    private static void ValidateCertificationReport(Repository repository, DecisionSessionCertificationReport report)
    {
        ValidateCertificationReportId(report.ReportId);
        if (report.RepositoryId != repository.Id ||
            report.Result.RepositoryId != repository.Id ||
            report.Governance.RepositoryId != repository.Id ||
            report.Health.RepositoryId != repository.Id)
        {
            throw new DecisionSessionValidationException("Decision session certification report belongs to a different repository.");
        }

        if (!string.Equals(report.ReportId, report.Result.Id, StringComparison.Ordinal))
        {
            throw new DecisionSessionValidationException("Decision session certification report id does not match its result id.");
        }
    }

    private static void ValidateCertificationReportId(string reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId) ||
            reportId.Contains(Path.DirectorySeparatorChar) ||
            reportId.Contains(Path.AltDirectorySeparatorChar) ||
            !reportId.StartsWith("certification.", StringComparison.Ordinal) ||
            !reportId.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new ArgumentException("Decision session certification report id is invalid.", nameof(reportId));
        }
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
