using LoopRelay.Core.Prompts.Projections;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

public sealed class ProjectionProvenanceTests
{
    [Fact]
    public async Task Factory_uses_generated_prompt_source_hash_separate_from_prompt_identity()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        ProjectionProvenance provenance = new ProjectionProvenanceFactory(new ProjectionRegistry())
            .Create("SelectNextEpic", context);

        Assert.Equal("ProjectionForSelectNextEpic", provenance.Prompt.PromptName);
        Assert.Equal(ProjectionForSelectNextEpic.SourceHash, provenance.Prompt.SourceHash);
        Assert.NotEqual(RoadmapHash.Sha256(provenance.Prompt.PromptName), provenance.Prompt.SourceHash);
        Assert.Contains(provenance.CausalInputs, input =>
            input.Kind == ProjectionProvenance.ProjectionPromptTemplateInputKind &&
            input.Identity == provenance.Prompt.PromptName &&
            input.Version == ProjectionForSelectNextEpic.SourceHash);
    }

    [Fact]
    public async Task Freshness_is_fresh_when_context_and_prompt_provenance_match()
    {
        ProjectionProvenance current = await CurrentProvenanceAsync();
        ProjectionManifestEntry entry = TrustedEntry(current);

        ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(current, entry);

        Assert.Equal(ProjectionStaleStatus.Fresh, freshness.Status);
        Assert.Empty(freshness.Reasons);
    }

    [Fact]
    public async Task Freshness_reports_project_context_drift()
    {
        ProjectionProvenance current = await CurrentProvenanceAsync();
        ProjectionManifestEntry entry = TrustedEntry(WithProjectContextHash(current, "old-context-hash"));

        ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(current, entry);

        Assert.Equal(ProjectionStaleStatus.Stale, freshness.Status);
        Assert.Contains(ProjectionStaleReason.ProjectContextDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Freshness_reports_prompt_template_drift()
    {
        ProjectionProvenance current = await CurrentProvenanceAsync();
        ProjectionManifestEntry entry = TrustedEntry(WithPromptSourceHash(current, "old-prompt-source-hash"));

        ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(current, entry);

        Assert.Equal(ProjectionStaleStatus.Stale, freshness.Status);
        Assert.Contains(ProjectionStaleReason.PromptTemplateDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Freshness_reports_context_and_prompt_template_drift()
    {
        ProjectionProvenance current = await CurrentProvenanceAsync();
        ProjectionProvenance previous = WithPromptSourceHash(
            WithProjectContextHash(current, "old-context-hash"),
            "old-prompt-source-hash");

        ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(current, TrustedEntry(previous));

        Assert.Contains(ProjectionStaleReason.ProjectContextDrift, freshness.Reasons);
        Assert.Contains(ProjectionStaleReason.PromptTemplateDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Prompt_name_drift_is_not_reported_as_prompt_template_drift_when_source_hash_matches()
    {
        ProjectionProvenance current = await CurrentProvenanceAsync();
        ProjectionProvenance previous = WithPromptName(current, "ProjectionForRenamedSelectNextEpic");

        ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(current, TrustedEntry(previous));

        Assert.Contains(ProjectionStaleReason.PromptIdentityDrift, freshness.Reasons);
        Assert.DoesNotContain(ProjectionStaleReason.PromptTemplateDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Missing_manifest_is_unknown_provenance()
    {
        ProjectionProvenance current = await CurrentProvenanceAsync();

        ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(current, null);

        Assert.Equal(ProjectionStaleStatus.UnknownProvenance, freshness.Status);
        Assert.Contains(ProjectionStaleReason.MissingManifest, freshness.Reasons);
    }

    [Fact]
    public async Task Explicit_unknown_manifest_entry_is_unknown_provenance()
    {
        ProjectionProvenance current = await CurrentProvenanceAsync();
        ProjectionManifestEntry entry = TrustedEntry(current) with
        {
            ProvenanceStatus = ProjectionProvenanceStatus.Unknown,
        };

        ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(current, entry);

        Assert.Equal(ProjectionStaleStatus.UnknownProvenance, freshness.Status);
        Assert.Contains(ProjectionStaleReason.UnknownProvenance, freshness.Reasons);
    }

    private static async Task<ProjectionProvenance> CurrentProvenanceAsync()
    {
        using var repo = new TempRepo();
        ProjectContext context = await SeedProjectAsync(repo);
        return new ProjectionProvenanceFactory(new ProjectionRegistry()).Create("SelectNextEpic", context);
    }

    private static async Task<ProjectContext> SeedProjectAsync(TempRepo repo)
    {
        repo.SeedProjectContext();
        return await new Cli.Services.ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
    }

    private static ProjectionManifestEntry TrustedEntry(ProjectionProvenance provenance) =>
        ProjectionManifestEntry.FromTrustedProvenance(
            provenance,
            "projection-hash",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            ProjectionValidationStatus.Valid,
            ProjectionFreshness.Fresh,
            null);

    private static ProjectionProvenance WithProjectContextHash(ProjectionProvenance provenance, string contextHash) =>
        provenance with
        {
            ProjectContextHash = contextHash,
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == ProjectionProvenance.ProjectContextInputKind
                    ? input with { Version = contextHash }
                    : input).ToArray(),
        };

    private static ProjectionProvenance WithPromptSourceHash(ProjectionProvenance provenance, string sourceHash) =>
        provenance with
        {
            Prompt = provenance.Prompt with { SourceHash = sourceHash },
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == ProjectionProvenance.ProjectionPromptTemplateInputKind
                    ? input with { Version = sourceHash }
                    : input).ToArray(),
        };

    private static ProjectionProvenance WithPromptName(ProjectionProvenance provenance, string promptName) =>
        provenance with
        {
            Prompt = provenance.Prompt with { PromptName = promptName },
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == ProjectionProvenance.ProjectionPromptTemplateInputKind
                    ? input with { Identity = promptName }
                    : input).ToArray(),
        };
}
