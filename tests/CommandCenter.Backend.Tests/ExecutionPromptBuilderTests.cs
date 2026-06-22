using CommandCenter.Execution;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Services;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionPromptBuilderTests
{
    [Fact]
    public void PromptIncludesRequiredInputsAndInstructions()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext());

        Assert.Contains(@"C:\repos\Project", prompt.Text);
        Assert.Contains(".agents/milestones/m2.md", prompt.Text);
        Assert.Contains("Plan: .agents/plan.md", prompt.Text);
        Assert.Contains("plan content", prompt.Text);
        Assert.Contains("Milestone: .agents/milestones/m2.md", prompt.Text);
        Assert.Contains("milestone content", prompt.Text);
        Assert.Contains("Branch: main", prompt.Text);
        Assert.Contains("Work only inside the repository path shown above.", prompt.Text);
        Assert.Contains("Produce or update `.agents/handoffs/handoff.md`", prompt.Text);
        Assert.Contains("Do not commit changes.", prompt.Text);
        Assert.Contains("Do not push changes.", prompt.Text);
        Assert.Contains("Leave handoff acceptance, commit, and push control to Command Center.", prompt.Text);
    }

    [Fact]
    public void PromptIncludesOptionalArtifactsWhenPresent()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("OperationalContext", ".agents/operational_context.md", "context content"),
                Artifact("CurrentHandoff", ".agents/handoffs/handoff.md", "handoff content"),
                Artifact("CurrentDecisions", ".agents/decisions/decisions.md", "decisions content")
            ]));

        Assert.Contains("OperationalContext: .agents/operational_context.md", prompt.Text);
        Assert.Contains("context content", prompt.Text);
        Assert.Contains("CurrentHandoff: .agents/handoffs/handoff.md", prompt.Text);
        Assert.Contains("handoff content", prompt.Text);
        Assert.Contains("CurrentDecisions: .agents/decisions/decisions.md", prompt.Text);
        Assert.Contains("decisions content", prompt.Text);
        Assert.Contains(".agents/operational_context.md", prompt.Metadata.IncludedArtifactPaths);
        Assert.Contains(".agents/handoffs/handoff.md", prompt.Metadata.IncludedArtifactPaths);
        Assert.Contains(".agents/decisions/decisions.md", prompt.Metadata.IncludedArtifactPaths);
    }

    [Fact]
    public void PromptOrdersOperationalContextBetweenMilestoneAndHandoff()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("CurrentDecisions", ".agents/decisions/decisions.md", "decisions content"),
                Artifact("CurrentHandoff", ".agents/handoffs/handoff.md", "handoff content"),
                Artifact("OperationalContext", ".agents/operational_context.md", "context content")
            ]));

        Assert.Equal(
            [
                ".agents/plan.md",
                ".agents/milestones/m2.md",
                ".agents/operational_context.md",
                ".agents/handoffs/handoff.md",
                ".agents/decisions/decisions.md"
            ],
            prompt.Metadata.IncludedArtifactPaths);
        Assert.True(
            prompt.Text.IndexOf("Milestone: .agents/milestones/m2.md", StringComparison.Ordinal) <
            prompt.Text.IndexOf("OperationalContext: .agents/operational_context.md", StringComparison.Ordinal));
        Assert.True(
            prompt.Text.IndexOf("OperationalContext: .agents/operational_context.md", StringComparison.Ordinal) <
            prompt.Text.IndexOf("CurrentHandoff: .agents/handoffs/handoff.md", StringComparison.Ordinal));
    }

    [Fact]
    public void PromptReportsMissingOptionalArtifactsWithoutBlockingPromptCreation()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext());

        Assert.Contains("Missing optional artifacts:", prompt.Text);
        Assert.Contains(".agents/operational_context.md", prompt.Text);
        Assert.Contains(".agents/handoffs/handoff.md", prompt.Text);
        Assert.Contains(".agents/decisions/decisions.md", prompt.Text);
    }

    [Fact]
    public void PromptIncludesGovernedDecisionProjectionBeforeRawDecisionArtifact()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("CurrentDecisions", ".agents/decisions/decisions.md", "raw decisions content")
            ],
            decisionProjection: new ExecutionDecisionProjection(
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                DateTimeOffset.UtcNow,
                [
                    new ExecutionConstraint(
                        "ECON-0001",
                        "DEC-0001",
                        "Architecture",
                        "Use repository artifacts",
                        DecisionClassification.Architectural,
                        [])
                ],
                [],
                [],
                [])));

        Assert.Contains("## Governed Decision Projection", prompt.Text);
        Assert.Contains("- DEC-0001 (Architectural): Use repository artifacts", prompt.Text);
        Assert.True(
            prompt.Text.IndexOf("## Governed Decision Projection", StringComparison.Ordinal) <
            prompt.Text.IndexOf("CurrentDecisions: .agents/decisions/decisions.md", StringComparison.Ordinal));
    }

    [Fact]
    public void PromptIncludesDirtyRepositoryDiagnostics()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(dirtyState: new RepositoryDirtyState
        {
            IsClean = false,
            StagedPaths = ["z-staged.cs", "a-staged.cs"],
            ModifiedPaths = ["src/changed.cs"],
            DeletedPaths = ["src/deleted.cs"],
            RenamedPaths = ["src/old.cs -> src/new.cs"],
            UntrackedPaths = ["notes.md"]
        }));

        Assert.Contains("Working tree clean: no", prompt.Text);
        Assert.Contains("- a-staged.cs", prompt.Text);
        Assert.Contains("- z-staged.cs", prompt.Text);
        Assert.Contains("- src/changed.cs", prompt.Text);
        Assert.Contains("- src/deleted.cs", prompt.Text);
        Assert.Contains("- src/old.cs -> src/new.cs", prompt.Text);
        Assert.Contains("- notes.md", prompt.Text);
        Assert.True(prompt.Metadata.DirtyRepository);
    }

    [Fact]
    public void PromptOutputIsStableForEquivalentContext()
    {
        var builder = new ExecutionPromptBuilder();
        ExecutionPrompt first = builder.Build(CreateContext(dirtyState: new RepositoryDirtyState
        {
            IsClean = false,
            ModifiedPaths = ["b.cs", "a.cs"]
        }));
        ExecutionPrompt second = builder.Build(CreateContext(dirtyState: new RepositoryDirtyState
        {
            IsClean = false,
            ModifiedPaths = ["b.cs", "a.cs"]
        }));

        Assert.Equal(first.Text, second.Text);
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000001", first.Text);
        Assert.DoesNotContain("2026-06-19", first.Text);
    }

    private static ExecutionContext CreateContext(
        IReadOnlyList<ExecutionContextArtifact>? optionalArtifacts = null,
        RepositoryDirtyState? dirtyState = null,
        ExecutionDecisionProjection? decisionProjection = null)
    {
        var artifacts = new List<ExecutionContextArtifact>
        {
            Artifact("Plan", ".agents/plan.md", "plan content"),
            Artifact("Milestone", ".agents/milestones/m2.md", "milestone content")
        };
        if (optionalArtifacts is not null)
        {
            artifacts.AddRange(optionalArtifacts);
        }

        return new ExecutionContext
        {
            RepositoryId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            RepositoryName = "Project",
            RepositoryPath = @"C:\repos\Project",
            MilestonePath = ".agents/milestones/m2.md",
            GeneratedAt = new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            Artifacts = artifacts,
            RepositorySnapshot = new ExecutionRepositorySnapshot
            {
                Branch = "main",
                DirtyState = dirtyState ?? new RepositoryDirtyState(),
                CapturedAt = new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero)
            },
            DecisionProjection = decisionProjection,
            Diagnostics = new ExecutionContextDiagnostics
            {
                TotalBytes = 27,
                TotalCharacters = 27,
                WarningThresholdBytes = 128 * 1024,
                HardLimitBytes = 512 * 1024,
                MissingOptionalArtifacts = optionalArtifacts is null
                    ? [".agents/operational_context.md", ".agents/handoffs/handoff.md", ".agents/decisions/decisions.md"]
                    : []
            }
        };
    }

    private static ExecutionContextArtifact Artifact(string role, string relativePath, string content)
    {
        return new ExecutionContextArtifact
        {
            Role = role,
            RelativePath = relativePath,
            Name = Path.GetFileName(relativePath),
            Content = content,
            ByteCount = content.Length,
            CharacterCount = content.Length
        };
    }
}
