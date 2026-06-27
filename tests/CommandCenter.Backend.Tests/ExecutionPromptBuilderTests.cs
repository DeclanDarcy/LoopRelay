using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Execution;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Services;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionPromptBuilderTests
{
    [Fact]
    public void StartPromptRendersCatalogBodyWithComposedPlanContext()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext());

        // No prior handoff => the StartExecution catalog template is rendered.
        Assert.Contains("start executing the first milestone", prompt.Text);
        Assert.Contains("then write .agents/handoffs/handoff.md with:", prompt.Text);
        Assert.DoesNotContain("continue executing the current milestone", prompt.Text);

        // Plan + Milestone artifact contents compose the catalog {plan} hole.
        Assert.Contains("plan content", prompt.Text);
        Assert.Contains("milestone content", prompt.Text);

        // The obsolete literal chrome (headers, instruction block, inline diagnostics) is gone.
        Assert.DoesNotContain("## Repository", prompt.Text);
        Assert.DoesNotContain("## Context Artifacts", prompt.Text);
        Assert.DoesNotContain("## Governed Decision Projection", prompt.Text);
        Assert.DoesNotContain("Do not commit changes.", prompt.Text);
    }

    [Fact]
    public void StartPromptOmitsGovernedDecisionsBecauseStartHasNoDecisionsHole()
    {
        // Governance reaches the agent only through the {decisions} hole, which exists solely on
        // ContinueExecution. A first-milestone Start therefore carries no decisions, by design.
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(
            CreateContext(decisionProjection: ProjectionWithGovernance()));

        Assert.Contains("start executing the first milestone", prompt.Text);
        Assert.DoesNotContain("Constraints:", prompt.Text);
        Assert.DoesNotContain("Use repository artifacts", prompt.Text);
    }

    [Fact]
    public void ContinuePromptRendersGovernedProjectionIntoDecisionsHole()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("OperationalContext", ".agents/operational_context.md", "context content"),
                Artifact("CurrentHandoff", ".agents/handoffs/handoff.md", "handoff content")
            ],
            decisionProjection: ProjectionWithGovernance()));

        // A current handoff selects the ContinueExecution catalog template.
        Assert.Contains("continue executing the current milestone", prompt.Text);
        Assert.DoesNotContain("start executing the first milestone", prompt.Text);

        // Operational context folds into {plan}; handoff fills {handoff}; the structured projection
        // (NOT a raw decisions.md artifact) is rendered into {decisions}.
        Assert.Contains("context content", prompt.Text);
        Assert.Contains("handoff content", prompt.Text);
        Assert.Contains("Constraints:", prompt.Text);
        Assert.Contains("- DEC-0001 (RepositoryConvention, Architectural): Use repository artifacts", prompt.Text);
        Assert.Contains("Architecture Rules:", prompt.Text);
    }

    [Fact]
    public void MetadataOrdersIncludedArtifactPathsByRole()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("CurrentHandoff", ".agents/handoffs/handoff.md", "handoff content"),
                Artifact("OperationalContext", ".agents/operational_context.md", "context content")
            ]));

        Assert.Equal(
            [
                ".agents/plan.md",
                ".agents/milestones/m2.md",
                ".agents/operational_context.md",
                ".agents/handoffs/handoff.md"
            ],
            prompt.Metadata.IncludedArtifactPaths);
    }

    [Fact]
    public void MetadataFlagsDirtyRepository()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(dirtyState: new RepositoryDirtyState
        {
            IsClean = false,
            ModifiedPaths = ["src/changed.cs"]
        }));

        Assert.True(prompt.Metadata.DirtyRepository);
    }

    [Fact]
    public void PromptTextIsDeterministicAndCarriesNoTimestampOrIdentity()
    {
        var builder = new ExecutionPromptBuilder();
        ExecutionPrompt first = builder.Build(CreateContext());
        ExecutionPrompt second = builder.Build(CreateContext());

        Assert.Equal(first.Text, second.Text);
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000001", first.Text);
        Assert.DoesNotContain("2026-06-19", first.Text);
    }

    private static ExecutionDecisionProjection ProjectionWithGovernance()
    {
        return new ExecutionDecisionProjection(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            [
                new ExecutionConstraint(
                    "ECON-0001",
                    "DEC-0001",
                    "Architecture",
                    "Use repository artifacts",
                    DecisionClassification.Architectural,
                    ExecutionProjectionKind.RepositoryConvention,
                    [])
            ],
            [],
            [],
            [
                new ExecutionArchitectureRule(
                    "EARC-0001",
                    "DEC-0001",
                    "Architecture",
                    "Use repository artifacts",
                    DecisionClassification.Architectural,
                    ExecutionProjectionKind.RepositoryConvention,
                    [])
            ],
            [],
            [],
            new ExecutionDecisionContext([], [], [], [], [], []));
    }

    private static ExecutionContext CreateContext(
        IReadOnlyList<LoadedArtifact>? optionalArtifacts = null,
        RepositoryDirtyState? dirtyState = null,
        ExecutionDecisionProjection? decisionProjection = null)
    {
        var artifacts = new List<LoadedArtifact>
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
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Project",
            Path = @"C:\repos\Project",
            GeneratedAt = new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            Artifacts = artifacts,
            Snapshot = new RepositorySnapshot
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
                    ? [".agents/operational_context.md", ".agents/handoffs/handoff.md"]
                    : []
            }
        };
    }

    private static LoadedArtifact Artifact(string role, string relativePath, string content)
    {
        return new LoadedArtifact
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
