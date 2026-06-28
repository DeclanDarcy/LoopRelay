using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

[Collection("ProcessEnvironment")]
public sealed class DecisionContextServiceTests
{
    [Fact]
    public async Task BuildContextAssemblesDeterministicItemsWithSourceAttribution()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\nImplement decision lifecycle.");
        await WriteAsync(repository, ".agents/milestones/m1-context-resolution.md", "# Milestone 1\n\nBuild deterministic context.");
        await WriteAsync(repository, ".agents/operational_context.md", "# Operational Context\n\n- Stable architecture.");
        await WriteAsync(repository, ".agents/handoffs/handoff.0001.md", "# Handoff 1\n\nPrevious state.");
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "# Handoff\n\nCurrent state.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveDecisionAsync(repository, CreateDecision(repository.Id));
        var service = CreateService(repository, store, decisionRepository);

        DecisionContext first = await service.BuildContextAsync(repository.Id);
        DecisionContext second = await service.BuildContextAsync(repository.Id);

        Assert.True(first.Validation.IsValid);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(
            first.Items.Select(item => (item.Kind, item.Id, item.Fingerprint)).ToArray(),
            second.Items.Select(item => (item.Kind, item.Id, item.Fingerprint)).ToArray());
        Assert.Contains(first.Items, item =>
            item.Kind == "Plan" &&
            item.Sources.Any(source => source.RelativePath == ".agents/plan.md" && source.Excerpt == "# Plan"));
        Assert.Contains(first.Items, item =>
            item.Kind == "Milestone" &&
            item.Sources.Any(source => source.RelativePath == ".agents/milestones/m1-context-resolution.md"));
        Assert.Contains(first.Items, item => item.Kind == "Decision" && item.Id == "DEC-0001");
        Assert.Contains(first.Items, item => item.Kind == "RecentHandoff" && item.Sources.Any(source => source.RelativePath == ".agents/handoffs/handoff.md"));
        Assert.Contains(first.Diagnostics.Sources, source =>
            source.Name == "CurrentDecisionMarkdown" &&
            source.Status == DecisionContextSourceStatus.Missing);
    }

    [Fact]
    public async Task BuildContextReportsMissingRequiredInputs()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var service = CreateService(repository, store, new FileSystemDecisionRepository(store));

        DecisionContext context = await service.BuildContextAsync(repository.Id);

        Assert.False(context.Validation.IsValid);
        Assert.Contains(context.Validation.Errors, error => error.Contains(".agents/plan.md", StringComparison.Ordinal));
        Assert.Contains(context.Validation.Errors, error => error.Contains(".agents/milestones", StringComparison.Ordinal));
        Assert.Contains(context.Diagnostics.Sources, source =>
            source.Name == "Plan" &&
            source.Required &&
            source.Status == DecisionContextSourceStatus.Missing);
    }

    [Fact]
    public async Task BuildContextAllowsOptionalSourcesToBeOmitted()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\nImplement decision lifecycle.");
        await WriteAsync(repository, ".agents/milestones/m1-context-resolution.md", "# Milestone 1\n\nBuild deterministic context.");
        var store = new FileSystemArtifactStore();
        var service = CreateService(repository, store, new FileSystemDecisionRepository(store));

        DecisionContext context = await service.BuildContextAsync(repository.Id);

        Assert.True(context.Validation.IsValid);
        Assert.Contains(context.Validation.Warnings, warning => warning.Contains(".agents/operational_context.md", StringComparison.Ordinal));
        Assert.Contains(context.Diagnostics.Sources, source =>
            source.Name == "RecentHandoffs" &&
            !source.Required &&
            source.Status == DecisionContextSourceStatus.Missing);
    }

    [Fact]
    public async Task CreateSnapshotPersistsAndReloadsAfterServiceRestart()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\nImplement decision lifecycle.");
        await WriteAsync(repository, ".agents/milestones/m1-context-resolution.md", "# Milestone 1\n\nBuild deterministic context.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var service = CreateService(repository, store, decisionRepository);

        DecisionContextSnapshot snapshot = await service.CreateSnapshotAsync(repository.Id);
        var restartedService = CreateService(repository, store, new FileSystemDecisionRepository(store));
        IReadOnlyList<DecisionContextSnapshot> snapshots = await restartedService.ListSnapshotsAsync(repository.Id);

        DecisionContextSnapshot reloaded = Assert.Single(snapshots);
        Assert.Equal(snapshot.SnapshotId, reloaded.SnapshotId);
        Assert.Equal(snapshot.Fingerprint, reloaded.Fingerprint);
        Assert.Equal(snapshot.Context.Fingerprint, reloaded.Context.Fingerprint);
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "contexts", $"{snapshot.SnapshotId}.json")));
    }

    [Fact]
    public async Task MarkdownDecisionFallbackIsLoadedOnlyWhenStructuredRecordsAreAbsent()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\nImplement decision lifecycle.");
        await WriteAsync(repository, ".agents/milestones/m1-context-resolution.md", "# Milestone 1\n\nBuild deterministic context.");
        await WriteAsync(repository, ".agents/decisions/decisions.md", "# Decisions\n\n- Legacy markdown-only decision.");
        var store = new FileSystemArtifactStore();
        var service = CreateService(repository, store, new FileSystemDecisionRepository(store));

        DecisionContext context = await service.BuildContextAsync(repository.Id);

        Assert.Contains(context.Items, item => item.Kind == "CurrentDecisionMarkdown");
        Assert.True(context.Validation.IsValid);
    }

    [Fact]
    public async Task ContextEndpointsReturnLiveContextAndPersistSnapshot()
    {
        Repository repository = new()
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = CreateGitRepositoryDirectory()
        };
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\nImplement decision lifecycle.");
        await WriteAsync(repository, ".agents/milestones/m1-context-resolution.md", "# Milestone 1\n\nBuild deterministic context.");

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();

        HttpResponseMessage liveResponse = await client.GetAsync($"{root}/api/repositories/{repository.Id}/decisions/context");
        HttpResponseMessage snapshotResponse = await client.PostAsync($"{root}/api/repositories/{repository.Id}/decisions/context", null);
        HttpResponseMessage listResponse = await client.GetAsync($"{root}/api/repositories/{repository.Id}/decisions/context/snapshots");

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        DecisionContext? liveContext = await liveResponse.Content.ReadFromJsonAsync<DecisionContext>(jsonOptions);
        DecisionContextSnapshot? snapshot = await snapshotResponse.Content.ReadFromJsonAsync<DecisionContextSnapshot>(jsonOptions);
        DecisionContextSnapshot[]? snapshots = await listResponse.Content.ReadFromJsonAsync<DecisionContextSnapshot[]>(jsonOptions);
        Assert.NotNull(liveContext);
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshots);
        Assert.Equal(liveContext.Fingerprint, snapshot.Fingerprint);
        Assert.Single(snapshots);
    }

    private static DecisionContextService CreateService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        return new DecisionContextService(new StubRepositoryService(repository), store, decisionRepository);
    }

    private static Decision CreateDecision(Guid repositoryId)
    {
        DateTimeOffset now = new(2026, 06, 22, 12, 00, 00, TimeSpan.Zero);
        return new Decision(
            new DecisionId("DEC-0001"),
            DecisionState.Open,
            DecisionClassification.Architectural,
            "Persist structured decision records",
            "Decision lifecycle needs repository-owned state.",
            new DecisionMetadata(repositoryId, now, now),
            null,
            [],
            [new DecisionEvidence("M0B requires structured artifacts.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            [new DecisionHistoryEntry(now, "Created", null, DecisionState.Open.ToString(), "Initial context test.", [])]);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path
        };
    }

    private static string CreateGitRepositoryDirectory()
    {
        string directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class StubRepositoryService(params Repository[] repositories) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync()
        {
            return Task.FromResult<IReadOnlyList<Repository>>(repositories);
        }

        public Task<Repository> RegisterAsync(string repositoryPath)
        {
            throw new NotSupportedException();
        }

        public Task RemoveAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }
    }
}
