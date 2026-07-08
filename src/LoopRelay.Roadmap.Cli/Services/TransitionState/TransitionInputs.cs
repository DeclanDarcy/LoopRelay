using System.Text;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.State;

namespace LoopRelay.Roadmap.Cli.Services.TransitionState;

internal sealed class TransitionInputResolver(
    RoadmapArtifacts artifacts,
    ExecutionPreparationProvenanceService executionPreparation)
{
    private readonly RoadmapArtifacts _artifacts = artifacts;
    private readonly ExecutionPreparationProvenanceService _executionPreparation = executionPreparation;
    public async Task<TransitionInputSnapshot> ResolveAsync(TransitionInputRequest request)
    {
        var inputs = new TransitionInputAccumulator();

        if (!IsNone(request.ProjectionPath))
        {
            inputs.AddRequired(request.ProjectionPath, TransitionInputRole.Projection);
        }

        await AddPromptInputsAsync(request, inputs);

        IReadOnlyList<TransitionArtifactInput> artifactInputs = await inputs.SnapshotAsync(_artifacts);
        string? projectionHash = artifactInputs
            .FirstOrDefault(input => string.Equals(input.Path, request.ProjectionPath, StringComparison.Ordinal))
            ?.Hash;
        var projection = new TransitionProjectionIdentity(
            request.RuntimePromptName,
            request.ProjectionPath,
            projectionHash);
        string promptContextHash = RoadmapHash.Sha256(request.RenderedContext);
        string secondaryInputHash = RoadmapHash.Sha256(request.SecondaryInput);
        string snapshotHash = ComputeSnapshotHash(
            request.RuntimePromptName,
            projection,
            artifactInputs,
            promptContextHash,
            secondaryInputHash);

        return new TransitionInputSnapshot(
            request.RuntimePromptName,
            projection,
            artifactInputs,
            promptContextHash,
            secondaryInputHash,
            snapshotHash);
    }

    private async Task AddPromptInputsAsync(TransitionInputRequest request, TransitionInputAccumulator inputs)
    {
        switch (request.RuntimePromptName)
        {
            case "CreateRoadmapCompletionContext":
                await AddCompletedEpicInputsAsync(inputs);
                break;
            case "SelectNextEpic":
                inputs.AddRequired(RoadmapArtifactPaths.RoadmapCompletionContext, TransitionInputRole.RoadmapCompletionContext);
                await AddRoadmapSourceInputsAsync(inputs);
                break;
            case "EpicPreparationAudit":
                inputs.AddRequired(RoadmapArtifactPaths.Selection, TransitionInputRole.Selection);
                break;
            case "RealignEpic":
            case "ReimagineEpic":
                await AddCurrentEpicOrSelectionInputAsync(inputs);
                inputs.AddRequired(RequirePath(request.Context.AuditEvidencePath, request.RuntimePromptName), TransitionInputRole.AuditEvidence);
                break;
            case "CreateNewEpic":
            case "SplitEpic":
                inputs.AddRequired(RoadmapArtifactPaths.Selection, TransitionInputRole.Selection);
                break;
            case "GenerateMilestoneDeepDivesForEpic":
                inputs.AddRequired(RoadmapArtifactPaths.ActiveEpic, TransitionInputRole.ActiveEpic);
                break;
            case "EvaluateEpicCompletionAndDrift":
                inputs.AddRequired(RoadmapArtifactPaths.ActiveEpic, TransitionInputRole.ActiveEpic);
                inputs.AddRequired(RequirePath(request.Context.ExecutionEvidencePath, request.RuntimePromptName), TransitionInputRole.ExecutionEvidence);
                await AddMilestoneSpecInputsAsync(inputs);
                break;
            case "UpdateRoadmapCompletionContext":
                inputs.AddRequired(RoadmapArtifactPaths.RoadmapCompletionContext, TransitionInputRole.RoadmapCompletionContext);
                inputs.AddRequired(RoadmapArtifactPaths.ActiveEpic, TransitionInputRole.ActiveEpic);
                inputs.AddRequired(RequirePath(request.Context.CompletionEvaluationPath, request.RuntimePromptName), TransitionInputRole.CompletionEvaluation);
                break;
            case "CompletionCertificationRouting":
                inputs.AddRequired(RequirePath(request.Context.CompletionEvaluationPath, request.RuntimePromptName), TransitionInputRole.CompletionEvaluation);
                break;
        }
    }

    private async Task AddRoadmapSourceInputsAsync(TransitionInputAccumulator inputs)
    {
        IReadOnlyList<string> roadmapFiles = await _artifacts.RequireRoadmapSourcePathsAsync();
        foreach (string path in roadmapFiles)
        {
            inputs.AddRequired(path, TransitionInputRole.RoadmapSource);
        }
    }

    private async Task AddCompletedEpicInputsAsync(TransitionInputAccumulator inputs)
    {
        IReadOnlyList<string> completedEpics = await _artifacts.ListAsync(RoadmapArtifactPaths.CompletedEpicsDirectory, "*.md");
        foreach (string path in completedEpics.Order(StringComparer.Ordinal))
        {
            inputs.AddOptional(path, TransitionInputRole.CompletedEpic);
        }
    }

    private async Task AddCurrentEpicOrSelectionInputAsync(TransitionInputAccumulator inputs)
    {
        if (await _artifacts.ExistsAsync(RoadmapArtifactPaths.ActiveEpic))
        {
            inputs.AddRequired(RoadmapArtifactPaths.ActiveEpic, TransitionInputRole.ActiveEpic);
            return;
        }

        inputs.AddRequired(RoadmapArtifactPaths.Selection, TransitionInputRole.Selection);
    }

    private async Task AddMilestoneSpecInputsAsync(TransitionInputAccumulator inputs)
    {
        IReadOnlyList<string> specs = await _executionPreparation.RequireFreshMilestoneSpecPathsAsync();
        foreach (string path in specs.Order(StringComparer.Ordinal))
        {
            inputs.AddRequired(path, TransitionInputRole.MilestoneSpec);
        }
    }

    private static string RequirePath(string? path, string runtimePromptName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new RoadmapStepException($"Transition input context for {runtimePromptName} did not provide a required evidence path.");
        }

        return path;
    }

    private static bool IsNone(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, "None", StringComparison.OrdinalIgnoreCase);

    private static string ComputeSnapshotHash(
        string runtimePromptName,
        TransitionProjectionIdentity projection,
        IReadOnlyList<TransitionArtifactInput> artifactInputs,
        string promptContextHash,
        string secondaryInputHash)
    {
        var builder = new StringBuilder();
        AppendField(builder, "runtimePromptName", runtimePromptName);
        AppendField(builder, "projectionRuntimePromptName", projection.RuntimePromptName);
        AppendField(builder, "projectionPath", projection.ProjectionPath);
        AppendField(builder, "projectionHash", projection.ProjectionHash ?? string.Empty);
        AppendField(builder, "promptContextHash", promptContextHash);
        AppendField(builder, "secondaryInputHash", secondaryInputHash);

        foreach (TransitionArtifactInput input in artifactInputs)
        {
            AppendField(builder, "artifact.path", input.Path);
            AppendField(builder, "artifact.roles", input.Roles);
            AppendField(builder, "artifact.required", input.Required.ToString());
            AppendField(builder, "artifact.presence", input.Presence.ToString());
            AppendField(builder, "artifact.hash", input.Hash ?? string.Empty);
        }

        return RoadmapHash.Sha256(builder.ToString());
    }

    private static void AppendField(StringBuilder builder, string name, string value)
    {
        builder
            .Append(name.Length)
            .Append(':')
            .Append(name)
            .Append('=')
            .Append(value.Length)
            .Append(':')
            .Append(value)
            .AppendLine();
    }
}
