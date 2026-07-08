using LoopRelay.Core.Prompts.Projections;
using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ProjectionProvenanceTests
{
    [Fact]
    public async Task Factory_uses_generated_prompt_source_hash_separate_from_prompt_identity()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        Cli.ProjectionProvenance provenance = new Cli.ProjectionProvenanceFactory(new Cli.ProjectionRegistry())
            .Create("SelectNextEpic", context);

        Assert.Equal("ProjectionForSelectNextEpic", provenance.Prompt.PromptName);
        Assert.Equal(ProjectionForSelectNextEpic.SourceHash, provenance.Prompt.SourceHash);
        Assert.NotEqual(Cli.RoadmapHash.Sha256(provenance.Prompt.PromptName), provenance.Prompt.SourceHash);
        Assert.Contains(provenance.CausalInputs, input =>
            input.Kind == Cli.ProjectionProvenance.ProjectionPromptTemplateInputKind &&
            input.Identity == provenance.Prompt.PromptName &&
            input.Version == ProjectionForSelectNextEpic.SourceHash);
    }

    [Fact]
    public async Task Freshness_is_fresh_when_context_and_prompt_provenance_match()
    {
        Cli.ProjectionProvenance current = await CurrentProvenanceAsync();
        Cli.ProjectionManifestEntry entry = TrustedEntry(current);

        Cli.ProjectionFreshness freshness = Cli.ProjectionFreshnessEvaluator.Evaluate(current, entry);

        Assert.Equal(Cli.ProjectionStaleStatus.Fresh, freshness.Status);
        Assert.Empty(freshness.Reasons);
    }

    [Fact]
    public async Task Freshness_reports_project_context_drift()
    {
        Cli.ProjectionProvenance current = await CurrentProvenanceAsync();
        Cli.ProjectionManifestEntry entry = TrustedEntry(WithProjectContextHash(current, "old-context-hash"));

        Cli.ProjectionFreshness freshness = Cli.ProjectionFreshnessEvaluator.Evaluate(current, entry);

        Assert.Equal(Cli.ProjectionStaleStatus.Stale, freshness.Status);
        Assert.Contains(Cli.ProjectionStaleReason.ProjectContextDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Freshness_reports_prompt_template_drift()
    {
        Cli.ProjectionProvenance current = await CurrentProvenanceAsync();
        Cli.ProjectionManifestEntry entry = TrustedEntry(WithPromptSourceHash(current, "old-prompt-source-hash"));

        Cli.ProjectionFreshness freshness = Cli.ProjectionFreshnessEvaluator.Evaluate(current, entry);

        Assert.Equal(Cli.ProjectionStaleStatus.Stale, freshness.Status);
        Assert.Contains(Cli.ProjectionStaleReason.PromptTemplateDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Freshness_reports_context_and_prompt_template_drift()
    {
        Cli.ProjectionProvenance current = await CurrentProvenanceAsync();
        Cli.ProjectionProvenance previous = WithPromptSourceHash(
            WithProjectContextHash(current, "old-context-hash"),
            "old-prompt-source-hash");

        Cli.ProjectionFreshness freshness = Cli.ProjectionFreshnessEvaluator.Evaluate(current, TrustedEntry(previous));

        Assert.Contains(Cli.ProjectionStaleReason.ProjectContextDrift, freshness.Reasons);
        Assert.Contains(Cli.ProjectionStaleReason.PromptTemplateDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Prompt_name_drift_is_not_reported_as_prompt_template_drift_when_source_hash_matches()
    {
        Cli.ProjectionProvenance current = await CurrentProvenanceAsync();
        Cli.ProjectionProvenance previous = WithPromptName(current, "ProjectionForRenamedSelectNextEpic");

        Cli.ProjectionFreshness freshness = Cli.ProjectionFreshnessEvaluator.Evaluate(current, TrustedEntry(previous));

        Assert.Contains(Cli.ProjectionStaleReason.PromptIdentityDrift, freshness.Reasons);
        Assert.DoesNotContain(Cli.ProjectionStaleReason.PromptTemplateDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Missing_manifest_is_unknown_provenance()
    {
        Cli.ProjectionProvenance current = await CurrentProvenanceAsync();

        Cli.ProjectionFreshness freshness = Cli.ProjectionFreshnessEvaluator.Evaluate(current, null);

        Assert.Equal(Cli.ProjectionStaleStatus.UnknownProvenance, freshness.Status);
        Assert.Contains(Cli.ProjectionStaleReason.MissingManifest, freshness.Reasons);
    }

    [Fact]
    public async Task Explicit_unknown_manifest_entry_is_unknown_provenance()
    {
        Cli.ProjectionProvenance current = await CurrentProvenanceAsync();
        Cli.ProjectionManifestEntry entry = TrustedEntry(current) with
        {
            ProvenanceStatus = Cli.ProjectionProvenanceStatus.Unknown,
        };

        Cli.ProjectionFreshness freshness = Cli.ProjectionFreshnessEvaluator.Evaluate(current, entry);

        Assert.Equal(Cli.ProjectionStaleStatus.UnknownProvenance, freshness.Status);
        Assert.Contains(Cli.ProjectionStaleReason.UnknownProvenance, freshness.Reasons);
    }

    private static async Task<Cli.ProjectionProvenance> CurrentProvenanceAsync()
    {
        using var repo = new TempRepo();
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        return new Cli.ProjectionProvenanceFactory(new Cli.ProjectionRegistry()).Create("SelectNextEpic", context);
    }

    private static async Task<Cli.ProjectContext> SeedProjectAsync(TempRepo repo)
    {
        repo.SeedProjectContext();
        return await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
    }

    private static Cli.ProjectionManifestEntry TrustedEntry(Cli.ProjectionProvenance provenance) =>
        Cli.ProjectionManifestEntry.FromTrustedProvenance(
            provenance,
            "projection-hash",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            Cli.ProjectionValidationStatus.Valid,
            Cli.ProjectionFreshness.Fresh,
            null);

    private static Cli.ProjectionProvenance WithProjectContextHash(Cli.ProjectionProvenance provenance, string contextHash) =>
        provenance with
        {
            ProjectContextHash = contextHash,
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == Cli.ProjectionProvenance.ProjectContextInputKind
                    ? input with { Version = contextHash }
                    : input).ToArray(),
        };

    private static Cli.ProjectionProvenance WithPromptSourceHash(Cli.ProjectionProvenance provenance, string sourceHash) =>
        provenance with
        {
            Prompt = provenance.Prompt with { SourceHash = sourceHash },
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == Cli.ProjectionProvenance.ProjectionPromptTemplateInputKind
                    ? input with { Version = sourceHash }
                    : input).ToArray(),
        };

    private static Cli.ProjectionProvenance WithPromptName(Cli.ProjectionProvenance provenance, string promptName) =>
        provenance with
        {
            Prompt = provenance.Prompt with { PromptName = promptName },
            CausalInputs = provenance.CausalInputs.Select(input =>
                input.Kind == Cli.ProjectionProvenance.ProjectionPromptTemplateInputKind
                    ? input with { Identity = promptName }
                    : input).ToArray(),
        };
}
