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
    internal static class LocalVerificationTransitions
    {
        private static readonly HashSet<string> Supported =
        [
            "SelectEvaluationIntent",
            "VerifyPlanEntryContract",
            "VerifyExecuteEntryContract",
            "VerifyExecutionReadiness",
            "VerifyWorkflowExitGate",
        ];

        public static bool Supports(WorkflowTransitionDefinition definition) =>
            Supported.Contains(definition.Identity.Value);

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/local-verification/{definition.Identity}.md"];
    }

    internal static bool ExplicitSingleMilestoneInvariantViolated(
        string repositoryPath,
        out int actualMilestoneCount)
    {
        string strategicStructure = Path.Combine(
            repositoryPath,
            ".agents",
            "ctx",
            "04-strategic-structure.md");
        actualMilestoneCount = 0;
        if (!File.Exists(strategicStructure)) return false;

        string content = File.ReadAllText(strategicStructure);
        bool requiresExactlyOne = content.Contains("exactly one", StringComparison.OrdinalIgnoreCase) &&
            content.Contains("milestone", StringComparison.OrdinalIgnoreCase);
        if (!requiresExactlyOne) return false;

        string milestones = Path.Combine(repositoryPath, ".agents", "milestones");
        actualMilestoneCount = Directory.Exists(milestones)
            ? Directory.GetFiles(milestones, "*.md", SearchOption.TopDirectoryOnly).Length
            : 0;
        return actualMilestoneCount != 1;
    }

    internal static class LocalArtifactTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "GenerateOperationalContext";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/local-artifacts/{definition.Identity}.md"];
    }

    internal static class PlanProjectionTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "GenerateAdversarialProjection";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/plan-projection/{definition.Identity}.md"];
    }

    internal static class EvalPromptTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out _);

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/eval-prompt/{definition.Identity}.md"];
    }

    internal static class TraditionalRoadmapPromptTransitions
    {
        private const string RoadmapCompletionContext = ".agents/core/roadmap-completion-context.md";
        private const string Selection = ".agents/selection.md";
        private const string ActiveEpic = ".agents/epic.md";
        private const string EpicAudit = ".LoopRelay/evidence/traditional-roadmap-prompt/AuditExistingEpic-output.md";

        private static readonly IReadOnlyDictionary<string, string> PrimaryOutputs =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BootstrapRoadmapCompletionContext"] = RoadmapCompletionContext,
                ["UpdateRoadmapCompletionContext"] = RoadmapCompletionContext,
                ["SelectStrategicInitiative"] = Selection,
                ["AuditExistingEpic"] = EpicAudit,
                ["CreateEpic"] = ActiveEpic,
                ["SplitEpic"] = ActiveEpic,
                ["RealignEpic"] = ActiveEpic,
                ["ReimagineEpic"] = ActiveEpic,
                ["RetireEpic"] = RoadmapCompletionContext,
            };

        public static bool Supports(WorkflowTransitionDefinition definition) =>
            PrimaryOutputs.ContainsKey(definition.Identity.Value);

        public static bool TryGetPrimaryOutput(
            WorkflowTransitionDefinition definition,
            out string primaryOutput) =>
            PrimaryOutputs.TryGetValue(definition.Identity.Value, out primaryOutput!);

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/traditional-roadmap-prompt/{definition.Identity}.md"];

        public static IReadOnlyList<string> ValidatePrimaryOutput(
            WorkflowTransitionDefinition definition,
            string content) =>
            definition.Identity.Value switch
            {
                "BootstrapRoadmapCompletionContext" or "UpdateRoadmapCompletionContext" or "RetireEpic" =>
                    ContainsHeading(content, "# Roadmap Completion Context")
                        ? []
                        : ["roadmap completion context output is missing `# Roadmap Completion Context`."],
                "SelectStrategicInitiative" =>
                    ContainsAnyHeading(
                        content,
                        "# Strategic Initiative Selection",
                        "# Next Strategic Initiative Selection")
                        ? []
                        : ["strategic initiative selection output is missing a recognized selection heading."],
                "AuditExistingEpic" =>
                    ContainsHeading(content, "## Selected Epic") &&
                    ContainsHeading(content, "## Audit Disposition") &&
                    ContainsHeading(content, "## Final Disposition Statement")
                        ? []
                        : ["epic preparation audit output is missing its required audit structure."],
                "CreateEpic" or "SplitEpic" or "RealignEpic" or "ReimagineEpic" =>
                    ValidatePreparedEpic(content),
                _ => [],
            };

        private static bool ContainsAnyHeading(string content, params string[] headings) =>
            headings.Any(heading => ContainsHeading(content, heading));

        private static bool ContainsHeading(string content, string heading) =>
            content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Any(line => string.Equals(line.Trim(), heading, StringComparison.OrdinalIgnoreCase));

        private static IReadOnlyList<string> ValidatePreparedEpic(string content)
        {
            string[] lines = content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
            var issues = new List<string>();
            int epicHeadings = lines.Count(line => line.TrimStart().StartsWith("# Epic:", StringComparison.Ordinal));
            if (epicHeadings == 0)
            {
                issues.Add("prepared epic is missing top-level `# Epic:` heading.");
            }
            else if (epicHeadings > 1)
            {
                issues.Add("prepared epic contains multiple top-level `# Epic:` headings.");
            }

            RequireHeading(lines, "## Epic Metadata", issues);
            if (!HasHeading(lines, "## Strategic Purpose") && !HasHeading(lines, "## Strategic Continuity"))
            {
                issues.Add("prepared epic is missing `## Strategic Purpose` or `## Strategic Continuity` section.");
            }

            RequireHeading(lines, "## Desired Capability", issues);
            RequireHeading(lines, "## Acceptance Criteria", issues);
            RequireHeading(lines, "## Milestone Roadmap", issues);
            if (!HasMilestoneRoadmapTable(lines))
            {
                issues.Add("prepared epic is missing the required milestone roadmap table header.");
            }

            return issues;
        }

        private static void RequireHeading(string[] lines, string heading, List<string> issues)
        {
            if (!HasHeading(lines, heading))
            {
                issues.Add($"prepared epic is missing `{heading}` section.");
            }
        }

        private static bool HasHeading(string[] lines, string heading) =>
            lines.Any(line => string.Equals(line.Trim(), heading, StringComparison.Ordinal));

        private static bool HasMilestoneRoadmapTable(string[] lines)
        {
            const string requiredHeader = "|MilestoneID|MilestoneName|Purpose|Outcome|DependsOn|CompletionSignal|";
            return lines
                .Select(line => line.Replace(" ", string.Empty, StringComparison.Ordinal).Trim())
                .Any(line => string.Equals(line, requiredHeader, StringComparison.Ordinal));
        }
    }

    internal static class MilestoneDeepDiveTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "GenerateMilestoneDeepDivesForEpic";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/milestone-deep-dive/{definition.Identity}.md"];
    }

    internal static class PlanReadOnlyReviewTransitions
    {
        public const string ReviewOutputPath = ".LoopRelay/evidence/plan/adversarial-review.md";

        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "RunAdversarialReview" &&
            definition.ExecutionPosture.Kind == ExecutionPostureKind.ReadOnlyPrompt;

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/plan-read-only-review/{definition.Identity}.md"];
    }

    internal static class PlanScopedArtifactTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.ExecutionPosture.Kind == ExecutionPostureKind.ScopedArtifactOperation &&
            PlanScopedArtifactOperationCatalog.Supports(definition.Identity);

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/plan-scoped-artifact/{definition.Identity}.md"];
    }

    internal static class PlanWarmSessionTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "WriteExecutablePlan" or "RevisePlan" &&
            definition.ExecutionPosture.Kind == ExecutionPostureKind.WarmSession;

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/plan-warm-session/{definition.Identity}.md"];
    }

    internal static class ExecuteDecisionSessionTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "GenerateDecision" or "TransferDecisionSession" or "ContinueDecisionSession" &&
            definition.ExecutionPosture.Kind == ExecutionPostureKind.DecisionSession;

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/execute-decision-session/{definition.Identity}.md"];
    }

    internal static class ExecuteImplementationTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "ExecuteImplementationSlice" or "GenerateHandoff";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "ExecuteImplementationSlice"
                ? [".agents/evidence/execution/execution.md"]
                : [$".LoopRelay/evidence/execute-implementation/{definition.Identity}.md"];
    }

    internal static class ExecuteRepositoryStateTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "UpdateOperationalContext" or "PublishRepositoryState" or
                "EvaluateCommit" or "EvaluateMilestoneCompletion";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/execute-repository-state/{definition.Identity}.md"];
    }

    internal static class ExecuteReviewTransitions
    {
        public const string CompletionRouteOutputPath =
            ".LoopRelay/evidence/execute-review/InterpretCompletionRoute-output.md";

        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "RunNonImplementationReview" or "RunCompletionCertification" or
                "InterpretCompletionRoute";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/execute-review/{definition.Identity}.md"];
    }

    private sealed class UnifiedPromptRenderer : IPromptRenderer
    {
        public Task<RenderedPrompt> RenderAsync(
            PromptRenderRequest request,
            CancellationToken cancellationToken)
        {
            WorkflowTransitionDefinition definition = request.Definition;
            PromptContext context = request.Context;
            string text;
            string evidence;
            string? templateSourceHash;
            if (LocalVerificationTransitions.Supports(definition))
            {
                text = $"Local verification transition `{definition.Identity}` validates already-observed canonical products.";
                evidence = LocalVerificationTransitions.Evidence(definition)[0];
                templateSourceHash = null;
            }
            else if (LocalArtifactTransitions.Supports(definition))
            {
                text = $"Local artifact transition `{definition.Identity}` materializes deterministic repository-owned artifacts.";
                evidence = LocalArtifactTransitions.Evidence(definition)[0];
                templateSourceHash = null;
            }
            else if (EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out EvalPromptAsset asset))
            {
                text = RenderEvalPromptAsset(asset, context);
                evidence = $"unified-cli/prompts/eval/{asset.PromptAssetName}@{asset.SourceHash}";
                templateSourceHash = asset.SourceHash;
            }
            else if (CanonicalPromptAssetCatalog.TryGetByPromptIdentity(definition.PromptIdentity, out CanonicalPromptAsset canonicalAsset))
            {
                text = definition.Identity.Value switch
                {
                    "RunAdversarialReview" => RenderAdversarialPlanReviewPrompt(context),
                    "ExecuteImplementationSlice" => RenderExecuteImplementationPrompt(context),
                    _ => RenderCanonicalPromptAsset(canonicalAsset, context),
                };
                evidence = $"unified-cli/prompts/core/{canonicalAsset.PromptAssetName}@{canonicalAsset.SourceHash}";
                templateSourceHash = canonicalAsset.SourceHash;
            }
            else
            {
                // No catalog owns this prompt identity: the placeholder never reaches an agent —
                // the executor fails closed with a typed not-wired result before any send — and
                // the evidence names the unwired state instead of fabricating an asset path.
                text = $"Prompt `{definition.PromptIdentity}` is registered but execution integration is not wired.";
                evidence = $"unified-cli/prompts/unwired/{definition.PromptIdentity}";
                templateSourceHash = null;
            }

            return Task.FromResult(new RenderedPrompt(
                new PromptTemplateIdentity(definition.PromptIdentity),
                request.PolicyProfile,
                text,
                evidence,
                templateSourceHash));
        }

        private static string RenderEvalPromptAsset(
            EvalPromptAsset asset,
            PromptContext context)
        {
            string sections = context.Sections.Count == 0
                ? "No additional prompt context sections were provided."
                : string.Join(
                    Environment.NewLine + Environment.NewLine,
                    context.Sections.Select(section =>
                        $"""
                        ## {section.Title}

                        Source: {section.SourcePath}
                        Evidence: {string.Join(", ", section.Evidence)}

                        {section.Content}
                        """));
            return $"""
            {asset.PromptTemplate}

            ---

            # Canonical Runtime Context

            Prompt asset: {asset.PromptAssetName}
            Source hash: {asset.SourceHash}
            Primary output: {asset.PrimaryOutput}
            Primary output path: {asset.PrimaryOutputPath}

            ## Runtime Source Boundary

            The canonical input products required for this transition are embedded below. Use them directly as authoritative source content.
            Do not inspect the repository, invoke shell or filesystem tools, create an internal plan, or call planning tools.
            Return only the final Markdown for the primary output now; the runtime materializes it at the declared path.
            Keep the artifact compact while preserving every source-required semantic field, relationship, negative control, and traceability obligation.
            Represent the supplied scope exactly once: do not create multiple IDs for the same source obligation merely to restate its lifecycle phase, evidence view, negative-control variant, or implementation alternative.
            For a single-capability or single-milestone source, emit the smallest schema-complete set of items and groups. Keep acceptance evidence and negative controls inside the owning item unless the source or required schema explicitly makes them distinct nodes.
            Do not invent speculative dependencies, hypotheses, catalog items, or milestones beyond the embedded canonical inputs.

            {sections}
            """;
        }

        private static string RenderCanonicalPromptAsset(
            CanonicalPromptAsset asset,
            PromptContext context)
        {
            string promptTemplate = RenderTraditionalRoadmapPrompt(asset, context);
            if (asset.PromptIdentity == "GenerateMilestoneDeepDivesForEpic")
            {
                promptTemplate += """

                    ## Runtime Source Boundary

                    The authoritative selected epic is already resolved and embedded below as the `PreparedEpic` input product from `.agents/epic.md`.
                    Use that embedded product directly. A redundant filesystem lookup is not required.
                    If you do inspect the repository, the exact path is `.agents/epic.md` (including the leading dot), never `agents/epic.md`.
                    Do not block milestone-spec generation because a redundant lookup fails; emit the required `.agents/specs/*.md` file bundle from the embedded authoritative epic.
                    """;
            }
            string products = context.Inputs.Products.Count == 0
                ? "No input products were resolved."
                : string.Join(
                    Environment.NewLine,
                    context.Inputs.Products.Select(product =>
                        $"- {product.Identity}: {string.Join(", ", product.StorageRepresentations)}"));
            string sections = context.Sections.Count == 0
                ? "No additional prompt context sections were provided."
                : string.Join(
                    Environment.NewLine + Environment.NewLine,
                    context.Sections.Select(section =>
                        $"""
                        ## {section.Title}

                        Source: {section.SourcePath}
                        Evidence: {string.Join(", ", section.Evidence)}

                        {section.Content}
                        """));
            return $"""
            {promptTemplate}

            ---

            # Canonical Runtime Context

            Prompt asset: {asset.PromptAssetName}
            Prompt identity: {asset.PromptIdentity}
            Source hash: {asset.SourceHash}

            ## Input Products

            {products}

            ## Context Sections

            {sections}
            """;
        }

        private static string RenderTraditionalRoadmapPrompt(
            CanonicalPromptAsset asset,
            PromptContext context)
        {
            string projectContext = Section(context, "Project Context");
            string roadmapContext = ProductSection(context, ProductIdentity.RoadmapCompletionContext);
            string selection = ProductSection(context, ProductIdentity.StrategicInitiativeSelection);
            string epic = ProductSection(context, ProductIdentity.PreparedEpic);

            return asset.PromptIdentity switch
            {
                "BootstrapRoadmapCompletionContext" =>
                    Core.Prompts.Planning.CreateRoadmapCompletionContext.Render(projectContext, string.Empty),
                "UpdateRoadmapCompletionContext" =>
                    Core.Prompts.Planning.UpdateRoadmapCompletionContext.Render(projectContext, roadmapContext),
                "SelectStrategicInitiative" =>
                    Core.Prompts.Planning.SelectNextEpic.Render(projectContext),
                "AuditExistingEpic" =>
                    Core.Prompts.Planning.EpicPreparationAudit.Render(projectContext, selection),
                "CreateNewEpic" =>
                    Core.Prompts.Planning.CreateNewEpic.Render(projectContext, selection),
                "SplitEpic" =>
                    Core.Prompts.Planning.SplitEpic.Render(projectContext, epic),
                "RealignEpic" =>
                    Core.Prompts.Planning.RealignEpic.Render(projectContext, epic),
                "ReimagineEpic" =>
                    Core.Prompts.Planning.ReimagineEpic.Render(projectContext, epic),
                "GenerateMilestoneDeepDivesForEpic" =>
                    Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.Render(projectContext),
                _ => asset.PromptTemplate,
            };
        }

        private static string ProductSection(PromptContext context, ProductIdentity product) =>
            Section(context, $"Input Product: {product.Value}");

        private static string Section(PromptContext context, string title) =>
            context.Sections.FirstOrDefault(section =>
                string.Equals(section.Title, title, StringComparison.Ordinal))?.Content ?? string.Empty;

        private static string RenderAdversarialPlanReviewPrompt(PromptContext context)
        {
            // All instruction text is template-owned (covered by the asset SourceHash); this branch only
            // routes the two declared inputs into their positional holes.
            string projection = RequiredSection(context, "Adversarial Projection");
            string plan = RequiredSection(context, "Executable Plan");
            return AdversarialPlanReview.Render(projection, plan);
        }

        private static string RequiredSection(PromptContext context, string title) =>
            context.Sections.FirstOrDefault(section => string.Equals(section.Title, title, StringComparison.Ordinal))?.Content
            ?? throw new InvalidOperationException($"Plan prompt context did not include `{title}`.");

        private static string RenderExecuteImplementationPrompt(PromptContext context)
        {
            string plan = RequiredSection(context, "Executable Plan");
            string? details = Section(context, "Execution Details");
            string decisions = RequiredSection(context, "Decision Set");
            string? readme = Section(context, "Repository README");
            string milestones = RequiredSection(context, "Execution Milestones");
            return ContinueExecution.Render(
                plan,
                AppendExecutionContext(details, readme, milestones),
                decisions);
        }

        private static string AppendExecutionContext(
            string? details,
            string? repositoryReadme,
            string executionMilestones)
        {
            return $"""
                {details}

                # Execution Milestone Context

                The following milestone documents are authoritative execution state. Work only on the current applicable unchecked item(s), and change each checkbox to `[x]` when its item is completed and locally verified during this slice.

                <EXECUTION_MILESTONES>
                {executionMilestones.Trim()}
                </EXECUTION_MILESTONES>

                {RenderRepositoryReadmeContext(repositoryReadme)}
                """;
        }

        private static string RenderRepositoryReadmeContext(string? repositoryReadme)
        {
            if (string.IsNullOrWhiteSpace(repositoryReadme))
            {
                return string.Empty;
            }

            return $"""
                # Repository README Context

                The following repository-owned README content is authoritative execution context. Use it directly when the plan or verifier refers to README-defined values; do not attempt to infer those values from hashes.
                When the README specifies exact required content and the verifier accepts multiple values, the README resolves the canonical target. A broader verifier allowlist is not ambiguity and must not block implementation solely because it contains multiple accepted values.

                <REPOSITORY_README>
                {repositoryReadme.Trim()}
                </REPOSITORY_README>
                """;
        }
    }

}
