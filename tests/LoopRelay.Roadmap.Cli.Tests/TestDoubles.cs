using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapArtifacts = LoopRelay.Roadmap.Cli.RoadmapArtifacts;

namespace LoopRelay.Roadmap.Cli.Tests;

internal sealed class TempRepo : IDisposable
{
    public TempRepo()
    {
        Root = Path.Combine(Path.GetTempPath(), "cc-roadmap-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = Root,
        };
        Store = new FileSystemArtifactStore();
        Artifacts = new RoadmapArtifacts(Store, Repository);
    }

    public string Root { get; }
    public Repository Repository { get; }
    public FileSystemArtifactStore Store { get; }
    public RoadmapArtifacts Artifacts { get; }

    public void Write(string relativePath, string content)
    {
        string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public void SeedProjectContext()
    {
        int index = 1;
        foreach (string path in Cli.RoadmapArtifactPaths.ProjectContextSourceFiles)
        {
            Write(path, $"project context {index:00}");
            index++;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed class ScriptedAgentRuntime(params AgentTurnResult[] results) : IAgentRuntime
{
    private readonly Queue<AgentTurnResult> results = new(results);

    public int OneShotCalls { get; private set; }
    public List<string> Prompts { get; } = [];

    public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default) =>
        Task.FromResult<IAgentSession>(new ScriptedAgentSession(this));

    public Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        OneShotCalls++;
        Prompts.Add(prompt);
        AgentTurnResult result = results.Count == 0
            ? Completed(string.Empty)
            : results.Dequeue();
        return Task.FromResult(result);
    }

    public ValueTask CloseSessionAsync(IAgentSession session) => session.DisposeAsync();

    public static AgentTurnResult Completed(string output) => new(0, AgentTurnState.Completed, output, AgentTokenUsage.Zero);

    public static AgentTurnResult Failed(string diagnostics = "failed") => new(0, AgentTurnState.Failed, string.Empty, AgentTokenUsage.Zero, diagnostics);

    private sealed class ScriptedAgentSession(ScriptedAgentRuntime runtime) : IAgentSession
    {
        public SessionIdentity SessionId { get; } = SessionIdentity.New();
        public string RepositoryId => "repo";
        public SessionRole Role => SessionRole.Planning;
        public AgentSessionMode Mode => AgentSessionMode.OneShot;
        public AgentProcessState State => AgentProcessState.Exited;
        public int CompletedTurns => 0;
        public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;
        public string? ThreadId => null;

        public Task<AgentTurnResult> RunTurnAsync(string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default) =>
            runtime.RunOneShotAsync(Cli.AgentSpecs.ReadOnlyPlanning(new Repository { Id = Guid.NewGuid(), Path = Directory.GetCurrentDirectory() }), prompt, onChunk, cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class TestConsole : Cli.ILoopConsole
{
    public void Phase(string phase) { }
    public void Message(string content) { }
    public void Delta(string text) { }
    public void Tool(string summary) { }
    public void Info(string text) { }
    public void Warn(string text) { }
    public void Error(string text) { }
}

internal static class ExecutionPreparationTestSupport
{
    public static Cli.ExecutionPreparationProvenanceService CreateProvenance(TempRepo repo) =>
        new(repo.Artifacts, new Cli.ExecutionPreparationManifestStore(repo.Artifacts));

    public static async Task<Cli.ExecutionPreparationProvenanceService> SeedMilestoneSpecsAsync(
        TempRepo repo,
        params string[] specPaths)
    {
        Cli.ExecutionPreparationProvenanceService provenance = CreateProvenance(repo);
        await provenance.RecordMilestoneSpecsAsync(specPaths);
        return provenance;
    }

    public static async Task SeedOperationalContextAsync(
        Cli.ExecutionPreparationProvenanceService provenance,
        TempRepo repo,
        string content)
    {
        repo.Write(Cli.RoadmapArtifactPaths.OperationalContext, content);
        await provenance.RecordOperationalContextAsync(content);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.OperationalContext, Cli.ArtifactLifecycleState.Ready);
    }

    public static async Task SeedExecutionPromptAsync(
        Cli.ExecutionPreparationProvenanceService provenance,
        TempRepo repo,
        string content)
    {
        repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, content);
        await provenance.RecordExecutionPromptAsync(content);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.ExecutionPrompt, Cli.ArtifactLifecycleState.Ready);
    }
}

internal static class SelectionProvenanceTestSupport
{
    public static Cli.SelectionProvenanceService CreateProvenance(
        TempRepo repo,
        Cli.ExecutionPreparationProvenanceService? executionPreparation = null)
    {
        Cli.ExecutionPreparationProvenanceService provenance = executionPreparation ?? ExecutionPreparationTestSupport.CreateProvenance(repo);
        var contextBuilder = new Cli.RoadmapPromptContextBuilder(repo.Artifacts, provenance);
        var inputResolver = new Cli.TransitionInputResolver(repo.Artifacts, provenance);
        return new Cli.SelectionProvenanceService(
            repo.Artifacts,
            new Cli.SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
    }

    public static async Task SeedCurrentSelectionAsync(
        TempRepo repo,
        string selection,
        IReadOnlyList<Cli.RetiredEpic>? retiredEpics = null)
    {
        IReadOnlyList<Cli.RetiredEpic> effectiveRetiredEpics = retiredEpics ?? [];
        string projectionPath = Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        if (!await repo.Artifacts.ExistsAsync(projectionPath))
        {
            repo.Write(projectionPath, ProjectionSamples.Valid("SelectNextEpic"));
        }

        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
        var projections = new Cli.ProjectionRegistry();
        Cli.ProjectionProvenance projectionProvenance = new Cli.ProjectionProvenanceFactory(projections)
            .Create("SelectNextEpic", projectContext);
        await new Cli.ProjectionManifestStore(repo.Artifacts).UpsertAsync(Cli.ProjectionManifestEntry.FromTrustedProvenance(
            projectionProvenance,
            Cli.RoadmapHash.Sha256(repo.Read(projectionPath)),
            DateTimeOffset.UtcNow,
            Cli.ProjectionValidationStatus.Valid,
            Cli.ProjectionFreshness.Fresh,
            null));

        repo.Write(Cli.RoadmapArtifactPaths.Selection, selection);
        var provenance = CreateProvenance(repo);
        Cli.TransitionInputSnapshot cycle = await provenance.CaptureCurrentCycleAsync(
            repo.Read(projectionPath),
            effectiveRetiredEpics);
        await provenance.RecordActiveSelectionAsync(selection, cycle, effectiveRetiredEpics);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.Selection, Cli.ArtifactLifecycleState.Ready);
    }
}

internal static class ProjectionSamples
{
    public static string Valid(string runtimePromptName)
    {
        string title = runtimePromptName switch
        {
            "CreateRoadmapCompletionContext" => "# Roadmap Completion Projection",
            "UpdateRoadmapCompletionContext" => "# Roadmap Completion Update Projection",
            "SelectNextEpic" => "# Select Next Epic Projection",
            "EpicPreparationAudit" => "# Epic Preparation Audit Projection",
            "RealignEpic" => "# Epic Realignment Projection",
            "ReimagineEpic" => "# Epic Reimagination Projection",
            "CreateNewEpic" => "# Create New Epic Projection",
            "SplitEpic" => "# Split Epic Projection",
            "GenerateMilestoneDeepDivesForEpic" => "# Milestone Deep Dive Projection",
            "EvaluateEpicCompletionAndDrift" => "# Epic Completion Evaluation Projection",
            _ => throw new ArgumentOutOfRangeException(nameof(runtimePromptName)),
        };

        return $"""
        {title}

        ## Purpose

        Test projection.

        ## Authority Boundary

        Test boundary.

        ## Projection Metadata

        | Field | Value |
        |---|---|
        | Intended Consumer | {runtimePromptName} |

        ## Canonical Vocabulary

        | Term | Definition |
        |---|---|
        | Test | Test |

        ## Downstream Use Instructions

        Use the projection.

        ## Projection Integrity Checklist

        - The projection is structurally valid.
        """;
    }
}

internal static class RoadmapSamples
{
    public static string ValidEpic(
        string name = "Test Epic",
        string epicId = "EPIC-TEST",
        string status = "Authored",
        string sourceDisposition = "Create Epic")
    {
        return $"""
        # Epic: {name}

        ## Epic Metadata

        | Field | Value |
        |---|---|
        | Epic ID | {epicId} |
        | Status | {status} |
        | Source Disposition | {sourceDisposition} |
        | Projection Link | Test Projection |

        ## Strategic Purpose

        Deliver a bounded strategic capability for roadmap testing.

        ## Desired Capability

        The roadmap runtime can safely promote this epic as an authoritative artifact.

        ## Scope

        - Preserve artifact promotion boundaries.
        - Support milestone expansion.

        ## Non-Goals

        - Do not implement unrelated roadmap functionality.

        ## Acceptance Criteria

        - The epic has required metadata.
        - The epic has enough structure for milestone generation.

        ## Milestone Roadmap

        | Milestone ID | Milestone Name | Purpose | Outcome | Depends On | Completion Signal |
        |---|---|---|---|---|---|
        | M1 | Promotion Boundary | Verify promotion | Active epic is valid | None | Validation passes |
        """;
    }
}
