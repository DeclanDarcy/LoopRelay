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
    private sealed class UnifiedOutputInterpreter(Repository _repository) : IOutputInterpreter
    {
        public async Task<InterpretedTransitionOutput> InterpretAsync(
            CanonicalCausalContext causality,
            WorkflowTransitionDefinition definition,
            PromptExecutionResult executionResult,
            CancellationToken cancellationToken)
        {
            if (PlanWarmSessionTransitions.Supports(definition))
            {
                IReadOnlyList<string> evidence = PlanWarmSessionTransitions.Evidence(definition)
                    .Concat([OrchestrationArtifactPaths.Plan])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Plan warm-session output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (PlanProjectionTransitions.Supports(definition))
            {
                IReadOnlyList<string> evidence = PlanProjectionTransitions.Evidence(definition)
                    .Concat([PlanPromptContext.AdversarialPlanReviewProjectionPath])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Plan projection output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out EvalPromptAsset evalAsset))
            {
                if (string.IsNullOrWhiteSpace(executionResult.RawOutput))
                {
                    return new InterpretedTransitionOutput(
                        OutputInterpretationStatus.Unavailable,
                        [],
                        $"Eval prompt `{definition.Identity}` returned no output.",
                        []);
                }

                string path = ResolveRepositoryPath(_repository, evalAsset.PrimaryOutputPath);
                if (!AgentAuthoredPrimaryOutput(executionResult, evalAsset.PrimaryOutputPath) || !File.Exists(path))
                {
                    await new DurableFilesystemWriteEffectPlanner(_repository).WriteCandidateAsync(
                        causality,
                        evalAsset.PrimaryOutputPath,
                        executionResult.RawOutput,
                        cancellationToken);
                }
                IReadOnlyList<string> evidence = EvalPromptTransitions.Evidence(definition)
                    .Concat([evalAsset.PrimaryOutputPath])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Eval prompt output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (TraditionalRoadmapPromptTransitions.TryGetPrimaryOutput(definition, out string roadmapOutput))
            {
                if (string.IsNullOrWhiteSpace(executionResult.RawOutput))
                {
                    return new InterpretedTransitionOutput(
                        OutputInterpretationStatus.Unavailable,
                        [],
                        $"Traditional roadmap prompt `{definition.Identity}` returned no output.",
                        []);
                }

                string path = ResolveRepositoryPath(_repository, roadmapOutput);
                if (!AgentAuthoredPrimaryOutput(executionResult, roadmapOutput) || !File.Exists(path))
                {
                    await new DurableFilesystemWriteEffectPlanner(_repository).WriteCandidateAsync(
                        causality,
                        roadmapOutput,
                        executionResult.RawOutput,
                        cancellationToken);
                }
                IReadOnlyList<string> evidence = TraditionalRoadmapPromptTransitions.Evidence(definition)
                    .Concat([roadmapOutput])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Traditional roadmap prompt output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (MilestoneDeepDiveTransitions.Supports(definition))
            {
                string specsDirectory = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.SpecsDirectory);
                string[] existing = Directory.Exists(specsDirectory)
                    ? Directory.GetFiles(specsDirectory, "*.md")
                    : [];
                IReadOnlyList<string> materialized = existing.Length == 0
                    ? await MaterializeMilestoneBundleAsync(causality, executionResult.RawOutput, cancellationToken)
                    : existing.Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path)).ToArray();
                if (materialized.Count == 0)
                {
                    string normalized = executionResult.RawOutput.Replace("\r\n", "\n", StringComparison.Ordinal);
                    int fileMarkers = normalized.Split('\n').Count(line =>
                        line.Trim().StartsWith("# FILE:", StringComparison.OrdinalIgnoreCase));
                    int specHeadings = normalized.Split("# Milestone Spec:", StringSplitOptions.None).Length - 1;
                    return new InterpretedTransitionOutput(
                        OutputInterpretationStatus.Incomplete,
                        [],
                        $"Milestone deep-dive output contained no valid `.agents/specs/*.md` file bundle " +
                        $"(length={executionResult.RawOutput.Length}, file-markers={fileMarkers}, spec-headings={specHeadings}).",
                        MilestoneDeepDiveTransitions.Evidence(definition));
                }

                IReadOnlyList<string> evidence = MilestoneDeepDiveTransitions.Evidence(definition)
                    .Concat(materialized)
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Milestone deep-dive output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (PlanReadOnlyReviewTransitions.Supports(definition))
            {
                if (string.IsNullOrWhiteSpace(executionResult.RawOutput))
                {
                    return new InterpretedTransitionOutput(
                        OutputInterpretationStatus.Unavailable,
                        [],
                        "Adversarial review output was empty.",
                        []);
                }

                await new DurableFilesystemWriteEffectPlanner(_repository).WriteCandidateAsync(
                    causality,
                    PlanReadOnlyReviewTransitions.ReviewOutputPath,
                    executionResult.RawOutput,
                    cancellationToken);
                IReadOnlyList<string> evidence = PlanReadOnlyReviewTransitions.Evidence(definition)
                    .Concat([PlanReadOnlyReviewTransitions.ReviewOutputPath])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Plan read-only review output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (PlanScopedArtifactTransitions.Supports(definition))
            {
                PlanScopedArtifactOperationSpec operation =
                    PlanScopedArtifactOperationCatalog.Get(definition.Identity);
                IReadOnlyList<string> evidence = PlanScopedArtifactTransitions.Evidence(definition)
                    .Concat(operation.RequiredOutputs)
                    .Concat(operation.RequiredOutputGlob is { } glob
                        ? [$"{glob.Directory}/{glob.Pattern}"]
                        : Array.Empty<string>())
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Plan scoped artifact output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (ExecuteDecisionSessionTransitions.Supports(definition) ||
                ExecuteImplementationTransitions.Supports(definition) ||
                ExecuteRepositoryStateTransitions.Supports(definition) ||
                ExecuteReviewTransitions.Supports(definition))
            {
                return await InterpretExecuteOutputAsync(causality, definition, executionResult, cancellationToken);
            }

            if (!LocalVerificationTransitions.Supports(definition) &&
                !LocalArtifactTransitions.Supports(definition))
            {
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Unavailable,
                    [],
                    "Output interpretation is not wired because prompt execution did not run.",
                    []);
            }

            IReadOnlyList<string> localEvidence = LocalVerificationTransitions.Supports(definition)
                ? LocalVerificationTransitions.Evidence(definition)
                : LocalArtifactTransitions.Evidence(definition);
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(product, definition.Identity, localEvidence))
                .ToArray();
            return new InterpretedTransitionOutput(
                OutputInterpretationStatus.Valid,
                products,
                $"Local verification interpreted `{definition.Identity}` successfully.",
                localEvidence);
        }

        private static bool AgentAuthoredPrimaryOutput(PromptExecutionResult result, string expectedPath) =>
            result.Metadata.TryGetValue("primary-output-mutated", out string? mutated) &&
            bool.TryParse(mutated, out bool parsed) && parsed &&
            result.Metadata.TryGetValue("primary-output-path", out string? path) &&
            string.Equals(path, expectedPath, StringComparison.Ordinal);

        private async Task<IReadOnlyList<string>> MaterializeMilestoneBundleAsync(
            CanonicalCausalContext causality,
            string output,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(output)) return [];
            string[] lines = output
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
            var files = new List<(string RelativePath, string Content)>();
            string? currentPath = null;
            var content = new StringBuilder();

            void CompleteCurrent()
            {
                if (currentPath is null) return;
                string value = content.ToString().Trim();
                if (value.Length > 0) files.Add((currentPath, value + Environment.NewLine));
                content.Clear();
            }

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("# FILE:", StringComparison.OrdinalIgnoreCase))
                {
                    CompleteCurrent();
                    currentPath = ValidateMilestoneBundlePath(trimmed["# FILE:".Length..].Trim());
                    continue;
                }

                if (currentPath is not null) content.AppendLine(line);
            }
            CompleteCurrent();

            if (files.Count == 0)
            {
                string normalized = output.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
                if (normalized.StartsWith("```", StringComparison.Ordinal) &&
                    normalized.EndsWith("```", StringComparison.Ordinal))
                {
                    int firstNewline = normalized.IndexOf('\n');
                    normalized = firstNewline >= 0 ? normalized[(firstNewline + 1)..^3].Trim() : string.Empty;
                }
                const string heading = "# Milestone Spec:";
                int firstHeading = normalized.IndexOf(heading, StringComparison.Ordinal);
                int secondHeading = firstHeading < 0
                    ? -1
                    : normalized.IndexOf(heading, firstHeading + heading.Length, StringComparison.Ordinal);
                if (firstHeading >= 0 && secondHeading < 0)
                {
                    files.Add((
                        ".agents/specs/m1.md",
                        normalized[firstHeading..].Trim() + Environment.NewLine));
                }
            }

            if (files.Count == 0 ||
                files.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count ||
                files.Any(file => !file.Content.Contains("# Milestone Spec:", StringComparison.Ordinal)))
            {
                return [];
            }

            foreach ((string relativePath, string fileContent) in files)
            {
                await new DurableFilesystemWriteEffectPlanner(_repository).WriteCandidateAsync(
                    causality,
                    relativePath,
                    fileContent,
                    cancellationToken);
            }
            return files.Select(file => file.RelativePath).ToArray();
        }

        private static string ValidateMilestoneBundlePath(string candidate)
        {
            string normalized = candidate.Replace('\\', '/');
            const string prefix = ".agents/specs/";
            string fileName = normalized.StartsWith(prefix, StringComparison.Ordinal)
                ? normalized[prefix.Length..]
                : string.Empty;
            if (fileName.Length == 0 ||
                fileName.Contains('/') ||
                !fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                fileName is ".md" or "..md" ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidOperationException(
                    $"Milestone bundle path `{candidate}` is outside the canonical `.agents/specs/*.md` scope.");
            }
            return prefix + fileName;
        }

        private async Task<InterpretedTransitionOutput> InterpretExecuteOutputAsync(
            CanonicalCausalContext causality,
            WorkflowTransitionDefinition definition,
            PromptExecutionResult executionResult,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(executionResult.RawOutput))
            {
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Unavailable,
                    [],
                    $"Execute transition `{definition.Identity}` returned no output.",
                    []);
            }

            IReadOnlyList<string> evidence = ExecuteEvidence(definition);
            if (definition.Identity.Value == "InterpretCompletionRoute")
            {
                await new DurableFilesystemWriteEffectPlanner(_repository).WriteCandidateAsync(
                    causality,
                    ExecuteReviewTransitions.CompletionRouteOutputPath,
                    executionResult.RawOutput,
                    cancellationToken);
            }
            foreach (string evidencePath in evidence)
            {
                await new DurableFilesystemWriteEffectPlanner(_repository).WriteCandidateAsync(
                    causality,
                    evidencePath,
                    $"""
                    # Execute Transition Output

                    Transition: {definition.Identity}
                    Prompt: {definition.PromptIdentity}

                    {executionResult.RawOutput}
                    """,
                    cancellationToken);
            }

            IReadOnlyList<string> productEvidence = evidence
                .Concat(ExecuteArtifactEvidence(definition))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string causalIdentity = Hash(executionResult.RawOutput);
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    productEvidence,
                    causalIdentity,
                    ExecuteStorageRepresentations(product, definition, productEvidence)))
                .ToArray();
            return new InterpretedTransitionOutput(
                OutputInterpretationStatus.Valid,
                products,
                $"Execute transition output interpreted for `{definition.Identity}`.",
                productEvidence);
        }
    }

}
