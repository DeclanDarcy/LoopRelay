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

        Assert.Equal(".agents/milestones/m2.md", prompt.Metadata.MilestonePath);
    }

    [Fact]
    public void ContinuePromptFillsHandoffAndDecisionHolesWhenHandoffPresent()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("OperationalContext", ".agents/operational_context.md", "context content"),
                Artifact("CurrentHandoff", ".agents/handoffs/handoff.md", "handoff content"),
                Artifact("CurrentDecisions", ".agents/decisions/decisions.md", "decisions content")
            ]));

        // A current handoff selects the ContinueExecution catalog template.
        Assert.Contains("continue executing the current milestone", prompt.Text);
        Assert.DoesNotContain("start executing the first milestone", prompt.Text);

        // Operational context folds into {plan}; handoff and decisions fill their own holes.
        Assert.Contains("context content", prompt.Text);
        Assert.Contains("handoff content", prompt.Text);
        Assert.Contains("decisions content", prompt.Text);

        // Every delivered artifact is still tracked in the structured manifest metadata.
        Assert.Contains(".agents/operational_context.md", prompt.Metadata.IncludedArtifactPaths);
        Assert.Contains(".agents/handoffs/handoff.md", prompt.Metadata.IncludedArtifactPaths);
        Assert.Contains(".agents/decisions/decisions.md", prompt.Metadata.IncludedArtifactPaths);
    }

    [Fact]
    public void MetadataOrdersIncludedArtifactPathsByRole()
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

    private static ExecutionContext CreateContext(
        IReadOnlyList<ExecutionContextArtifact>? optionalArtifacts = null,
        RepositoryDirtyState? dirtyState = null)
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
