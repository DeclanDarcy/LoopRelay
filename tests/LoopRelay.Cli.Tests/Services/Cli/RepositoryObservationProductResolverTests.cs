using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class RepositoryObservationProductResolverTests
{
    // The requirements deliberately mix products the fixture materializes with one it never
    // does, so parity is asserted across the resolved, freshness-sensitive, and missing verdicts
    // rather than over a trivially empty result.
    private static readonly ProductRequirement[] Requirements =
    [
        Requirement(ProductIdentity.ExecutablePlan, DependencyStrength.Required, requiresFreshness: true),
        Requirement(ProductIdentity.OperationalContext, DependencyStrength.Required, requiresFreshness: false),
        Requirement(ProductIdentity.ExecutionDetails, DependencyStrength.Optional, requiresFreshness: false),
        Requirement(ProductIdentity.ExecutionMilestoneSet, DependencyStrength.Required, requiresFreshness: true),
        Requirement(ProductIdentity.CertifiedCompletion, DependencyStrength.Required, requiresFreshness: false),
    ];

    [Fact]
    public async Task Ambient_and_fresh_resolution_return_the_same_products_and_usability_verdicts()
    {
        // PERF-03 parity: for one fixed workspace state, resolving from the observation the
        // kernel cycle already owns must answer exactly what a fresh observation taken at the
        // same instant answers. Only the age of the observation changes; the verdict rules and
        // the causal identities they produce do not.
        Repository repository = await SeedWorkspaceAsync();
        var observer = new RepositoryObserver();
        var resolver = new LoopRelayCompositionRoot.RepositoryObservationProductResolver(observer, repository);
        RepositoryObservation cycleObservation = await observer.ObserveAsync(
            repository.Path, CancellationToken.None);

        ProductResolutionResult ambient = await resolver.ResolveFromObservationAsync(
            cycleObservation, Requirements, CancellationToken.None);
        ProductResolutionResult fresh = await resolver.ResolveAsync(Requirements, CancellationToken.None);

        Assert.Equal(Describe(fresh), Describe(ambient));
        Assert.NotEmpty(ambient.Products);
        Assert.NotEmpty(ambient.Missing);
        Assert.False(ambient.IsUsable);
    }

    [Fact]
    public async Task Ambient_resolution_answers_from_the_handed_down_observation_not_the_current_tree()
    {
        // The ambient path must not sneak a fresh observation in: deleting the plan after the
        // observation was taken leaves the ambient answer unchanged while the fresh answer moves.
        Repository repository = await SeedWorkspaceAsync();
        var observer = new RepositoryObserver();
        var resolver = new LoopRelayCompositionRoot.RepositoryObservationProductResolver(observer, repository);
        RepositoryObservation cycleObservation = await observer.ObserveAsync(
            repository.Path, CancellationToken.None);
        File.Delete(Path.Combine(repository.Path, ".agents", "plan.md"));

        ProductResolutionResult ambient = await resolver.ResolveFromObservationAsync(
            cycleObservation, Requirements, CancellationToken.None);
        ProductResolutionResult fresh = await resolver.ResolveAsync(Requirements, CancellationToken.None);

        Assert.Contains(ambient.Products, product => product.Identity == ProductIdentity.ExecutablePlan);
        Assert.DoesNotContain(fresh.Products, product => product.Identity == ProductIdentity.ExecutablePlan);
        Assert.Contains(fresh.Missing, requirement => requirement.Product == ProductIdentity.ExecutablePlan);
    }

    private static string Describe(ProductResolutionResult result) => string.Join(
        "\n",
        [
            $"usable:{result.IsUsable}",
            $"products:{Products(result.Products)}",
            $"missing:{string.Join(",", result.Missing.Select(requirement => requirement.Product.Value).Order(StringComparer.Ordinal))}",
            $"stale:{Products(result.Stale)}",
            $"invalid:{Products(result.Invalid)}",
            $"ambiguous:{Products(result.Ambiguous)}",
        ]);

    private static string Products(IReadOnlyList<ProductRecord> records) => string.Join(
        ",",
        records
            .Select(record =>
                $"{record.Identity.Value}|{record.CausalIdentity}|{record.Lifecycle}|" +
                $"{record.ValidationState}|{record.Freshness}")
            .Order(StringComparer.Ordinal));

    private static ProductRequirement Requirement(
        ProductIdentity product,
        DependencyStrength strength,
        bool requiresFreshness) =>
        new(product, strength, requiresFreshness, "repository observation", $"parity:{product.Value}");

    private static async Task<Repository> SeedWorkspaceAsync()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-product-parity-").FullName;
        await WriteAsync(path, ".agents/plan.md", "# Plan");
        await WriteAsync(path, ".agents/operational_context.md", "# Operational Context");
        await WriteAsync(path, ".agents/details.md", "# Details");
        await WriteAsync(path, ".agents/milestones/m1.md", "# Milestone\n\n- [ ] Implement capability.");
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    private static async Task WriteAsync(string root, string relativePath, string content)
    {
        string full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content);
    }
}
