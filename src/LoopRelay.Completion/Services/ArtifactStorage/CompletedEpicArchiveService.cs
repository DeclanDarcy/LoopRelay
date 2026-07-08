using System.Globalization;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Prompts;
using LoopRelay.Completion.Services.Observers;
using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Completion.Services.ArtifactStorage;

public sealed class CompletedEpicArchiveService(
    IArtifactStore _store,
    ICompletionPromptRunner _promptRunner,
    ICompletionObserver? _observer = null) : ICompletedEpicArchiveService
{

    public async Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
        CompletedEpicArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var artifacts = new CompletionArtifacts(_store, request.Repository);
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

        (_observer ?? NullCompletionObserver.Instance).Phase("Archive completed execution workspace");
        await artifacts.CopyFileIfPresentAsync(request.ActiveEpicPath, $"{archiveDirectory}/epic.md");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.DecisionsDirectory, $"{archiveDirectory}/decisions");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.DeltasDirectory, $"{archiveDirectory}/deltas");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.HandoffsDirectory, $"{archiveDirectory}/handoffs");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.MilestonesDirectory, $"{archiveDirectory}/milestones");
        await artifacts.MoveDirectoryContentsAsync(CompletionArtifactPaths.NonImplementationReviewDirectory, $"{archiveDirectory}/review");
        await artifacts.MoveFileIfPresentAsync(CompletionArtifactPaths.Details, $"{archiveDirectory}/details.md");
        await artifacts.MoveFileIfPresentAsync(CompletionArtifactPaths.OperationalContext, $"{archiveDirectory}/operational_context.md");
        await artifacts.MoveFileIfPresentAsync(CompletionArtifactPaths.ExecutionPlan, $"{archiveDirectory}/plan.md");

        (_observer ?? NullCompletionObserver.Instance).Phase("Synthesize completed epic");
        string label = index.ToString(CultureInfo.InvariantCulture);
        _ = await _promptRunner.RunAsync(
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
