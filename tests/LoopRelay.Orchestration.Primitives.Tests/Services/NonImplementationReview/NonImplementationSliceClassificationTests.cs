using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.RepositorySlices;

namespace LoopRelay.Orchestration.Tests.Services.NonImplementationReview;

public sealed class NonImplementationSliceClassificationTests
{
    [Fact]
    public async Task Classifier_excludes_code_files_under_src_and_tests()
    {
        var classifier = new NonImplementationArtifactClassifier();
        RepositorySliceDelta delta = Delta(
            File("src/LoopRelay.Core/Foo.cs"),
            File("tests/LoopRelay.Core.Tests/FooTests.cs"));

        NonImplementationArtifactClassificationSet result = await classifier.ClassifyAsync(delta);

        Assert.All(result.Classifications, classification =>
            Assert.Equal(NonImplementationArtifactRoute.ExcludedImplementationArtifact, classification.Route));
        Assert.All(result.Classifications, classification =>
            Assert.Equal(NonImplementationArtifactClassifier.Version, classification.ClassifierVersion));
    }

    [Fact]
    public void Classifier_excludes_machine_required_and_prompt_resources()
    {
        var classifier = new NonImplementationArtifactClassifier();

        NonImplementationArtifactRoute csproj = classifier.Classify(File("src/App/App.csproj")).Route;
        NonImplementationArtifactRoute slnx = classifier.Classify(File("LoopRelay.slnx")).Route;
        NonImplementationArtifactRoute package = classifier.Classify(File("package.json")).Route;
        NonImplementationArtifactRoute config = classifier.Classify(File("tests/LoopRelay.Core.Tests/xunit.runner.json")).Route;
        NonImplementationArtifactRoute prompt = classifier.Classify(File("src/LoopRelay.Core/Prompts/StartExecution.prompt")).Route;
        NonImplementationArtifactRoute lockfile = classifier.Classify(File("package-lock.json")).Route;

        Assert.Equal(NonImplementationArtifactRoute.ExcludedMachineRequiredArtifact, csproj);
        Assert.Equal(NonImplementationArtifactRoute.ExcludedMachineRequiredArtifact, slnx);
        Assert.Equal(NonImplementationArtifactRoute.ExcludedMachineRequiredArtifact, package);
        Assert.Equal(NonImplementationArtifactRoute.ExcludedMachineRequiredArtifact, config);
        Assert.Equal(NonImplementationArtifactRoute.ExcludedImplementationArtifact, prompt);
        Assert.Equal(NonImplementationArtifactRoute.ExcludedMachineRequiredArtifact, lockfile);
    }

    [Fact]
    public void Classifier_excludes_agents_operational_files_as_sanctioned()
    {
        var classifier = new NonImplementationArtifactClassifier();

        NonImplementationArtifactClassification result = classifier.Classify(
            File(OrchestrationArtifactPaths.NonImplementationLedger));

        Assert.Equal(NonImplementationArtifactRoute.ExcludedSanctionedOperationalArtifact, result.Route);
        Assert.Equal("sanctioned-operational-agents", result.RuleId);
        Assert.Contains(result.PathFacts, fact => fact == "path=.agents/review/non-implementation-ledger.json");
    }

    [Fact]
    public void Classifier_routes_root_docs_and_issues_markdown_as_semantic_candidates()
    {
        var classifier = new NonImplementationArtifactClassifier();

        Assert.Equal(
            NonImplementationArtifactRoute.SemanticReviewCandidate,
            classifier.Classify(File("README.md")).Route);
        Assert.Equal(
            NonImplementationArtifactRoute.SemanticReviewCandidate,
            classifier.Classify(File("docs/design.md")).Route);
        Assert.Equal(
            NonImplementationArtifactRoute.SemanticReviewCandidate,
            classifier.Classify(File("issues/123.md")).Route);
    }

    [Fact]
    public void Classifier_routes_unknown_files_as_ambiguous_for_semantic_review()
    {
        var classifier = new NonImplementationArtifactClassifier();

        NonImplementationArtifactClassification result = classifier.Classify(File("notes/archive.custom"));

        Assert.Equal(NonImplementationArtifactRoute.AmbiguousForSemanticReview, result.Route);
        Assert.Equal("ambiguous-unknown-file", result.RuleId);
    }

    [Fact]
    public void Classifier_output_is_deterministic_and_carries_candidate_identity_evidence()
    {
        var classifier = new NonImplementationArtifactClassifier();
        RepositoryChangedFileFacts file = File("docs/design.md");

        NonImplementationArtifactClassification first = classifier.Classify(file);
        NonImplementationArtifactClassification second = classifier.Classify(file);

        Assert.Equal(first.Route, second.Route);
        Assert.Equal(first.RuleId, second.RuleId);
        Assert.Equal(first.Rationale, second.Rationale);
        Assert.Equal(first.ClassifierVersion, second.ClassifierVersion);
        Assert.Equal(first.PathFacts, second.PathFacts);
        Assert.Equal(NonImplementationArtifactRoute.SemanticReviewCandidate, first.Route);
        Assert.Contains(first.PathFacts, fact => fact == "postSha256=hash");
        Assert.Same(file, first.File);
    }

    [Fact]
    public async Task Baseline_delta_excludes_pre_existing_dirty_files_unchanged_by_execution()
    {
        using TempRepository repo = TempRepository.Create();
        repo.Write("docs/design.md", "before");
        var runner = new ScriptedProcessRunner();
        runner.EnqueueStatus(" M docs/design.md\n");
        runner.EnqueueDiff("M\tdocs/design.md\n");
        runner.EnqueueStatus(" M docs/design.md\n");
        runner.EnqueueDiff("M\tdocs/design.md\n");
        var store = NewBaselineStore(repo, runner);

        RepositorySliceBaseline baseline = await store.CapturePreSliceAsync("slice-test", DateTimeOffset.UnixEpoch);
        RepositorySliceDelta delta = await store.CapturePostSliceDeltaAsync(baseline, DateTimeOffset.UnixEpoch);

        Assert.Empty(delta.ChangedFiles);
    }

    [Fact]
    public async Task Baseline_delta_includes_pre_existing_dirty_files_modified_by_execution()
    {
        using TempRepository repo = TempRepository.Create();
        repo.Write("docs/design.md", "before");
        var runner = new ScriptedProcessRunner();
        runner.EnqueueStatus(" M docs/design.md\n");
        runner.EnqueueDiff("M\tdocs/design.md\n");
        runner.EnqueueStatus(" M docs/design.md\n");
        runner.EnqueueDiff("M\tdocs/design.md\n");
        var store = NewBaselineStore(repo, runner);

        RepositorySliceBaseline baseline = await store.CapturePreSliceAsync("slice-test", DateTimeOffset.UnixEpoch);
        repo.Write("docs/design.md", "after");
        RepositorySliceDelta delta = await store.CapturePostSliceDeltaAsync(baseline, DateTimeOffset.UnixEpoch);

        RepositoryChangedFileFacts changed = Assert.Single(delta.ChangedFiles);
        Assert.Equal("docs/design.md", changed.Path);
        Assert.True(changed.PreExisted);
        Assert.Equal(" M", changed.BaselineStatus);
        Assert.Equal(" M", changed.PostStatus);
        Assert.NotEqual(changed.BaselineContentSha256, changed.PostContentSha256);
        Assert.Equal("M", Assert.Single(changed.TrackedDiffMetadata).Status);
    }

    [Fact]
    public async Task Baseline_delta_discovers_untracked_files_created_during_execution()
    {
        using TempRepository repo = TempRepository.Create();
        var runner = new ScriptedProcessRunner();
        runner.EnqueueStatus("");
        runner.EnqueueDiff("");
        runner.EnqueueStatus("?? notes.md\n");
        runner.EnqueueDiff("");
        var store = NewBaselineStore(repo, runner);

        RepositorySliceBaseline baseline = await store.CapturePreSliceAsync("slice-test", DateTimeOffset.UnixEpoch);
        repo.Write("notes.md", "new note");
        RepositorySliceDelta delta = await store.CapturePostSliceDeltaAsync(baseline, DateTimeOffset.UnixEpoch);

        RepositoryChangedFileFacts changed = Assert.Single(delta.ChangedFiles);
        Assert.Equal("notes.md", changed.Path);
        Assert.False(changed.PreExisted);
        Assert.Equal("??", changed.PostStatus);
        Assert.True(changed.Exists);
        Assert.NotNull(changed.PostContentSha256);
    }

    [Fact]
    public async Task Baseline_delta_uses_rename_destination_path_and_preserves_source_evidence()
    {
        using TempRepository repo = TempRepository.Create();
        repo.Write("new.md", "renamed");
        var runner = new ScriptedProcessRunner();
        runner.EnqueueStatus("");
        runner.EnqueueDiff("");
        runner.EnqueueStatus("R  old.md -> new.md\n");
        runner.EnqueueDiff("R100\told.md\tnew.md\n");
        var store = NewBaselineStore(repo, runner);

        RepositorySliceBaseline baseline = await store.CapturePreSliceAsync("slice-test", DateTimeOffset.UnixEpoch);
        RepositorySliceDelta delta = await store.CapturePostSliceDeltaAsync(baseline, DateTimeOffset.UnixEpoch);

        RepositoryChangedFileFacts changed = Assert.Single(delta.ChangedFiles);
        Assert.Equal("new.md", changed.Path);
        Assert.Equal("old.md", changed.PreviousPath);
        RepositoryGitDiffNameStatus diff = Assert.Single(changed.TrackedDiffMetadata);
        Assert.Equal("R100", diff.Status);
        Assert.Equal("old.md", diff.PreviousPath);
        Assert.Equal("new.md", diff.Path);
    }

    [Fact]
    public async Task Detector_captures_added_deleted_modified_and_staged_paths()
    {
        using TempRepository repo = TempRepository.Create();
        repo.Write("src/New.cs", "new");
        repo.Write("src/Staged.cs", "staged");
        var runner = new ScriptedProcessRunner();
        runner.EnqueueStatus("""
            A  src/New.cs
             D docs/deleted.md
            M  src/Staged.cs
            """);
        runner.EnqueueDiff("""
            A	src/New.cs
            D	docs/deleted.md
            M	src/Staged.cs
            """);
        var repository = new Repository { Id = Guid.NewGuid(), Name = "test", Path = repo.Root };
        var detector = new RepositoryChangeSetDetector(runner, repository);

        RepositorySliceSnapshot snapshot = await detector.CaptureSnapshotAsync("slice-test", DateTimeOffset.UnixEpoch);

        Assert.Equal(["docs/deleted.md", "src/New.cs", "src/Staged.cs"], snapshot.Files.Select(file => file.Path));
        RepositoryFileSnapshotEntry deleted = snapshot.Files.Single(file => file.Path == "docs/deleted.md");
        Assert.True(deleted.IsDeleted);
        Assert.False(deleted.Exists);
        RepositoryFileSnapshotEntry added = snapshot.Files.Single(file => file.Path == "src/New.cs");
        Assert.Equal("A ", added.Status);
        Assert.True(added.Exists);
        Assert.NotNull(added.ContentSha256);
        RepositoryFileSnapshotEntry staged = snapshot.Files.Single(file => file.Path == "src/Staged.cs");
        Assert.Equal("M ", staged.Status);
        Assert.Equal("M", Assert.Single(staged.TrackedDiffMetadata).Status);
    }

    [Fact]
    public async Task Baseline_store_assigns_slice_id_and_persists_pre_and_post_metadata_when_configured()
    {
        using TempRepository repo = TempRepository.Create();
        var runner = new ScriptedProcessRunner();
        runner.EnqueueStatus("");
        runner.EnqueueDiff("");
        runner.EnqueueStatus("");
        runner.EnqueueDiff("");
        var artifacts = new InMemoryArtifactStore();
        var repository = new Repository { Id = Guid.NewGuid(), Name = "test", Path = repo.Root };
        var store = new RepositorySliceBaselineStore(new RepositoryChangeSetDetector(runner, repository), artifacts);

        RepositorySliceBaseline baseline = await store.CapturePreSliceAsync("slice-test", DateTimeOffset.UnixEpoch);
        await store.CapturePostSliceAsync(baseline, DateTimeOffset.UnixEpoch);

        Assert.Equal("slice-test", baseline.ExecutionSliceId);
        Assert.Equal(OrchestrationArtifactPaths.NonImplementationSliceBaseline("slice-test"), baseline.PersistedPath);
        string? baselineJson = await artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationSliceBaseline("slice-test"));
        string? postJson = await artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationSlicePostSnapshot("slice-test"));
        Assert.Contains("\"executionSliceId\": \"slice-test\"", baselineJson);
        Assert.Contains("\"executionSliceId\": \"slice-test\"", postJson);
    }

    private static RepositorySliceBaselineStore NewBaselineStore(
        TempRepository repository,
        ScriptedProcessRunner runner)
    {
        var repo = new Repository { Id = Guid.NewGuid(), Name = "test", Path = repository.Root };
        return new RepositorySliceBaselineStore(new RepositoryChangeSetDetector(runner, repo));
    }

    private static RepositorySliceDelta Delta(params RepositoryChangedFileFacts[] files) =>
        new("slice-test", files);

    private static RepositoryChangedFileFacts File(string path) =>
        new(
            "slice-test",
            path.Replace('\\', '/'),
            PreviousPath: null,
            Status: " M",
            BaselineStatus: null,
            PostStatus: " M",
            PreExisted: false,
            Exists: true,
            IsDeleted: false,
            Extension: Path.GetExtension(path).ToLowerInvariant(),
            Size: 1,
            BaselineContentSha256: null,
            PostContentSha256: "hash",
            TrackedDiffMetadata: Array.Empty<RepositoryGitDiffNameStatus>());

    private sealed class ScriptedProcessRunner : IProcessRunner
    {
        private readonly Queue<ProcessRunResult> statusResults = new();
        private readonly Queue<ProcessRunResult> diffResults = new();

        public void EnqueueStatus(string stdout) => statusResults.Enqueue(Ok(stdout));

        public void EnqueueDiff(string stdout) => diffResults.Enqueue(Ok(stdout));

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory)
        {
            if (fileName != "git")
            {
                throw new InvalidOperationException("Only git calls are expected.");
            }

            return arguments.FirstOrDefault() switch
            {
                "status" => Task.FromResult(statusResults.Dequeue()),
                "diff" => Task.FromResult(diffResults.Dequeue()),
                _ => throw new InvalidOperationException($"Unexpected git command: {string.Join(" ", arguments)}"),
            };
        }

        public Task<IAgentProcess> StartInteractiveAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static ProcessRunResult Ok(string stdout) =>
            new() { ExitCode = 0, StandardOutput = stdout, StandardError = string.Empty, Duration = TimeSpan.Zero };
    }

    private sealed class InMemoryArtifactStore : IArtifactStore
    {
        private readonly Dictionary<string, string> files = new(StringComparer.Ordinal);

        public Task<bool> ExistsAsync(string path) => Task.FromResult(files.ContainsKey(path));

        public Task<string?> ReadAsync(string path)
        {
            files.TryGetValue(path, out string? content);
            return Task.FromResult(content);
        }

        public Task WriteAsync(string path, string content)
        {
            files[path] = content;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string path)
        {
            files.Remove(path);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class TempRepository : IDisposable
    {
        private TempRepository(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TempRepository Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "looprelay-non-implementation-review",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempRepository(root);
        }

        public void Write(string relativePath, string content)
        {
            string fullPath = ArtifactPath.ResolveRepositoryPath(
                new Repository { Path = Root },
                relativePath.Replace('\\', '/'));
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            System.IO.File.WriteAllText(fullPath, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
