using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Cli.Services.Decisions.Recovery;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Effects;
using LoopRelay.Cli.Services.Planning;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Cli.Services.Storage;
using LoopRelay.Cli.Services.Import;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Primitives;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Completion.Services.Prompts;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Interactions;
using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Primitives;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Storage;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationCompletion;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Services.RepositorySlices;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Services.Context;
using LoopRelay.Projections.Services.Definitions;
using LoopRelay.Projections.Services.Manifests;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Cli.Services.Cli;

internal sealed partial class LoopRelayCompositionRoot
{
    private sealed class UnifiedTransitionDefinitionResolver(
        IReadOnlyList<WorkflowDefinition> _definitions) : ITransitionDefinitionResolver
    {
        public Task<WorkflowTransitionDefinition> ResolveAsync(
            TransitionRuntimeRequest request,
            CancellationToken cancellationToken)
        {
            WorkflowDefinition definition = _definitions.Single(item => item.Identity == request.Workflow);
            return Task.FromResult(definition.Transitions.Single(item => item.Identity == request.Transition));
        }

        public Task<IReadOnlyList<WorkflowTransitionIdentity>> ResolveEligibleSuccessorsAsync(
            WorkflowTransitionDefinition definition,
            IReadOnlyList<ProductRecord> validatedProducts,
            CancellationToken cancellationToken) =>
            Task.FromResult(definition.EligibleSuccessors);
    }

    private sealed class RepositoryObservationProductResolver(
        RepositoryObserver _observer,
        Repository _repository) : IProductResolver
    {
        public async Task<ProductResolutionResult> ResolveAsync(
            IReadOnlyList<ProductRequirement> requirements,
            CancellationToken cancellationToken)
        {
            RepositoryObservation observation = await _observer.ObserveAsync(_repository.Path, cancellationToken);
            var products = new List<ProductRecord>();
            var missing = new List<ProductRequirement>();
            var stale = new List<ProductRecord>();
            var invalid = new List<ProductRecord>();
            var ambiguous = new List<ProductRecord>();

            foreach (ProductRequirement requirement in requirements)
            {
                ObservedProduct? observed = observation.Products.FirstOrDefault(item => item.Product.Identity == requirement.Product);
                if (observed is null)
                {
                    missing.Add(requirement);
                    continue;
                }

                products.Add(observed.Product);
                if (requirement.RequiresFreshness && observed.Product.Freshness == ProductFreshness.Stale)
                {
                    stale.Add(observed.Product);
                }

                if (!observed.GateUsable ||
                    observed.Product.ValidationState is ProductValidationState.Invalid or ProductValidationState.Stale)
                {
                    invalid.Add(observed.Product);
                }

                if (observed.Product.ValidationState == ProductValidationState.Ambiguous)
                {
                    ambiguous.Add(observed.Product);
                }
            }

            return new ProductResolutionResult(products, missing, stale, invalid, ambiguous);
        }
    }

    private sealed class UnifiedRepositoryObservationSource(
        RepositoryObserver _observer,
        Repository _repository) : ICanonicalRepositoryObservationSource
    {
        public Task<RepositoryObservation> ObserveAsync(CancellationToken cancellationToken = default) =>
            _observer.ObserveAsync(_repository.Path, cancellationToken);
    }

    // Every requirement is evaluated individually into its own GateRequirementResult; the gate status
    // is the worst-of aggregation Invalid > Unsatisfied > Ambiguous > Waiting > Satisfied. Requirement
    // kinds are implicit by shape: Product != null is a product requirement, InputSurface != null is a
    // clean-input requirement (scoped git-porcelain cleanliness), neither is an explainable declaration.
    internal sealed class UnifiedGateEvaluator(
        IProcessRunner _processRunner,
        Repository _repository,
        IInteractionBroker? _interactionBroker = null,
        string _resolvedPolicyIdentity = "test-policy") : IGateEvaluator
    {
        public Task<GateResult> EvaluateInputGateAsync(
            GateDefinition gate,
            ProductResolutionResult inputs,
            InputGateEvaluationContext context,
            CancellationToken cancellationToken) =>
            EvaluateInputGateCoreAsync(gate, inputs, context, cancellationToken);

        internal Task<GateResult> EvaluateInputGateAsync(
            GateDefinition gate,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken) =>
            EvaluateInputGateCoreAsync(gate, inputs, context: null, cancellationToken);

        private async Task<GateResult> EvaluateInputGateCoreAsync(
            GateDefinition gate,
            ProductResolutionResult inputs,
            InputGateEvaluationContext? context,
            CancellationToken cancellationToken)
        {
            var requirements = new List<GateRequirementResult>(gate.Requirements.Count);
            foreach (GateRequirementDefinition requirement in gate.Requirements)
            {
                requirements.Add(await EvaluateRequirementAsync(
                    requirement,
                    context,
                    product => EvaluateInputProduct(requirement, product, inputs),
                    cancellationToken));
            }
            if (context?.Request.Transition == new WorkflowTransitionIdentity("VerifyWorkflowExitGate"))
            {
                requirements.Add(await EvaluateCompletionAuthorityAsync(context, cancellationToken));
            }

            GateResult result = Aggregate(
                gate,
                requirements,
                "Input gate satisfied by repository-owned products.");
            return result.Status == GateStatus.Satisfied
                ? result
                : result with { Explanation = $"Input gate {Describe(result.Status)}: {result.Explanation}" };
        }

        private async Task<GateRequirementResult> EvaluateCompletionAuthorityAsync(
            InputGateEvaluationContext context,
            CancellationToken cancellationToken)
        {
            CanonicalCompletionSnapshot snapshot = await new CanonicalCompletionAuthorityStore(_repository)
                .ReadSnapshotAsync(cancellationToken);
            CompletionDecision? decision = snapshot.Decisions.LastOrDefault(item =>
                item.RootRun == context.Causality.Run && item.Kind == CompletionDecisionKind.CertifiedCandidate);
            CompletionCertificate? certificate = decision is null ? null : snapshot.Certificates
                .SingleOrDefault(item => item.Decision == decision.Identity);
            CompletionClosurePlan? plan = certificate is null ? null : snapshot.ClosurePlans
                .SingleOrDefault(item => item.Certificate == certificate.Identity);
            if (decision is null || certificate is null || plan is null)
            {
                return new GateRequirementResult(
                    "completion-authority-certified-candidate",
                    GateStatus.Unsatisfied,
                    "Completion Authority has no certified candidate, certificate, and closure plan for this root run.",
                    [$"completion-authority:no-certified-candidate:{context.Causality.Run.Value}"],
                    RuntimeOutcomeKind.MissingRequiredInput);
            }
            return new GateRequirementResult(
                "completion-authority-certified-candidate",
                GateStatus.Satisfied,
                "Completion Authority owns the certified candidate and immutable closure plan for this root run.",
                [decision.Identity.Value, certificate.Identity.Value, plan.Identity.Value]);
        }

        public async Task<GateResult> EvaluateOutputGateAsync(
            GateDefinition gate,
            ProductValidationResult validation,
            CancellationToken cancellationToken)
        {
            var requirements = new List<GateRequirementResult>(gate.Requirements.Count);
            foreach (GateRequirementDefinition requirement in gate.Requirements)
            {
                requirements.Add(await EvaluateRequirementAsync(
                    requirement,
                    context: null,
                    product => EvaluateOutputProduct(requirement, product, validation),
                    cancellationToken));
            }

            return Aggregate(gate, requirements, validation.Explanation);
        }

        // Worst-of ordering: Invalid > Unsatisfied > Ambiguous > Waiting > Satisfied.
        internal static GateStatus WorstOf(IEnumerable<GateStatus> statuses)
        {
            GateStatus worst = GateStatus.Satisfied;
            foreach (GateStatus status in statuses)
            {
                if (Severity(status) > Severity(worst))
                {
                    worst = status;
                }
            }

            return worst;
        }

        // Repo-relative porcelain paths are in scope when they equal the surface or fall under it as a
        // path prefix; a collapsed entry for the surface directory itself (".agents" gitlink or
        // "?? .agents/" untracked directory) also counts.
        internal static bool IsWithinSurface(string path, string surface)
        {
            string normalizedSurface = Normalize(surface);
            if (normalizedSurface.Length == 0)
            {
                return true;
            }

            string normalizedPath = Normalize(path);
            return normalizedPath == normalizedSurface ||
                normalizedPath.StartsWith(normalizedSurface + "/", StringComparison.Ordinal);
        }

        private async Task<GateRequirementResult> EvaluateRequirementAsync(
            GateRequirementDefinition requirement,
            InputGateEvaluationContext? context,
            Func<ProductIdentity, GateRequirementResult> productEvaluation,
            CancellationToken cancellationToken)
        {
            if (requirement.Product is { } product)
            {
                return productEvaluation(product);
            }

            if (requirement.InputSurface is { } surface)
            {
                return await EvaluateCleanInputAsync(requirement, surface, context, cancellationToken);
            }

            return new GateRequirementResult(
                requirement.Identity,
                GateStatus.Satisfied,
                "Requirement declares no product or input surface; it is satisfied as an explainable declaration.",
                [requirement.Description]);
        }

        private static GateRequirementResult EvaluateInputProduct(
            GateRequirementDefinition requirement,
            ProductIdentity product,
            ProductResolutionResult inputs)
        {
            // Product failures declare the rank-0 cannot-proceed outcome so a missing product
            // always outranks a surface problem in the runtime's worst-of selection.
            if (inputs.Missing.Any(missing => missing.Product == product) ||
                inputs.Products.All(resolved => resolved.Identity != product))
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Required input product '{product}' is missing; produce it before rerunning.",
                    [product.Value],
                    UnsatisfiedOutcome: RuntimeOutcomeKind.MissingRequiredInput);
            }

            if (inputs.Invalid.FirstOrDefault(invalid => invalid.Identity == product) is { } invalidRecord)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Required input product '{product}' is invalid or unusable; repair it before rerunning.",
                    ProductEvidence(product, invalidRecord),
                    UnsatisfiedOutcome: RuntimeOutcomeKind.MissingRequiredInput);
            }

            if (inputs.Stale.FirstOrDefault(stale => stale.Identity == product) is { } staleRecord)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Required input product '{product}' is stale; refresh it before rerunning.",
                    ProductEvidence(product, staleRecord),
                    UnsatisfiedOutcome: RuntimeOutcomeKind.MissingRequiredInput);
            }

            if (inputs.Ambiguous.FirstOrDefault(ambiguous => ambiguous.Identity == product) is { } ambiguousRecord)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Ambiguous,
                    $"Required input product '{product}' has ambiguous validation state.",
                    ProductEvidence(product, ambiguousRecord));
            }

            ProductRecord satisfied = inputs.Products.First(resolved => resolved.Identity == product);
            return new GateRequirementResult(
                requirement.Identity,
                GateStatus.Satisfied,
                $"Required input product '{product}' is resolved and usable.",
                satisfied.EvidenceLocations.Count > 0 ? satisfied.EvidenceLocations : [product.Value]);
        }

        private static GateRequirementResult EvaluateOutputProduct(
            GateRequirementDefinition requirement,
            ProductIdentity product,
            ProductValidationResult validation)
        {
            if (validation.MissingProducts.Contains(product) ||
                validation.InvalidProducts.Contains(product) ||
                validation.StaleProducts.Contains(product))
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Output product '{product}' failed validation: {validation.Explanation}",
                    [product.Value]);
            }

            if (validation.AmbiguousProducts.Contains(product))
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Ambiguous,
                    $"Output product '{product}' has ambiguous validation state: {validation.Explanation}",
                    [product.Value]);
            }

            ProductRecord? validated = validation.Products.FirstOrDefault(item => item.Identity == product);
            if (validated is not null)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Satisfied,
                    $"Output product '{product}' passed validation.",
                    validated.EvidenceLocations.Count > 0 ? validated.EvidenceLocations : [product.Value]);
            }

            return validation.Status == ProductValidationStatus.Valid
                ? new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Satisfied,
                    $"Output product '{product}' is accepted by a valid output validation.",
                    validation.Evidence)
                : new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Output product '{product}' was not produced by a valid output validation: {validation.Explanation}",
                    [product.Value]);
        }

        private async Task<GateRequirementResult> EvaluateCleanInputAsync(
            GateRequirementDefinition requirement,
            string surface,
            InputGateEvaluationContext? context,
            CancellationToken cancellationToken)
        {
            // Read-at-use resolves every consumed input to a commit (M3); a workspace without a
            // git working tree cannot honor that, so a declared input surface cannot proceed here.
            string gitMarker = Path.Combine(_repository.Path, ".git");
            if (!Directory.Exists(gitMarker) && !File.Exists(gitMarker))
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Input surface '{surface}' has no git working tree; consumed inputs cannot resolve to a commit. Initialize git and commit the surface before rerunning.",
                    [surface],
                    UnsatisfiedOutcome: RuntimeOutcomeKind.UnversionedInputSurface);
            }

            ProcessRunResult status = await _processRunner.RunAsync("git", ["status", "--porcelain"], _repository.Path);
            if (status.ExitCode != 0)
            {
                // Process stderr can span lines; the concern text feeds line-oriented warning output,
                // so it is collapsed to a single line before it is composed into the explanation.
                string standardError = string.Join(
                    " ",
                    status.StandardError.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Invalid,
                    $"Cleanliness of input surface '{surface}' could not be evaluated: git status failed: {standardError}",
                    [surface]);
            }

            IReadOnlyList<string> dirty = Git.GitPorcelain.ChangedPaths(status.StandardOutput)
                .Where(path => IsWithinSurface(path, surface))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (dirty.Count == 0)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Satisfied,
                    $"Input surface '{surface}' is clean in the git working tree.",
                    [surface]);
            }

            if (context is null || !context.Interactive)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Input surface '{surface}' has uncommitted changes; commit the listed files under '{surface}' before rerunning.",
                    dirty,
                    UnsatisfiedOutcome: RuntimeOutcomeKind.DirtyInputSurface);
            }

            string semanticKey = string.Join(":", "dirty-input-commit-offer",
                context.Causality.Workspace.Value,
                context.Request.Workflow.Value,
                context.Request.Stage.Value,
                context.Request.Transition.Value,
                requirement.Identity,
                Normalize(surface));
            InteractionCategoryPolicy policy = InteractionCategoryPolicyRegistry.Resolve(
                InteractionCategory.DirtyInputCommitOffer,
                _resolvedPolicyIdentity);
            var request = new InteractionRequest(
                InteractionRequestIdentity.New(),
                InteractionCategory.DirtyInputCommitOffer,
                new InteractionCausalSubject(
                    context.Causality,
                    "declared-input-surface",
                    Normalize(surface)),
                $"Commit the exact declared input surface '{surface}' before this transition runs?",
                JsonSerializer.Serialize(new
                {
                    surface = Normalize(surface),
                    changedPaths = dirty,
                    gitEvidence = status.StandardOutput,
                }),
                policy,
                dirty.Prepend($"git-status:{status.StandardOutput}").ToArray(),
                semanticKey,
                DateTimeOffset.UtcNow);
            IInteractionBroker interactionBroker = _interactionBroker
                ?? throw new InvalidOperationException("Interactive gate evaluation requires an interaction broker.");
            InteractionAggregate aggregate = await interactionBroker.CreateAsync(
                new CreateInteractionCommand(request), cancellationToken);
            if (aggregate.State == InteractionLifecycle.Persisted)
            {
                aggregate = await interactionBroker.PresentAsync(
                    aggregate.Request.Identity, aggregate.RowVersion, cancellationToken);
            }

            bool declined = aggregate.ResumeAuthorized &&
                aggregate.AcceptedResponse is { ResponseJson: var responseJson } &&
                JsonDocument.Parse(responseJson).RootElement.GetProperty("accept").GetBoolean() == false;
            if (declined || aggregate.State == InteractionLifecycle.Cancelled)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Input surface '{surface}' remains dirty because the durable commit offer was declined.",
                    dirty.Append($"interaction:{aggregate.Request.Identity.Value}").ToArray(),
                    UnsatisfiedOutcome: RuntimeOutcomeKind.DirtyInputSurface);
            }

            return new GateRequirementResult(
                requirement.Identity,
                GateStatus.Unsatisfied,
                $"Input surface '{surface}' awaits durable interaction '{aggregate.Request.Identity.Value}'.",
                dirty.Append($"interaction:{aggregate.Request.Identity.Value}").ToArray(),
                UnsatisfiedOutcome: RuntimeOutcomeKind.HumanDecisionRequired);
        }

        private static GateResult Aggregate(
            GateDefinition gate,
            IReadOnlyList<GateRequirementResult> requirements,
            string satisfiedExplanation)
        {
            if (requirements.Count == 0)
            {
                // No requirement, no decision: a gate with zero requirements is satisfied with an
                // explainable requirement result naming why.
                var explainable = new GateRequirementResult(
                    $"{gate.Identity}.Explainable",
                    GateStatus.Satisfied,
                    $"Gate '{gate.Identity}' declares no requirements; it is satisfied by definition.",
                    [gate.Purpose]);
                return new GateResult(
                    GateStatus.Satisfied,
                    [explainable],
                    explainable.Explanation,
                    explainable.Evidence);
            }

            GateStatus status = WorstOf(requirements.Select(requirement => requirement.Status));
            string explanation = status == GateStatus.Satisfied
                ? satisfiedExplanation
                : requirements.First(requirement => requirement.Status == status).Explanation;
            IReadOnlyList<string> evidence = requirements
                .SelectMany(requirement => requirement.Evidence)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return new GateResult(status, requirements, explanation, evidence);
        }

        private static IReadOnlyList<string> ProductEvidence(ProductIdentity product, ProductRecord record) =>
            new[] { product.Value }
                .Concat(record.EvidenceLocations)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

#pragma warning disable CS8524
        private static int Severity(GateStatus status) =>
            status switch
            {
                GateStatus.Satisfied => 0,
                GateStatus.Waiting => 1,
                GateStatus.Ambiguous => 2,
                GateStatus.Unsatisfied => 3,
                GateStatus.Invalid => 4,
            };

        private static string Describe(GateStatus status) =>
            status switch
            {
                GateStatus.Satisfied => "satisfied",
                GateStatus.Unsatisfied => "unsatisfied",
                GateStatus.Waiting => "waiting",
                GateStatus.Ambiguous => "ambiguous",
                GateStatus.Invalid => "invalid",
            };
#pragma warning restore CS8524

        private static string Normalize(string value)
        {
            string normalized = value.Replace('\\', '/').Trim().Trim('/');
            return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
        }
    }

    private sealed class UnifiedPromptContextBuilder(Repository _repository) : IPromptContextBuilder
    {
        public async Task<PromptContext> BuildAsync(
            TransitionRuntimeRequest request,
            WorkflowTransitionDefinition definition,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken)
        {
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string key, string value) in request.Metadata ?? new Dictionary<string, string>())
            {
                metadata[key] = value;
            }

            var sections = new List<PromptContextSection>();
            var consumedFiles = new List<ConsumedInputFile>();
            if (request.Workflow == WorkflowIdentity.EvalRoadmap &&
                request.Transition == EvalRoadmapMilestonePromptContext.Transition)
            {
                EvalRoadmapMilestonePromptContextResult result =
                    EvalRoadmapMilestonePromptContext.Build(_repository.Path);
                if (!result.IsUsable)
                {
                    throw new PromptContextUnavailableException(result.Explanation, result.Evidence, result.ConsumedFiles);
                }

                foreach ((string key, string value) in result.Metadata)
                {
                    metadata[key] = value;
                }

                sections.AddRange(result.Sections);
                consumedFiles.AddRange(result.ConsumedFiles);
            }
            else if (request.Workflow == WorkflowIdentity.EvalRoadmap)
            {
                foreach (ProductRecord product in inputs.Products)
                {
                    foreach (string candidate in product.StorageRepresentations)
                    {
                        string fullPath = ResolveRepositoryPath(_repository, candidate);
                        string? content = null;
                        if (File.Exists(fullPath))
                        {
                            content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                        }
                        else if (Directory.Exists(fullPath))
                        {
                            string[] files = Directory.GetFiles(fullPath, "*.md", SearchOption.TopDirectoryOnly)
                                .Order(StringComparer.Ordinal)
                                .ToArray();
                            if (files.Length > 0)
                            {
                                content = string.Join(
                                    Environment.NewLine + Environment.NewLine,
                                    await Task.WhenAll(files.Select(async file =>
                                        $"# FILE: {ArtifactPath.ToRepositoryRelativePath(_repository, file)}{Environment.NewLine}{Environment.NewLine}" +
                                        await File.ReadAllTextAsync(file, cancellationToken))));
                            }
                        }

                        if (content is null)
                        {
                            continue;
                        }

                        sections.Add(new PromptContextSection(
                            $"Input Product: {product.Identity.Value}",
                            content,
                            candidate,
                            product.EvidenceLocations));
                        break;
                    }
                }

                if (!sections.Any(section => section.Title.StartsWith("Input Product:", StringComparison.Ordinal)) &&
                    inputs.Products.Any(product =>
                    product.Identity == ProductIdentity.EvaluationIntent))
                {
                    string inputDirectory = ResolveRepositoryPath(
                        _repository,
                        EvaluationArtifactPaths.InputDirectory);
                    string[] files = Directory.Exists(inputDirectory)
                        ? Directory.GetFiles(inputDirectory, "*.md", SearchOption.TopDirectoryOnly)
                            .Order(StringComparer.Ordinal)
                            .ToArray()
                        : [];
                    if (files.Length > 0)
                    {
                        sections.Add(new PromptContextSection(
                            $"Input Product: {ProductIdentity.EvaluationIntent.Value}",
                            string.Join(
                                Environment.NewLine + Environment.NewLine,
                                await Task.WhenAll(files.Select(async file =>
                                    $"# FILE: {ArtifactPath.ToRepositoryRelativePath(_repository, file)}{Environment.NewLine}{Environment.NewLine}" +
                                    await File.ReadAllTextAsync(file, cancellationToken)))),
                            EvaluationArtifactPaths.InputDirectory,
                            files.Select(file => ArtifactPath.ToRepositoryRelativePath(_repository, file)).ToArray()));
                    }
                }

                var artifacts = new ProjectionArtifacts(
                    new RepositoryArtifactStore(new FileSystemArtifactStore(), _repository),
                    _repository);
                ProjectContext projectContext = await new ProjectContextLoader(artifacts)
                    .LoadAsync(cancellationToken);
                sections.Insert(0, new PromptContextSection(
                    "Project Context",
                    projectContext.Content,
                    "ProjectContext",
                    projectContext.SourceFiles));
            }
            else if (request.Workflow == WorkflowIdentity.Plan &&
                PlanPromptContext.Supports(definition.Identity))
            {
                PlanPromptContextResult result =
                    PlanPromptContext.Build(_repository.Path, definition, inputs);
                if (!result.IsUsable)
                {
                    throw new PromptContextUnavailableException(result.Explanation, result.Evidence, result.ConsumedFiles);
                }

                foreach ((string key, string value) in result.Metadata)
                {
                    metadata[key] = value;
                }

                sections.AddRange(result.Sections);
                consumedFiles.AddRange(result.ConsumedFiles);
            }
            else if (request.Workflow == WorkflowIdentity.Execute &&
                request.Transition == new WorkflowTransitionIdentity("ExecuteImplementationSlice"))
            {
                (string Title, string Path, bool Required)[] executionInputs =
                [
                    ("Executable Plan", OrchestrationArtifactPaths.Plan, true),
                    ("Execution Details", OrchestrationArtifactPaths.Details, false),
                    ("Decision Set", OrchestrationArtifactPaths.Decisions, true),
                    ("Repository README", "README.md", false),
                ];
                foreach ((string title, string relativePath, bool required) in executionInputs)
                {
                    string fullPath = ResolveRepositoryPath(_repository, relativePath);
                    if (!File.Exists(fullPath))
                    {
                        if (required)
                        {
                            throw new PromptContextUnavailableException(
                                $"ExecuteImplementationSlice requires `{relativePath}`.",
                                [$"execution-input:missing:{relativePath}"],
                                consumedFiles);
                        }

                        continue;
                    }

                    string content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                    ConsumedInputFile consumed = ConsumedInputFile.FromContent(relativePath, content);
                    consumedFiles.Add(consumed);
                    sections.Add(new PromptContextSection(
                        title,
                        content,
                        relativePath,
                        [$"consumed-input-sha256:{consumed.Sha256}"]));
                }

                string milestoneDirectory = ResolveRepositoryPath(
                    _repository,
                    OrchestrationArtifactPaths.MilestonesDirectory);
                string[] milestoneFiles = Directory.Exists(milestoneDirectory)
                    ? Directory.GetFiles(milestoneDirectory, "m*.md", SearchOption.TopDirectoryOnly)
                        .Order(StringComparer.Ordinal)
                        .ToArray()
                    : [];
                if (milestoneFiles.Length == 0)
                {
                    throw new PromptContextUnavailableException(
                        $"ExecuteImplementationSlice requires milestone files under `{OrchestrationArtifactPaths.MilestonesDirectory}/`.",
                        [$"execution-input:missing:{OrchestrationArtifactPaths.MilestonesDirectory}/m*.md"],
                        consumedFiles);
                }

                var milestoneContents = new List<string>(milestoneFiles.Length);
                foreach (string milestoneFile in milestoneFiles)
                {
                    string relativePath = ArtifactPath.ToRepositoryRelativePath(_repository, milestoneFile);
                    string content = await File.ReadAllTextAsync(milestoneFile, cancellationToken);
                    ConsumedInputFile consumed = ConsumedInputFile.FromContent(relativePath, content);
                    consumedFiles.Add(consumed);
                    milestoneContents.Add($"# FILE: {relativePath}{Environment.NewLine}{Environment.NewLine}{content}");
                }

                sections.Add(new PromptContextSection(
                    "Execution Milestones",
                    string.Join(Environment.NewLine + Environment.NewLine, milestoneContents),
                    OrchestrationArtifactPaths.MilestonesDirectory + "/",
                    consumedFiles
                        .Where(file => file.Path.StartsWith(
                            OrchestrationArtifactPaths.MilestonesDirectory + "/",
                            StringComparison.Ordinal))
                        .Select(file => $"consumed-input-sha256:{file.Sha256}")
                        .ToArray()));
            }
            else if (request.Workflow == WorkflowIdentity.TraditionalRoadmap)
            {
                var artifacts = new ProjectionArtifacts(
                    new RepositoryArtifactStore(new FileSystemArtifactStore(), _repository),
                    _repository);
                ProjectContext projectContext = await new ProjectContextLoader(artifacts)
                    .LoadAsync(cancellationToken);
                sections.Add(new PromptContextSection(
                    "Project Context",
                    projectContext.Content,
                    "ProjectContext",
                    projectContext.SourceFiles));

                foreach (ProductRecord product in inputs.Products)
                {
                    string? path = product.StorageRepresentations.FirstOrDefault(candidate =>
                        File.Exists(ResolveRepositoryPath(_repository, candidate)));
                    if (path is null)
                    {
                        continue;
                    }

                    string content = await File.ReadAllTextAsync(
                        ResolveRepositoryPath(_repository, path),
                        cancellationToken);
                    string title = definition.Identity.Value == "GenerateMilestoneDeepDivesForEpic" &&
                        product.Identity == ProductIdentity.PreparedEpic
                            ? "Active Epic"
                            : $"Input Product: {product.Identity.Value}";
                    sections.Add(new PromptContextSection(
                        title,
                        content,
                        path,
                        product.EvidenceLocations));
                }
            }

            return new PromptContext(
                definition,
                inputs,
                TransitionInputSnapshotHasher.Create(definition, inputs.Products, metadata, sections),
                metadata,
                sections,
                consumedFiles);
        }
    }

    // Persists each chain-boundary decision as an append-only history fact so chain progression
    // never exists only in memory or console output.
    internal sealed class CanonicalChainBoundaryEvidenceStore(
        CanonicalWorkflowPersistenceStore _persistence) : IChainBoundaryEvidenceStore
    {
        private static readonly JsonSerializerOptions BoundaryJsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        public Task AppendAsync(ChainBoundaryEvidenceCapture capture, CancellationToken cancellationToken) =>
            _persistence.AppendChainBoundaryEventAsync(
                new CanonicalChainBoundaryEventRecord(
                    CausalUlid.NewId("bnd"),
                    capture.Run.Value,
                    capture.ChainIdentity,
                    capture.Evaluation.SourceWorkflow,
                    capture.Evaluation.TargetWorkflow,
                    capture.Evaluation.ExitGate.Status,
                    capture.Evaluation.EntryGate?.Status,
                    capture.Evaluation.ProductTransfer?.Gate.Status,
                    capture.Evaluation.CanAdvance ? "Advanced" : "StoppedAtBoundary",
                    capture.Evaluation.Explanation,
                    capture.Evaluation.ExitGate.Evidence
                        .Concat(capture.Evaluation.EntryGate?.Evidence ?? [])
                        .Concat(capture.Evaluation.ProductTransfer?.Gate.Evidence ?? [])
                        .ToArray(),
                    JsonSerializer.Serialize(capture.Evaluation, BoundaryJsonOptions),
                    capture.RecordedAt),
                cancellationToken);
    }

    // Enriches a consumption capture with git provenance — the commit every read resolves to and
    // per-surface tree hashes at that commit — then appends the receipt. Enrichment failures
    // degrade to null fields; the receipt still records exactly what was read.
    internal sealed class CanonicalReadReceiptStore(
        CanonicalWorkflowPersistenceStore _persistence,
        IProcessRunner _processRunner,
        Repository _repository) : IReadReceiptStore
    {
        public async Task AppendAsync(ReadReceiptCapture capture, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> surfaces = capture.Definition.InputGate.Requirements
                .Where(requirement => requirement.InputSurface is not null)
                .Select(requirement => requirement.InputSurface!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string? commitHash = await GitScalarAsync("rev-parse", "HEAD");
            Dictionary<string, string?>? surfaceTreeHashes = null;
            if (surfaces.Count > 0)
            {
                surfaceTreeHashes = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (string surface in surfaces)
                {
                    surfaceTreeHashes[surface] = commitHash is null
                        ? null
                        : await GitScalarAsync("rev-parse", $"HEAD:{surface.TrimEnd('/')}");
                }
            }

            await _persistence.AppendReadReceiptAsync(
                new CanonicalReadReceiptRecord(
                    CausalUlid.NewId("rcpt"),
                    capture.Causality.Run.Value,
                    capture.Request.Workflow.Value,
                    capture.Definition.Identity.Value,
                    capture.Causality.Attempt.Value,
                    commitHash,
                    surfaces,
                    surfaceTreeHashes,
                    capture.ConsumedFiles.Select(file => new CanonicalReadReceiptFile(file.Path, file.Sha256)).ToArray(),
                    capture.ConsumedProducts.Select(product => new CanonicalReadReceiptProduct(
                        product.Identity.Value,
                        product.CausalIdentity,
                        product.ValidationState.ToString())).ToArray(),
                    capture.Validation,
                    capture.ConsumedAt,
                    capture.Causality.TransitionRun.Value),
                cancellationToken);
        }

        private async Task<string?> GitScalarAsync(params string[] arguments)
        {
            try
            {
                ProcessRunResult result = await _processRunner.RunAsync("git", arguments, _repository.Path);
                if (result.ExitCode != 0)
                {
                    return null;
                }

                string value = result.StandardOutput.Trim();
                return value.Length == 0 ? null : value;
            }
            catch
            {
                return null;
            }
        }
    }

}
