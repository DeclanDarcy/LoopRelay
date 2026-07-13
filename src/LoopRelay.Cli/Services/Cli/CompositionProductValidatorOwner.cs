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
    private sealed class UnifiedProductValidator(Repository _repository) : IProductValidator
    {
        public async Task<ProductValidationResult> ValidateAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output,
            CancellationToken cancellationToken)
        {
            if (PlanWarmSessionTransitions.Supports(definition))
            {
                return ValidatePlanWarmSessionOutput(definition, output);
            }

            if (PlanProjectionTransitions.Supports(definition))
            {
                return await ValidateSingleArtifactOutputAsync(
                    definition,
                    output,
                    PlanPromptContext.AdversarialPlanReviewProjectionPath,
                    "Plan projection",
                    validateProjection: true);
            }

            if (EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out EvalPromptAsset evalAsset))
            {
                return await ValidateSingleArtifactOutputAsync(
                    definition,
                    output,
                    evalAsset.PrimaryOutputPath,
                    "Eval prompt",
                    validateProjection: false);
            }

            if (TraditionalRoadmapPromptTransitions.TryGetPrimaryOutput(definition, out string roadmapOutput))
            {
                return await ValidateTraditionalRoadmapOutputAsync(
                    definition,
                    output,
                    roadmapOutput,
                    "Traditional roadmap prompt");
            }

            if (MilestoneDeepDiveTransitions.Supports(definition))
            {
                return await ValidateMilestoneDeepDiveOutputAsync(definition, output);
            }

            if (PlanReadOnlyReviewTransitions.Supports(definition))
            {
                return await ValidateSingleArtifactOutputAsync(
                    definition,
                    output,
                    PlanReadOnlyReviewTransitions.ReviewOutputPath,
                    "Plan read-only review",
                    validateProjection: false);
            }

            if (PlanScopedArtifactTransitions.Supports(definition))
            {
                return await ValidatePlanScopedArtifactOutputAsync(definition, output);
            }

            if (ExecuteDecisionSessionTransitions.Supports(definition) ||
                ExecuteImplementationTransitions.Supports(definition) ||
                ExecuteRepositoryStateTransitions.Supports(definition) ||
                ExecuteReviewTransitions.Supports(definition))
            {
                return await ValidateExecuteOutputAsync(definition, output);
            }

            if (!LocalVerificationTransitions.Supports(definition) &&
                !LocalArtifactTransitions.Supports(definition))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    "Product validation is not wired because prompt execution did not run.",
                    []);
            }

            if (definition.Identity.Value == "VerifyExecuteEntryContract" &&
                !ExecutionMilestoneFileSet.Evaluate(
                    Directory.Exists(ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.MilestonesDirectory))
                        ? Directory.GetFiles(
                            ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.MilestonesDirectory),
                            OrchestrationArtifactPaths.MilestoneSearchPattern,
                            SearchOption.TopDirectoryOnly)
                        : []).IsValid)
            {
                ExecutionMilestoneFileSetResult fileSet = ExecutionMilestoneFileSet.Evaluate(
                    Directory.GetFiles(
                        ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.MilestonesDirectory),
                        OrchestrationArtifactPaths.MilestoneSearchPattern,
                        SearchOption.TopDirectoryOnly));
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    output.CandidateProducts,
                    [],
                    [ProductIdentity.ExecutionMilestoneSet],
                    [],
                    [],
                    fileSet.Explanation,
                    output.Evidence);
            }

            if (definition.Identity.Value == "VerifyExecuteEntryContract" &&
                ExplicitSingleMilestoneInvariantViolated(_repository.Path, out int milestoneCount))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    output.CandidateProducts,
                    [],
                    [ProductIdentity.ExecutionMilestoneSet],
                    [],
                    [],
                    $"The authoritative strategic structure requires exactly one execution milestone, but `.agents/milestones` contains {milestoneCount} Markdown files.",
                    output.Evidence);
            }

            HashSet<ProductIdentity> actual = output.CandidateProducts
                .Select(product => product.Identity)
                .ToHashSet();
            ProductIdentity[] missing = definition.ProducedProducts
                .Select(product => product.Identity)
                .Where(identity => !actual.Contains(identity))
                .ToArray();
            ProductValidationStatus status = missing.Length == 0
                ? ProductValidationStatus.Valid
                : ProductValidationStatus.Missing;
            return new ProductValidationResult(
                status,
                output.CandidateProducts,
                missing,
                [],
                [],
                [],
                missing.Length == 0
                    ? $"Local verification validated `{definition.Identity}` products."
                    : $"Local verification did not produce all declared products for `{definition.Identity}`.",
                output.Evidence);
        }

        private async Task<ProductValidationResult> ValidateMilestoneDeepDiveOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output)
        {
            string directory = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.SpecsDirectory);
            string[] matches = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.md")
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            IReadOnlyList<string> evidence = output.Evidence;
            if (matches.Length == 0)
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Missing,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    $"{OrchestrationArtifactPaths.SpecsDirectory}/*.md was not written.",
                    evidence);
            }

            var relativePaths = new List<string>();
            var builder = new StringBuilder();
            foreach (string match in matches)
            {
                string relativePath = ArtifactPath.ToRepositoryRelativePath(_repository, match);
                string content = await File.ReadAllTextAsync(match);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                        [],
                        [],
                        $"{relativePath} is empty.",
                        evidence.Concat([relativePath]).ToArray());
                }

                relativePaths.Add(relativePath);
                builder
                    .AppendLine($"--- {relativePath} ---")
                    .AppendLine(content);
            }

            IReadOnlyList<string> productEvidence = evidence
                .Concat(relativePaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    productEvidence,
                    Hash(builder.ToString()),
                    relativePaths))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"Milestone deep-dive transition `{definition.Identity}` produced valid milestone specifications.",
                productEvidence);
        }

        private ProductValidationResult ValidatePlanWarmSessionOutput(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output)
        {
            string planPath = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.Plan);
            IReadOnlyList<string> evidence = output.Evidence;
            if (!File.Exists(planPath))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Missing,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    $"{OrchestrationArtifactPaths.Plan} was not written.",
                    evidence);
            }

            string plan = File.ReadAllText(planPath);
            if (string.IsNullOrWhiteSpace(plan))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    $"{OrchestrationArtifactPaths.Plan} is empty.",
                    evidence);
            }

            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    evidence,
                    Hash(plan)))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"Plan warm-session transition `{definition.Identity}` produced a valid executable plan.",
                evidence);
        }

        private async Task<ProductValidationResult> ValidateSingleArtifactOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output,
            string relativePath,
            string title,
            bool validateProjection)
        {
            IReadOnlyList<string> evidence = output.Evidence.Count == 0
                ? [relativePath]
                : output.Evidence;
            string path = ResolveRepositoryPath(_repository, relativePath);
            if (!File.Exists(path))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Missing,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    $"{relativePath} was not written.",
                    evidence);
            }

            string content = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    $"{relativePath} is empty.",
                    evidence);
            }

            if (validateProjection)
            {
                ProjectionDefinitionRegistry registry = ProjectionDefinitionRegistry.CreateDefault();
                ProjectionValidationResult validation =
                    new ProjectionValidator(registry).Validate(ProjectionRuntimePromptNames.AdversarialPlanReview, content);
                if (!validation.IsValid)
                {
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                        [],
                        [],
                        validation.Error ?? "Projection validation failed.",
                        evidence);
                }
            }

            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    evidence,
                    Hash(content),
                    [relativePath]))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"{title} transition `{definition.Identity}` produced a valid artifact.",
                evidence);
        }

        private async Task<ProductValidationResult> ValidatePlanScopedArtifactOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output)
        {
            PlanScopedArtifactOperationSpec operation =
                PlanScopedArtifactOperationCatalog.Get(definition.Identity);
            List<string> evidence = [..PlanScopedArtifactTransitions.Evidence(definition)];
            var invalid = new List<ProductIdentity>();
            var missing = new List<ProductIdentity>();
            var artifactPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (string requiredOutput in operation.RequiredOutputs)
            {
                string path = ResolveRepositoryPath(_repository, requiredOutput);
                if (!File.Exists(path))
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{requiredOutput} was not written.",
                        evidence);
                }

                string content = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(content))
                {
                    invalid.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        invalid.Distinct().ToArray(),
                        [],
                        [],
                        $"{requiredOutput} is empty.",
                        evidence.Concat([requiredOutput]).ToArray());
                }

                artifactPaths.Add(requiredOutput);
                evidence.Add(requiredOutput);
            }

            if (operation.RequiredOutputGlob is { } requiredGlob)
            {
                string directory = ResolveRepositoryPath(_repository, requiredGlob.Directory);
                string[] matches = Directory.Exists(directory)
                    ? Directory.GetFiles(directory, requiredGlob.Pattern)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : [];
                if (matches.Length == 0)
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{requiredGlob.Directory}/{requiredGlob.Pattern} was not written.",
                        evidence);
                }

                string[] relativeMatches = matches
                    .Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path))
                    .ToArray();
                ExecutionMilestoneFileSetResult fileSet =
                    ExecutionMilestoneFileSet.Evaluate(relativeMatches);
                if (!fileSet.IsValid)
                {
                    invalid.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        invalid.Distinct().ToArray(),
                        [],
                        [],
                        fileSet.Explanation,
                        evidence.Concat(relativeMatches).Distinct(StringComparer.Ordinal).ToArray());
                }
                foreach (string relativeMatch in relativeMatches)
                {
                    artifactPaths.Add(relativeMatch);
                    evidence.Add(relativeMatch);
                }

                if (operation.RequireChecklistInGlob)
                {
                    ExecutionMilestoneGateResult gate =
                        ExecutionMilestoneGate.Evaluate(_repository.Path, relativeMatches);
                    if (!gate.ReadinessSatisfied)
                    {
                        invalid.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                        return new ProductValidationResult(
                            ProductValidationStatus.Invalid,
                            [],
                            [],
                            invalid.Distinct().ToArray(),
                            [],
                            [],
                            "extracted milestones contain no trackable checkboxes.",
                            evidence);
                    }
                }
            }

            if (operation.ChangedGuard is { } changedGuard)
            {
                string path = ResolveRepositoryPath(_repository, changedGuard);
                if (!File.Exists(path))
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{changedGuard} was not written.",
                        evidence);
                }

                artifactPaths.Add(changedGuard);
                evidence.Add(changedGuard);
            }

            string causalIdentity = await HashArtifactsAsync(artifactPaths);
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    evidence.Distinct(StringComparer.Ordinal).ToArray(),
                    causalIdentity,
                    PlanScopedStorageRepresentations(product, artifactPaths)))
                .ToArray();

            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"Plan scoped artifact transition `{definition.Identity}` produced valid repository artifacts.",
                evidence.Distinct(StringComparer.Ordinal).ToArray());
        }

        private async Task<ProductValidationResult> ValidateTraditionalRoadmapOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output,
            string relativePath,
            string title)
        {
            IReadOnlyList<string> evidence = output.Evidence.Count == 0
                ? [relativePath]
                : output.Evidence;
            string path = ResolveRepositoryPath(_repository, relativePath);
            if (!File.Exists(path))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Missing,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    $"{relativePath} was not written.",
                    evidence);
            }

            string content = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    $"{relativePath} is empty.",
                    evidence.Concat([relativePath]).ToArray());
            }

            IReadOnlyList<string> validationIssues =
                TraditionalRoadmapPromptTransitions.ValidatePrimaryOutput(definition, content);
            if (validationIssues.Count > 0)
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    string.Join("; ", validationIssues),
                    evidence.Concat([relativePath]).ToArray());
            }

            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    evidence.Concat([relativePath]).Distinct(StringComparer.Ordinal).ToArray(),
                    Hash(content),
                    [relativePath]))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"{title} transition `{definition.Identity}` produced a parsed and validated artifact.",
                evidence.Concat([relativePath]).Distinct(StringComparer.Ordinal).ToArray());
        }

        private async Task<ProductValidationResult> ValidateExecuteOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output)
        {
            IReadOnlyList<string> evidence = output.Evidence.Count == 0
                ? ExecuteEvidence(definition)
                : output.Evidence;
            var missing = new List<ProductIdentity>();
            var invalid = new List<ProductIdentity>();
            var artifactPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (string relativePath in ExecuteRequiredArtifacts(definition))
            {
                string path = ResolveRepositoryPath(_repository, relativePath);
                if (!File.Exists(path))
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{relativePath} was not written.",
                        evidence);
                }

                string content = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(content))
                {
                    invalid.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        invalid.Distinct().ToArray(),
                        [],
                        [],
                        $"{relativePath} is empty.",
                        evidence.Concat([relativePath]).ToArray());
                }

                artifactPaths.Add(relativePath);
            }

            foreach (string evidencePath in ExecuteEvidence(definition))
            {
                string path = ResolveRepositoryPath(_repository, evidencePath);
                if (!File.Exists(path))
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{evidencePath} was not written.",
                        evidence);
                }

                artifactPaths.Add(evidencePath);
            }

            if (ExecuteDecisionSessionTransitions.Supports(definition))
            {
                string prompt = await File.ReadAllTextAsync(
                    ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.Decisions));
                string recommendation = await File.ReadAllTextAsync(
                    ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.ExecutionRecommendation));
                try
                {
                    ExecutionRecommendationContract.ValidatePair(prompt, recommendation);
                }
                catch (InvalidDataException exception)
                {
                    invalid.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        invalid.Distinct().ToArray(),
                        [],
                        [],
                        exception.Message,
                        evidence.Concat(artifactPaths).Distinct(StringComparer.Ordinal).ToArray());
                }
            }

            string causalIdentity = await HashArtifactsAsync(artifactPaths);
            IReadOnlyList<string> productEvidence = evidence
                .Concat(artifactPaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    productEvidence,
                    causalIdentity,
                    ExecuteStorageRepresentations(product, definition, productEvidence)))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"Execute transition `{definition.Identity}` produced valid repository artifacts.",
                productEvidence);
        }

        private async Task<string> HashArtifactsAsync(IEnumerable<string> relativePaths)
        {
            var builder = new StringBuilder();
            foreach (string relativePath in relativePaths
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal))
            {
                string path = ResolveRepositoryPath(_repository, relativePath);
                if (!File.Exists(path))
                {
                    continue;
                }

                builder
                    .AppendLine($"--- {relativePath} ---")
                    .AppendLine(await File.ReadAllTextAsync(path));
            }

            return Hash(builder.ToString());
        }
    }
}
