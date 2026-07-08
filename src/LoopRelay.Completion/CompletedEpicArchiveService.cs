using System.Globalization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Completion;

public sealed class CompletedEpicArchiveService(
    IArtifactStore store,
    ICompletionPromptRunner promptRunner,
    ICompletionObserver? observer = null) : ICompletedEpicArchiveService
{
    private readonly ICompletionObserver observer = observer ?? NullCompletionObserver.Instance;

    public async Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
        CompletedEpicArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var artifacts = new CompletionArtifacts(store, request.Repository);
        int index = await ComputeArchiveIndexAsync(artifacts, request.ArchiveRoot);
        string archiveDirectory = $"{request.ArchiveRoot}/{index}";
        string synthesisPath = $"{request.ArchiveRoot}/{index}.md";

        if (await artifacts.ExistsAsync(archiveDirectory) ||
            (await artifacts.ListAsync(archiveDirectory, "*")).Count > 0)
        {
            throw new CompletionCertificationException($"Completed epic archive collision: {archiveDirectory}");
        }

        if (await artifacts.ExistsAsync(synthesisPath))
        {
            throw new CompletionCertificationException($"Completed epic synthesis collision: {synthesisPath}");
        }

        this.observer.Phase("Archive completed execution workspace");
        await artifacts.CopyFileIfPresentAsync(request.ActiveEpicPath, $"{archiveDirectory}/epic.md");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.DecisionsDirectory, $"{archiveDirectory}/decisions");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.DeltasDirectory, $"{archiveDirectory}/deltas");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.HandoffsDirectory, $"{archiveDirectory}/handoffs");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.MilestonesDirectory, $"{archiveDirectory}/milestones");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.NonImplementationReviewDirectory, $"{archiveDirectory}/review");
        await artifacts.MoveFileIfPresentAsync(CompletionArtifactPaths.Details, $"{archiveDirectory}/details.md");
        await artifacts.MoveFileIfPresentAsync(CompletionArtifactPaths.OperationalContext, $"{archiveDirectory}/operational_context.md");
        await artifacts.MoveFileIfPresentAsync(CompletionArtifactPaths.ExecutionPlan, $"{archiveDirectory}/plan.md");

        this.observer.Phase("Synthesize completed epic");
        string label = index.ToString(CultureInfo.InvariantCulture);
        _ = await promptRunner.RunAsync(
            new CompletionRuntimePromptInvocation(
                CompletionRuntimePromptNames.SynthesizeCompletedEpic,
                Label: label),
            cancellationToken);

        string? synthesis = await artifacts.ReadAsync(synthesisPath);
        if (string.IsNullOrWhiteSpace(synthesis))
        {
            throw new CompletionCertificationException(
                $"SynthesizeCompletedEpic did not write required output: {synthesisPath}");
        }

        return new CompletedEpicArchiveResult(index, archiveDirectory, synthesisPath, synthesis);
    }

    private static async Task<int> ComputeArchiveIndexAsync(CompletionArtifacts artifacts, string archiveRoot)
    {
        IReadOnlyList<string> directories = await artifacts.ListDirectoriesAsync(archiveRoot);
        return directories.Count + 1;
    }
}
