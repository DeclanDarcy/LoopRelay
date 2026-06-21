using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Continuity;

public sealed class ContinuityReportService(
    IRepositoryService repositoryService,
    IArtifactStore artifactStore,
    IContinuityDiagnosticsService diagnosticsService) : IContinuityReportService
{
    private const string ReportsRelativePath = ".agents/operational_context/reports";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<ContinuityReport> GenerateReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        ContinuityDiagnostics diagnostics = await diagnosticsService.GetDiagnosticsAsync(repositoryId);
        string reportId = $"continuity.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        string relativePath = ArtifactPath.CombineRelative(ReportsRelativePath, $"{reportId}.json");
        var report = new ContinuityReport
        {
            ReportId = reportId,
            RepositoryId = repository.Id,
            GeneratedAt = DateTimeOffset.UtcNow,
            RelativePath = relativePath,
            Diagnostics = diagnostics
        };

        await artifactStore.WriteAsync(
            ArtifactPath.ResolveRepositoryPath(repository, relativePath),
            JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public async Task<IReadOnlyList<ContinuityReport>> ListReportsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        string reportsPath = ArtifactPath.ResolveRepositoryPath(repository, ReportsRelativePath);
        var reports = new List<ContinuityReport>();
        foreach (string file in await artifactStore.ListAsync(reportsPath, "continuity.*.json"))
        {
            string? content = await artifactStore.ReadAsync(file);
            if (content is null)
            {
                continue;
            }

            ContinuityReport? report;
            try
            {
                report = JsonSerializer.Deserialize<ContinuityReport>(content, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (report is not null)
            {
                reports.Add(report);
            }
        }

        return reports
            .OrderByDescending(report => report.GeneratedAt)
            .ThenByDescending(report => report.ReportId, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync()).FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
