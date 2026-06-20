using CommandCenter.Backend.Execution;

namespace CommandCenter.Backend.Tests;

public sealed class CodexExecutionProviderTests
{
    [Fact]
    public async Task LaunchesCodexExecFromRepositoryRootWithPromptOnStandardInput()
    {
        var repositoryPath = CreateTemporaryDirectory();
        var processRunner = new RecordingProcessRunner(new ProcessStartResult
        {
            ProcessId = 1234,
            HasExited = false
        });
        var provider = new CodexExecutionProvider(
            new StaticCodexExecutableResolver("C:\\tools\\codex.exe"),
            processRunner);

        var result = await provider.StartAsync(
            CreatePrompt(),
            CreateSession(repositoryPath));

        Assert.Equal("codex", result.ProviderName);
        Assert.Equal("C:\\tools\\codex.exe", result.ExecutablePath);
        Assert.Equal(1234, result.ProcessId);
        Assert.Equal("C:\\tools\\codex.exe", processRunner.FileName);
        Assert.Equal(repositoryPath, processRunner.WorkingDirectory);
        Assert.Equal(["exec", "--cd", repositoryPath, "-"], processRunner.Arguments);
        Assert.Equal("prompt text", processRunner.StandardInput);
    }

    [Fact]
    public async Task MissingCodexExecutableFailsWithStructuredProviderError()
    {
        var provider = new CodexExecutionProvider(
            new ThrowingCodexExecutableResolver(new ExecutionProviderException(
                "ProviderExecutableNotFound",
                "Codex executable was not found.")),
            new RecordingProcessRunner(new ProcessStartResult()));

        var exception = await Assert.ThrowsAsync<ExecutionProviderException>(() =>
            provider.StartAsync(CreatePrompt(), CreateSession(CreateTemporaryDirectory())));

        Assert.Equal("ProviderExecutableNotFound", exception.Code);
    }

    [Fact]
    public async Task ProcessStartFailureIsStructured()
    {
        var provider = new CodexExecutionProvider(
            new StaticCodexExecutableResolver("C:\\tools\\codex.exe"),
            new RecordingProcessRunner(new IOException("start failed")));

        var exception = await Assert.ThrowsAsync<ExecutionProviderException>(() =>
            provider.StartAsync(CreatePrompt(), CreateSession(CreateTemporaryDirectory())));

        Assert.Equal("ProviderLaunchFailed", exception.Code);
    }

    [Fact]
    public async Task ImmediateProviderExitIsStructured()
    {
        var provider = new CodexExecutionProvider(
            new StaticCodexExecutableResolver("C:\\tools\\codex.exe"),
            new RecordingProcessRunner(new ProcessStartResult
            {
                ProcessId = 456,
                HasExited = true,
                ExitCode = 2
            }));

        var exception = await Assert.ThrowsAsync<ExecutionProviderException>(() =>
            provider.StartAsync(CreatePrompt(), CreateSession(CreateTemporaryDirectory())));

        Assert.Equal("ProviderImmediateExit", exception.Code);
        Assert.Contains("2", exception.Message);
    }

    private static ExecutionPrompt CreatePrompt()
    {
        return new ExecutionPrompt
        {
            Text = "prompt text",
            Metadata = new ExecutionPromptMetadata
            {
                RepositoryPath = "repo",
                MilestonePath = ".agents/milestones/m2.md",
                IncludedArtifactPaths = [".agents/plan.md"],
                TotalContextBytes = 11,
                TotalContextCharacters = 11
            }
        };
    }

    private static ExecutionSession CreateSession(string repositoryPath)
    {
        return new ExecutionSession
        {
            Id = Guid.NewGuid(),
            RepositoryPath = repositoryPath,
            MilestonePath = ".agents/milestones/m2.md"
        };
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class StaticCodexExecutableResolver(string path) : ICodexExecutableResolver
    {
        public CodexExecutable Resolve()
        {
            return new CodexExecutable { Path = path };
        }
    }

    private sealed class ThrowingCodexExecutableResolver(Exception exception) : ICodexExecutableResolver
    {
        public CodexExecutable Resolve()
        {
            throw exception;
        }
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        private readonly ProcessStartResult? startResult;
        private readonly Exception? startException;

        public RecordingProcessRunner(ProcessStartResult startResult)
        {
            this.startResult = startResult;
        }

        public RecordingProcessRunner(Exception startException)
        {
            this.startException = startException;
        }

        public string? FileName { get; private set; }

        public IReadOnlyList<string>? Arguments { get; private set; }

        public string? WorkingDirectory { get; private set; }

        public string? StandardInput { get; private set; }

        public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
        {
            throw new NotSupportedException();
        }

        public Task<ProcessStartResult> StartAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            string? standardInput = null)
        {
            FileName = fileName;
            Arguments = arguments.ToArray();
            WorkingDirectory = workingDirectory;
            StandardInput = standardInput;

            if (startException is not null)
            {
                throw startException;
            }

            return Task.FromResult(startResult!);
        }
    }
}
