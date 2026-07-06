using System.Text;

namespace LoopRelay.Roadmap.Cli;

internal sealed class TransitionInputResolver(
    RoadmapArtifacts artifacts,
    ExecutionPreparationProvenanceService executionPreparation)
{
    public async Task<TransitionInputSnapshot> ResolveAsync(TransitionInputRequest request)
    {
        var inputs = new TransitionInputAccumulator();

        if (!IsNone(request.ProjectionPath))
        {
            inputs.AddRequired(request.ProjectionPath, TransitionInputRole.Projection);
        }

        await AddPromptInputsAsync(request, inputs);

        IReadOnlyList<TransitionArtifactInput> artifactInputs = await inputs.SnapshotAsync(artifacts);
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
        inputs.AddOptional(RoadmapArtifactPaths.RoadmapFile, TransitionInputRole.RoadmapSource);
        IReadOnlyList<string> roadmapFiles = await artifacts.ListAsync(RoadmapArtifactPaths.RoadmapDirectory, "*.md");
        foreach (string path in roadmapFiles.Order(StringComparer.Ordinal))
        {
            inputs.AddRequired(path, TransitionInputRole.RoadmapSource);
        }
    }

    private async Task AddCompletedEpicInputsAsync(TransitionInputAccumulator inputs)
    {
        IReadOnlyList<string> completedEpics = await artifacts.ListAsync(RoadmapArtifactPaths.CompletedEpicsDirectory, "*.md");
        foreach (string path in completedEpics.Order(StringComparer.Ordinal))
        {
            inputs.AddOptional(path, TransitionInputRole.CompletedEpic);
        }
    }

    private async Task AddCurrentEpicOrSelectionInputAsync(TransitionInputAccumulator inputs)
    {
        if (await artifacts.ExistsAsync(RoadmapArtifactPaths.ActiveEpic))
        {
            inputs.AddRequired(RoadmapArtifactPaths.ActiveEpic, TransitionInputRole.ActiveEpic);
            return;
        }

        inputs.AddRequired(RoadmapArtifactPaths.Selection, TransitionInputRole.Selection);
    }

    private async Task AddMilestoneSpecInputsAsync(TransitionInputAccumulator inputs)
    {
        IReadOnlyList<string> specs = await executionPreparation.RequireFreshMilestoneSpecPathsAsync();
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

internal sealed record TransitionInputRequest(
    string RuntimePromptName,
    string ProjectionPath,
    string RenderedContext,
    string SecondaryInput,
    TransitionInputContext Context);

internal sealed record TransitionInputContext(
    string? AuditEvidencePath = null,
    string? CompletionEvaluationPath = null,
    string? ExecutionEvidencePath = null)
{
    public static TransitionInputContext Empty { get; } = new();

    public static TransitionInputContext AuditEvidence(string path) =>
        new(AuditEvidencePath: path);

    public static TransitionInputContext CompletionEvaluation(string path) =>
        new(CompletionEvaluationPath: path);

    public static TransitionInputContext ExecutionEvidence(string path) =>
        new(ExecutionEvidencePath: path);
}

internal sealed record TransitionInputSnapshot(
    string RuntimePromptName,
    TransitionProjectionIdentity Projection,
    IReadOnlyList<TransitionArtifactInput> ArtifactInputs,
    string PromptContextHash,
    string SecondaryInputHash,
    string SnapshotHash)
{
    public IReadOnlyDictionary<string, string> ToInputArtifactHashes() =>
        new SortedDictionary<string, string>(
            ArtifactInputs
                .Where(input => input.Presence == TransitionInputPresence.Present && input.Hash is not null)
                .ToDictionary(input => input.Path, input => input.Hash!, StringComparer.Ordinal),
            StringComparer.Ordinal);
}

internal sealed record TransitionProjectionIdentity(
    string RuntimePromptName,
    string ProjectionPath,
    string? ProjectionHash);

internal sealed record TransitionArtifactInput(
    string Path,
    string Roles,
    bool Required,
    TransitionInputPresence Presence,
    string? Hash);

internal enum TransitionInputPresence
{
    Present,
    MissingOptional,
}

internal static class TransitionInputRole
{
    public const string Projection = "Projection";
    public const string RoadmapCompletionContext = "RoadmapCompletionContext";
    public const string RoadmapSource = "RoadmapSource";
    public const string Selection = "Selection";
    public const string ActiveEpic = "ActiveEpic";
    public const string AuditEvidence = "AuditEvidence";
    public const string MilestoneSpec = "MilestoneSpec";
    public const string ExecutionEvidence = "ExecutionEvidence";
    public const string CompletionEvaluation = "CompletionEvaluation";
    public const string CompletedEpic = "CompletedEpic";
}

internal sealed class TransitionInputAccumulator
{
    private readonly Dictionary<string, PendingTransitionInput> inputs = new(StringComparer.Ordinal);

    public void AddRequired(string path, string role) => Add(path, role, required: true);

    public void AddOptional(string path, string role) => Add(path, role, required: false);

    public async Task<IReadOnlyList<TransitionArtifactInput>> SnapshotAsync(RoadmapArtifacts artifacts)
    {
        var snapshot = new List<TransitionArtifactInput>();
        foreach (PendingTransitionInput input in inputs.Values.OrderBy(input => input.Path, StringComparer.Ordinal))
        {
            string? content = await artifacts.ReadAsync(input.Path);
            if (content is null)
            {
                if (input.Required)
                {
                    throw new RoadmapStepException($"Required transition input is missing: {input.Path}");
                }

                snapshot.Add(new TransitionArtifactInput(
                    input.Path,
                    input.JoinedRoles(),
                    Required: false,
                    TransitionInputPresence.MissingOptional,
                    Hash: null));
                continue;
            }

            snapshot.Add(new TransitionArtifactInput(
                input.Path,
                input.JoinedRoles(),
                input.Required,
                TransitionInputPresence.Present,
                RoadmapHash.Sha256(content)));
        }

        return snapshot;
    }

    private void Add(string path, string role, bool required)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Transition input path cannot be empty.", nameof(path));
        }

        if (!inputs.TryGetValue(path, out PendingTransitionInput? input))
        {
            input = new PendingTransitionInput(path);
            inputs.Add(path, input);
        }

        input.Required |= required;
        input.Roles.Add(role);
    }

    private sealed class PendingTransitionInput(string path)
    {
        public string Path { get; } = path;
        public bool Required { get; set; }
        public SortedSet<string> Roles { get; } = new(StringComparer.Ordinal);
        public string JoinedRoles() => string.Join("+", Roles);
    }
}
