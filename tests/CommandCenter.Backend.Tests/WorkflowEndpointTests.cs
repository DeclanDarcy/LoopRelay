using System.Net;
using CommandCenter.Backend;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

[Collection("ProcessEnvironment")]
public sealed class WorkflowEndpointTests
{
    private static readonly WorkflowRoute[] Routes =
    [
        new("GET", "/api/repositories/{repositoryId:guid}/workflow"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/diagnostics"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/timeline"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/history"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/transitions"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/gates"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/gates/history"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/recovery"),
        new("POST", "/api/repositories/{repositoryId:guid}/workflow/recover"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/execution"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/handoff"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/decisions"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/operational-context"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/git"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/continuation/evaluation"),
        new("POST", "/api/repositories/{repositoryId:guid}/workflow/continuation/run"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/continuation/history"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/preparation/evaluation"),
        new("POST", "/api/repositories/{repositoryId:guid}/workflow/preparation/run"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/preparation/history"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/health"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/reports/repository"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/reports/progression"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/reports/human-governance"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/reports/readiness"),
        new("GET", "/api/repositories/{repositoryId:guid}/workflow/certification"),
        new("POST", "/api/repositories/{repositoryId:guid}/workflow/certification")
    ];

    [Fact]
    public async Task WorkflowRoutesExposeCompleteMilestoneSurface()
    {
        await using WebApplication app = Program.CreateApp([]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        RouteEndpoint[] workflowEndpoints = app.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("/workflow", StringComparison.Ordinal) == true)
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("/decision-sessions/workflow", StringComparison.Ordinal) != true)
            .OrderBy(endpoint => endpoint.RoutePattern.RawText, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Routes.Length, workflowEndpoints.Length);
        foreach (WorkflowRoute route in Routes)
        {
            RouteEndpoint endpoint = Assert.Single(
                workflowEndpoints,
                endpoint => endpoint.RoutePattern.RawText == route.Pattern &&
                    endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.SequenceEqual([route.Method]));
            Assert.Equal(route.Pattern, endpoint.RoutePattern.RawText);
        }
    }

    [Fact]
    public async Task WorkflowEndpointsReturnSuccessForRegisteredRepository()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = CreateApp(repository);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();

        foreach (WorkflowRoute route in Routes)
        {
            HttpResponseMessage response = route.Method == "POST"
                ? await client.PostAsync(root + ToConcretePath(route.Pattern, repository.Id), null)
                : await client.GetAsync(root + ToConcretePath(route.Pattern, repository.Id));

            Assert.True(
                response.IsSuccessStatusCode,
                $"{route.Method} {route.Pattern} returned {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    [Fact]
    public async Task WorkflowEndpointsPreserveMissingRepositoryAsNotFound()
    {
        Repository repository = CreateRepository();
        await using WebApplication app = CreateApp(repository);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();

        HttpResponseMessage response = await client.GetAsync(
            app.Urls.Single() + ToConcretePath(Routes[0].Pattern, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplication CreateApp(Repository repository)
    {
        return Program.CreateApp(
            [],
            services =>
            {
                services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository));
                services.AddSingleton<IGitService>(new FakeGitService());
            });
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        Directory.CreateDirectory(Path.Combine(path, ".agents"));
        File.WriteAllText(Path.Combine(path, ".agents", "plan.md"), "# Plan");
        File.WriteAllText(Path.Combine(path, ".agents", "handoff.md"), "# Handoff");
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path
        };
    }

    private static string ToConcretePath(string pattern, Guid repositoryId)
    {
        return pattern.Replace("{repositoryId:guid}", repositoryId.ToString(), StringComparison.Ordinal);
    }

    private sealed record WorkflowRoute(string Method, string Pattern);

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

    private sealed class FakeGitService : IGitService
    {
        public Task<RepositorySnapshot> GetSnapshotAsync(Repository repository)
        {
            return Task.FromResult(new RepositorySnapshot
            {
                Branch = "main",
                DirtyState = new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<RepositoryGitStatus> GetStatusAsync(Repository repository)
        {
            return Task.FromResult(new RepositoryGitStatus
            {
                Branch = "main",
                DirtyState = new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session)
        {
            return Task.FromResult(new CommitPreparation
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                RepositoryId = repository.Id,
                RepositoryPath = repository.Path,
                ProposedMessage = "Prepare workflow commit",
                ScopeItems = [],
                StatusSnapshot = new CommitStatusSnapshot
                {
                    Id = "commit-snapshot-1",
                    Branch = "main",
                    DirtyState = new RepositoryDirtyState(),
                    CapturedAt = DateTimeOffset.UtcNow
                },
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository)
        {
            return Task.FromResult(new CommitStatusSnapshot
            {
                Id = "commit-snapshot-1",
                Branch = "main",
                DirtyState = new RepositoryDirtyState(),
                CapturedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<CommitResult> CommitAsync(
            Repository repository,
            string message,
            IReadOnlyList<string> selectedPaths,
            string preparationSnapshotId)
        {
            return Task.FromResult(new CommitResult
            {
                CommitSha = "abc123",
                CommitMessage = message,
                PreparationSnapshotId = preparationSnapshotId,
                SelectedPaths = selectedPaths,
                CommittedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<PushResult> PushAsync(Repository repository, string? commitSha)
        {
            return Task.FromResult(new PushResult
            {
                PushAttemptedAt = DateTimeOffset.UtcNow,
                PushedAt = DateTimeOffset.UtcNow,
                PushedCommitSha = commitSha ?? "abc123",
                RemoteName = "origin",
                BranchName = "main"
            });
        }
    }
}
