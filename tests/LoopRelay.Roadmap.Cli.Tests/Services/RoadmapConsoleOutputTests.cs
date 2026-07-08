using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapConsoleOutputTests
{
    [Fact]
    public async Task Roadmap_cli_stops_at_milestone_specs_without_execution_prep_steps()
    {
        using var repo = SeedRepo();
        var console = new TestConsole();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(NewEpicSelection()),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateNewEpic")),
            ScriptedAgentRuntime.Completed(RoadmapSamples.ValidEpic("Console Output Epic", "EPIC-CONSOLE")),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("GenerateMilestoneDeepDivesForEpic")),
            ScriptedAgentRuntime.Completed(MilestoneBundle()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(
            repo,
            runtime,
            console).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Contains("Generate milestone deep dives", console.Phases);
        Assert.DoesNotContain("Generate operational context", console.Phases);
        Assert.DoesNotContain("Generate execution prompt", console.Phases);
        Cli.RoadmapStateDocument state = (await new Cli.RoadmapStateStore(repo.Artifacts).LoadAsync())!;
        Assert.Equal(Cli.RoadmapState.MilestoneSpecsReady, state.CurrentState);
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.OperationalContext));
        Assert.False(await repo.Artifacts.ExistsAsync(Cli.RoadmapArtifactPaths.ExecutionPrompt));
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "existing completion context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        return repo;
    }

    private static string NewEpicSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Select New Intermediary Epic |
        | Recommended Initiative | Keep roadmap console output focused |
        | Initiative Type | New Intermediary Epic |
        | Confidence | High |
        | Primary Reason | Exercise execution readiness output. |
        """;

    private static string MilestoneBundle() => """
        # FILE: .agents/specs/console-output-test.md
        # Console Output Test Milestone

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        - [ ] Internal artifact generation does not print phase headings.
        """;

}
