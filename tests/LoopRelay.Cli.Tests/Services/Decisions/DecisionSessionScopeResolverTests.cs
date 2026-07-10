using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Decisions;

public sealed class DecisionSessionScopeResolverTests
{
    [Fact]
    public void IdenticalCanonicalProductsYieldTheSameScope()
    {
        ProductRecord[] products = [Product(ProductIdentity.PreparedEpic, 'a'), Product(ProductIdentity.ExecutablePlan, 'b')];

        DecisionSessionScope first = DecisionSessionScopeResolver.Resolve("0123456789abcdef0123456789abcdef", products);
        DecisionSessionScope second = DecisionSessionScopeResolver.Resolve("0123456789abcdef0123456789abcdef", products.Reverse());

        Assert.Equal(first.ScopeId, second.ScopeId);
        Assert.Matches("^[a-f0-9]{64}$", first.ScopeId.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChangedEpicOrPlanIdentityYieldsANewScope(bool changeEpic)
    {
        ProductRecord[] original = [Product(ProductIdentity.PreparedEpic, 'a'), Product(ProductIdentity.ExecutablePlan, 'b')];
        ProductRecord[] changed =
        [
            Product(ProductIdentity.PreparedEpic, changeEpic ? 'c' : 'a'),
            Product(ProductIdentity.ExecutablePlan, changeEpic ? 'b' : 'c'),
        ];

        DecisionSessionScope first = DecisionSessionScopeResolver.Resolve("0123456789abcdef0123456789abcdef", original);
        DecisionSessionScope second = DecisionSessionScopeResolver.Resolve("0123456789abcdef0123456789abcdef", changed);

        Assert.NotEqual(first.ScopeId, second.ScopeId);
    }

    [Fact]
    public void ProcessLocalOrStaleCausalIdentityIsRejected()
    {
        ProductRecord invalid = Product(ProductIdentity.PreparedEpic, 'a') with
        {
            CausalIdentity = "local-verification:random",
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            DecisionSessionScopeResolver.Resolve(
                "0123456789abcdef0123456789abcdef",
                [invalid, Product(ProductIdentity.ExecutablePlan, 'b')]));

        Assert.Contains("canonical content/producer identity", exception.Message);
    }

    [Fact]
    public void MissingOrAmbiguousProductsBlockScopeResolution()
    {
        ProductRecord epic = Product(ProductIdentity.PreparedEpic, 'a');
        ProductRecord plan = Product(ProductIdentity.ExecutablePlan, 'b');

        Assert.Throws<InvalidOperationException>(() =>
            DecisionSessionScopeResolver.Resolve("0123456789abcdef0123456789abcdef", [epic]));
        Assert.Throws<InvalidOperationException>(() =>
            DecisionSessionScopeResolver.Resolve("0123456789abcdef0123456789abcdef", [epic, epic, plan]));
    }

    private static ProductRecord Product(ProductIdentity identity, char hashCharacter) =>
        new(
            identity,
            identity == ProductIdentity.PreparedEpic ? WorkflowIdentity.TraditionalRoadmap : WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity($"Produce{identity.Value}"),
            [WorkflowIdentity.Execute],
            "repository",
            "canonical",
            [$".agents/{identity.Value}.md"],
            new string(hashCharacter, 64),
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$".agents/{identity.Value}.md"]);
}
