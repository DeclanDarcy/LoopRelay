using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Execution;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Services;

namespace CommandCenter.Execution.Tests;

public sealed class ExecutionPromptBuilderTests
{
    [Fact]
    public void StartPromptRendersCatalogBodyWithExactPlanText()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext());

        // No prior handoff => the StartExecution catalog template is rendered.
        Assert.Contains("start executing the first milestone", prompt.Text);
        Assert.DoesNotContain("continue executing the current milestone", prompt.Text);

        // The exact .agents/plan.md text fills the catalog {plan} hole — nothing is composed into it.
        Assert.Contains("plan content", prompt.Text);

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
    public void ContinuePromptRendersRawDecisionsArtifactAndExcludesOperationalContext()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("OperationalContext", ".agents/operational_context.md", "context content"),
                Artifact("CurrentHandoff", ".agents/handoffs/handoff.md", "handoff content"),
                Artifact("Decisions", ".agents/decisions/decisions.md", "DEC-0001: keep the raw decisions text verbatim")
            ],
            decisionProjection: ProjectionWithGovernance()));

        // A current handoff selects the ContinueExecution catalog template.
        Assert.Contains("continue executing the current milestone", prompt.Text);
        Assert.DoesNotContain("start executing the first milestone", prompt.Text);

        // {decisions} carries the exact raw decisions.md text. The handoff is no longer a ContinueExecution
        // hole ({handoff} was removed); it reaches the agent via the decision session's system prompt instead.
        Assert.DoesNotContain("handoff content", prompt.Text);
        Assert.Contains("DEC-0001: keep the raw decisions text verbatim", prompt.Text);

        // Operational context belongs to the decision/codex session and must NOT be injected into the prompt.
        Assert.DoesNotContain("context content", prompt.Text);

        // The structured projection feeds launch-blocking gating only — it is never rendered into the prompt.
        Assert.DoesNotContain("Constraints:", prompt.Text);
        Assert.DoesNotContain("Architecture Rules:", prompt.Text);
        Assert.DoesNotContain("RepositoryConvention", prompt.Text);
    }

    [Fact]
    public void MetadataOrdersIncludedArtifactPathsByRole()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("CurrentHandoff", ".agents/handoffs/handoff.md", "handoff content"),
                Artifact("Decisions", ".agents/decisions/decisions.md", "decisions content"),
                Artifact("OperationalContext", ".agents/operational_context.md", "context content")
            ]));

        Assert.Equal(
            [
                ".agents/plan.md",
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

    [Fact]
    public void StartProvenanceRecordsCatalogPromptIdentityAndArtifacts()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext());

        PromptProvenance provenance = Assert.IsType<PromptProvenance>(prompt.Provenance);
        Assert.Equal(nameof(StartExecution), provenance.PromptName);
        Assert.Equal(typeof(StartExecution).FullName, provenance.PromptType);
        Assert.Equal(StartExecution.SourceHash, provenance.SourceHash);
        Assert.Equal(PromptSessionRole.OperationalExecution, provenance.SessionRole);
        Assert.Equal("Start", provenance.WorkflowPhase);
        Assert.Equal(
            [".agents/plan.md"],
            provenance.InputArtifactIdentities);
        // Both operational turns are directed to write the current handoff.
        Assert.Equal([".agents/handoffs/handoff.md"], provenance.OutputArtifactIdentities);
    }

    [Fact]
    public void ContinueProvenanceRecordsContinueCatalogPromptIdentity()
    {
        ExecutionPrompt prompt = new ExecutionPromptBuilder().Build(CreateContext(
            optionalArtifacts:
            [
                Artifact("OperationalContext", ".agents/operational_context.md", "context content"),
                Artifact("CurrentHandoff", ".agents/handoffs/handoff.md", "handoff content")
            ]));

        PromptProvenance provenance = Assert.IsType<PromptProvenance>(prompt.Provenance);
        Assert.Equal(nameof(ContinueExecution), provenance.PromptName);
        Assert.Equal(typeof(ContinueExecution).FullName, provenance.PromptType);
        Assert.Equal(ContinueExecution.SourceHash, provenance.SourceHash);
        Assert.Equal(PromptSessionRole.OperationalExecution, provenance.SessionRole);
        Assert.Equal("Continue", provenance.WorkflowPhase);
        Assert.Contains(".agents/handoffs/handoff.md", provenance.InputArtifactIdentities);
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
            Artifact("Plan", ".agents/plan.md", "plan content")
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
