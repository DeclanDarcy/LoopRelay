using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

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
        foreach (string path in RoadmapArtifactPaths.ProjectContextSourceFiles)
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
            runtime.RunOneShotAsync(AgentSpecs.ReadOnlyPlanning(new Repository { Id = Guid.NewGuid(), Path = Directory.GetCurrentDirectory() }), prompt, onChunk, cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class TestConsole : ILoopConsole
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
    public static ExecutionPreparationProvenanceService CreateProvenance(TempRepo repo) =>
        new(repo.Artifacts, new ExecutionPreparationManifestStore(repo.Artifacts));

    public static async Task<ExecutionPreparationProvenanceService> SeedMilestoneSpecsAsync(
        TempRepo repo,
        params string[] specPaths)
    {
        ExecutionPreparationProvenanceService provenance = CreateProvenance(repo);
        await provenance.RecordMilestoneSpecsAsync(specPaths);
        return provenance;
    }

    public static async Task SeedOperationalContextAsync(
        ExecutionPreparationProvenanceService provenance,
        TempRepo repo,
        string content)
    {
        repo.Write(RoadmapArtifactPaths.OperationalContext, content);
        await provenance.RecordOperationalContextAsync(content);
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.OperationalContext, ArtifactLifecycleState.Ready);
    }

    public static async Task SeedExecutionPromptAsync(
        ExecutionPreparationProvenanceService provenance,
        TempRepo repo,
        string content)
    {
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, content);
        await provenance.RecordExecutionPromptAsync(content);
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.ExecutionPrompt, ArtifactLifecycleState.Ready);
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
