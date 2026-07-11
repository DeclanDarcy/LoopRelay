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
    ICompletionObserver? _observer = null,
    ICompletedEpicArchiveMaterializer? _archiveMaterializer = null) : ICompletedEpicArchiveService
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
        IReadOnlyList<ArchiveFileOperation> retainedOperations =
            await BuildRetainedArchivePlanAsync(artifacts, request.ActiveEpicPath, archiveDirectory);
        await ValidateArchiveTargetsAsync(artifacts, retainedOperations);
        await (_archiveMaterializer ?? NullCompletedEpicArchiveMaterializer.Instance).MaterializeAsync(
            _store,
            request.Repository,
            archiveDirectory,
            cancellationToken);
        await ValidateArchiveTargetsAsync(artifacts, retainedOperations, allowIdenticalExisting: true);
        await ExecuteRetainedArchivePlanAsync(artifacts, retainedOperations);

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

    private static async Task<IReadOnlyList<ArchiveFileOperation>> BuildRetainedArchivePlanAsync(
        CompletionArtifacts artifacts,
        string activeEpicPath,
        string archiveDirectory)
    {
        var operations = new List<ArchiveFileOperation>();
        await AddFileOperationIfPresentAsync(artifacts, operations, activeEpicPath, $"{archiveDirectory}/epic.md", DeleteSource: false);
        await AddDirectoryMoveOperationsAsync(artifacts, operations, CompletionArtifactPaths.DecisionsDirectory, $"{archiveDirectory}/decisions");
        await AddDirectoryMoveOperationsAsync(artifacts, operations, CompletionArtifactPaths.DeltasDirectory, $"{archiveDirectory}/deltas");
        await AddDirectoryMoveOperationsAsync(artifacts, operations, CompletionArtifactPaths.HandoffsDirectory, $"{archiveDirectory}/handoffs");
        await AddDirectoryMoveOperationsAsync(artifacts, operations, CompletionArtifactPaths.MilestonesDirectory, $"{archiveDirectory}/milestones");
        await AddDirectoryMoveOperationsAsync(artifacts, operations, CompletionArtifactPaths.NonImplementationReviewDirectory, $"{archiveDirectory}/review");
        await AddFileOperationIfPresentAsync(artifacts, operations, CompletionArtifactPaths.Details, $"{archiveDirectory}/details.md", DeleteSource: true);
        await AddFileOperationIfPresentAsync(artifacts, operations, CompletionArtifactPaths.OperationalContext, $"{archiveDirectory}/operational_context.md", DeleteSource: true);
        await AddFileOperationIfPresentAsync(artifacts, operations, CompletionArtifactPaths.ExecutionPlan, $"{archiveDirectory}/plan.md", DeleteSource: true);
        return operations;
    }

    private static async Task AddDirectoryMoveOperationsAsync(
        CompletionArtifacts artifacts,
        List<ArchiveFileOperation> operations,
        string sourceDirectory,
        string targetDirectory)
    {
        IReadOnlyList<string> files = await artifacts.ListAsync(sourceDirectory, "*");
        foreach (string sourcePath in files.Order(StringComparer.Ordinal))
        {
            string targetPath = $"{targetDirectory.TrimEnd('/')}/{RelativeSuffix(sourceDirectory, sourcePath)}";
            await AddFileOperationIfPresentAsync(artifacts, operations, sourcePath, targetPath, DeleteSource: true);
        }
    }

    private static async Task AddFileOperationIfPresentAsync(
        CompletionArtifacts artifacts,
        List<ArchiveFileOperation> operations,
        string sourcePath,
        string targetPath,
        bool DeleteSource)
    {
        string? content = await artifacts.ReadAsync(sourcePath);
        if (content is null)
        {
            return;
        }

        operations.Add(new ArchiveFileOperation(sourcePath, Normalize(targetPath), content, DeleteSource));
    }

    private static async Task ValidateArchiveTargetsAsync(
        CompletionArtifacts artifacts,
        IReadOnlyList<ArchiveFileOperation> operations,
        bool allowIdenticalExisting = false)
    {
        foreach (string duplicate in operations
            .GroupBy(operation => operation.TargetPath, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            throw new CompletionCertificationException($"Archive target planned more than once: {duplicate}");
        }

        foreach (ArchiveFileOperation operation in operations)
        {
            if (await artifacts.ExistsAsync(operation.TargetPath))
            {
                if (allowIdenticalExisting &&
                    string.Equals(
                        await artifacts.ReadAsync(operation.TargetPath),
                        operation.Content,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                throw new CompletionCertificationException($"Archive target already exists: {operation.TargetPath}");
            }
        }
    }

    private static async Task ExecuteRetainedArchivePlanAsync(
        CompletionArtifacts artifacts,
        IReadOnlyList<ArchiveFileOperation> operations)
    {
        foreach (ArchiveFileOperation operation in operations)
        {
            await artifacts.WriteAsync(operation.TargetPath, operation.Content);
        }

        foreach (ArchiveFileOperation operation in operations.Where(operation => operation.DeleteSource))
        {
            await artifacts.DeleteAsync(operation.SourcePath);
        }
    }

    private static string RelativeSuffix(string sourceDirectory, string sourcePath)
    {
        string normalizedDirectory = Normalize(sourceDirectory).TrimEnd('/');
        string normalizedPath = Normalize(sourcePath);
        if (!normalizedPath.StartsWith(normalizedDirectory + "/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(normalizedPath);
        }

        return normalizedPath[(normalizedDirectory.Length + 1)..];
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    private sealed record ArchiveFileOperation(
        string SourcePath,
        string TargetPath,
        string Content,
        bool DeleteSource);
}
