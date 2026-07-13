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
        currentProducts = NormalizeExpectedArchiveConsumption(
            definition,
            frozenContext.Inputs,
            currentProducts);
        currentProducts = NormalizeOwnInPlaceCandidates(
            causality,
            definition,
            frozenContext.Inputs,
            currentProducts);
        PromptContext current = await _contexts.BuildAsync(request, definition, currentProducts, cancellationToken);
        string frozenHash = ComparableSnapshotHash(definition, frozenContext.Inputs, frozenContext);
        string currentHash = ComparableSnapshotHash(definition, currentProducts, current);
        if (!string.Equals(currentHash, frozenHash, StringComparison.Ordinal))
        {
            return new InputFreshnessResult(
                InputFreshnessStatus.InputInvalidated,
                "The causal input snapshot changed while the provider attempt was running; candidate promotion is denied.",
                [frozenHash, currentHash]);
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

    private static string ComparableSnapshotHash(
        LoopRelay.Orchestration.Workflows.WorkflowTransitionDefinition definition,
        ProductResolutionResult inputs,
        PromptContext context)
    {
        HashSet<LoopRelay.Orchestration.Workflows.ProductIdentity> inPlaceOutputs = definition.ProducedProducts
            .Select(product => product.Identity)
            .Where(identity => definition.RequiredInputProducts.Any(requirement => requirement.Product == identity))
            .ToHashSet();
        if (inPlaceOutputs.Count == 0)
        {
            return context.InputSnapshot.Hash;
        }

        string[] mutablePaths = inputs.Products
            .Where(product => inPlaceOutputs.Contains(product.Identity))
            .SelectMany(product => product.StorageRepresentations.Concat(product.EvidenceLocations))
            .Select(NormalizePath)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (mutablePaths.Length == 0)
        {
            return context.InputSnapshot.Hash;
        }

        PromptContextSection[] stableSections = context.Sections
            .Where(section => !mutablePaths.Any(path =>
                RepresentsSameSurface(path, NormalizePath(section.SourcePath))))
            .ToArray();
        string[] mutableMetadataTokens = mutablePaths.Select(MetadataToken).ToArray();
        Dictionary<string, string> stableMetadata = context.Metadata
            .Where(pair => !mutableMetadataTokens.Any(token =>
                token.Length > 0 && pair.Key.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return TransitionInputSnapshotHasher.Create(
            definition,
            inputs.Products,
            stableMetadata,
            stableSections).Hash;
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('.', '/').TrimEnd('/');

    private static bool RepresentsSameSurface(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal) ||
        left.StartsWith(right + "/", StringComparison.Ordinal) ||
        right.StartsWith(left + "/", StringComparison.Ordinal);

    private static string MetadataToken(string path) => string.Concat(
        NormalizePath(path).Select(character => char.IsLetterOrDigit(character)
            ? char.ToLowerInvariant(character)
            : '_')).Trim('_');

    private static ProductResolutionResult NormalizeOwnInPlaceCandidates(
        LoopRelay.Core.Models.Identity.CanonicalCausalContext causality,
        LoopRelay.Orchestration.Workflows.WorkflowTransitionDefinition definition,
        ProductResolutionResult frozen,
        ProductResolutionResult current)
    {
        HashSet<LoopRelay.Orchestration.Workflows.ProductIdentity> inPlaceOutputs = definition.ProducedProducts
            .Select(product => product.Identity)
            .Where(identity => definition.RequiredInputProducts.Any(requirement => requirement.Product == identity))
            .ToHashSet();
        if (inPlaceOutputs.Count == 0)
        {
            return current;
        }

        Dictionary<LoopRelay.Orchestration.Workflows.ProductIdentity, LoopRelay.Orchestration.Workflows.ProductRecord>
            frozenByIdentity = frozen.Products.ToDictionary(product => product.Identity);
        bool IsExpectedInPlaceReplacement(LoopRelay.Orchestration.Workflows.ProductRecord product) =>
            inPlaceOutputs.Contains(product.Identity) &&
            frozenByIdentity.ContainsKey(product.Identity) &&
            ((product.Lifecycle == LoopRelay.Orchestration.Workflows.ProductLifecycle.Proposed &&
              string.Equals(product.CausalIdentity, causality.Attempt.Value, StringComparison.Ordinal)) ||
             (product.Lifecycle == LoopRelay.Orchestration.Workflows.ProductLifecycle.Active &&
              string.Equals(product.Authority, "repository observation", StringComparison.Ordinal)));

        return current with
        {
            Products = current.Products
                .Select(product => IsExpectedInPlaceReplacement(product) ? frozenByIdentity[product.Identity] : product)
                .ToArray(),
            Stale = current.Stale.Where(product => !IsExpectedInPlaceReplacement(product)).ToArray(),
            Invalid = current.Invalid.Where(product => !IsExpectedInPlaceReplacement(product)).ToArray(),
            Ambiguous = current.Ambiguous.Where(product => !IsExpectedInPlaceReplacement(product)).ToArray(),
        };
    }

    /// <summary>
    /// Archive transitions are explicitly authorized to relocate their declared inputs while the
    /// provider operation is running. Preserve the frozen causal records only when the current
    /// resolver reports those inputs missing or unusable; a still-present but changed product is
    /// left untouched and will fail the normal snapshot comparison.
    /// </summary>
    private static ProductResolutionResult NormalizeExpectedArchiveConsumption(
        LoopRelay.Orchestration.Workflows.WorkflowTransitionDefinition definition,
        ProductResolutionResult frozen,
        ProductResolutionResult current)
    {
        if (!definition.Effects.Any(effect => effect.Category ==
            LoopRelay.Orchestration.Workflows.EffectCategory.Archive))
        {
            return current;
        }

        HashSet<LoopRelay.Orchestration.Workflows.ProductIdentity> unavailable = current.Missing
            .Select(requirement => requirement.Product)
            .Concat(current.Stale.Select(product => product.Identity))
            .Concat(current.Invalid.Select(product => product.Identity))
            .Concat(current.Ambiguous.Select(product => product.Identity))
            .ToHashSet();
        if (unavailable.Count == 0)
        {
            return current;
        }

        Dictionary<LoopRelay.Orchestration.Workflows.ProductIdentity, LoopRelay.Orchestration.Workflows.ProductRecord>
            frozenByIdentity = frozen.Products
                .Where(product => unavailable.Contains(product.Identity))
                .ToDictionary(product => product.Identity);
        if (frozenByIdentity.Count == 0)
        {
            return current;
        }

        HashSet<LoopRelay.Orchestration.Workflows.ProductIdentity> normalized =
            frozenByIdentity.Keys.ToHashSet();
        return current with
        {
            Products = current.Products
                .Where(product => !normalized.Contains(product.Identity))
                .Concat(frozenByIdentity.Values)
                .ToArray(),
            Missing = current.Missing
                .Where(requirement => !normalized.Contains(requirement.Product))
                .ToArray(),
            Stale = current.Stale.Where(product => !normalized.Contains(product.Identity)).ToArray(),
            Invalid = current.Invalid.Where(product => !normalized.Contains(product.Identity)).ToArray(),
            Ambiguous = current.Ambiguous.Where(product => !normalized.Contains(product.Identity)).ToArray(),
        };
    }
}
