namespace LoopRelay.Orchestration.Runtime;

/// <summary>
/// Re-reads declared inputs immediately before promotion and compares them with the frozen
/// attempt snapshot. Raw output and candidate products remain durable when this check fails.
/// </summary>
public sealed class SnapshotInputFreshnessValidator(
    IProductResolver _products,
    IPromptContextBuilder _contexts) : IInputFreshnessValidator
{
    public async Task<InputFreshnessResult> ValidateAsync(
        LoopRelay.Core.Models.Identity.CanonicalCausalContext causality,
        TransitionRuntimeRequest request,
        LoopRelay.Orchestration.Workflows.WorkflowTransitionDefinition definition,
        PromptContext frozenContext,
        CancellationToken cancellationToken)
    {
        ProductResolutionResult currentProducts =
            await _products.ResolveAsync(definition.RequiredInputProducts, cancellationToken);
        PromptContext current = await _contexts.BuildAsync(request, definition, currentProducts, cancellationToken);
        if (!string.Equals(current.InputSnapshot.Hash, frozenContext.InputSnapshot.Hash, StringComparison.Ordinal))
        {
            return new InputFreshnessResult(
                InputFreshnessStatus.InputInvalidated,
                "The causal input snapshot changed while the provider attempt was running; candidate promotion is denied.",
                [frozenContext.InputSnapshot.Hash, current.InputSnapshot.Hash]);
        }

        string[] frozenProducts = frozenContext.Inputs.Products
            .Select(product => $"{product.Identity.Value}:{product.CausalIdentity}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] observedProducts = currentProducts.Products
            .Select(product => $"{product.Identity.Value}:{product.CausalIdentity}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!frozenProducts.SequenceEqual(observedProducts, StringComparer.Ordinal))
        {
            return new InputFreshnessResult(
                InputFreshnessStatus.ConcurrentStateAdvanced,
                "Canonical product state advanced while the attempt was running; candidate promotion is denied.",
                frozenProducts.Concat(observedProducts).ToArray());
        }

        return new InputFreshnessResult(
            InputFreshnessStatus.Fresh,
            "Causal inputs still match the frozen attempt snapshot.",
            [frozenContext.InputSnapshot.Hash]);
    }
}
