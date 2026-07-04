using CommandCenter.Execution;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Modules;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Tests;

public sealed class CodexExecutionProviderTests
{
    [Fact]
    public async Task LaunchesTextModeCodexExecWithFullAccessSandboxAndNeverApproval()
    {
        string repositoryPath = CreateTemporaryDirectory();
        var processRunner = new RecordingProcessRunner(new ProcessStartResult
        {
            ProcessId = 1234,
            HasExited = false
        });
        var provider = new CodexExecutionProvider(
            new StaticCodexExecutableResolver("C:\\tools\\codex.exe"),
            processRunner);

        ExecutionProviderStartResult result = await provider.StartAsync(
            CreatePrompt(),
            CreateSession(repositoryPath),
            new RecordingObserver());

        Assert.Equal("codex", result.ProviderName);
        Assert.Equal("C:\\tools\\codex.exe", result.ExecutablePath);
        Assert.Equal(1234, result.ProcessId);
        Assert.Equal("C:\\tools\\codex.exe", processRunner.FileName);
        Assert.Equal(repositoryPath, processRunner.WorkingDirectory);
        Assert.Equal(
            ["exec", "--cd", repositoryPath, "--sandbox", "danger-full-access", "-c", "approval_policy=\"never\"", "-"],
            processRunner.Arguments);
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
            provider.StartAsync(CreatePrompt(), CreateSession(CreateTemporaryDirectory()), new RecordingObserver()));

        Assert.Equal("ProviderExecutableNotFound", exception.Code);
    }

    [Fact]
    public async Task ProcessStartFailureIsStructured()
    {
        var provider = new CodexExecutionProvider(
            new StaticCodexExecutableResolver("C:\\tools\\codex.exe"),
            new RecordingProcessRunner(new IOException("start failed")));

        var exception = await Assert.ThrowsAsync<ExecutionProviderException>(() =>
            provider.StartAsync(CreatePrompt(), CreateSession(CreateTemporaryDirectory()), new RecordingObserver()));

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
            provider.StartAsync(CreatePrompt(), CreateSession(CreateTemporaryDirectory()), new RecordingObserver()));

        Assert.Equal("ProviderImmediateExit", exception.Code);
        Assert.Contains("2", exception.Message);
    }

    [Fact]
    public async Task CodexProviderDeclaresReattachUnsupported()
    {
        var provider = new CodexExecutionProvider(
            new StaticCodexExecutableResolver("C:\\tools\\codex.exe"),
            new RecordingProcessRunner(new ProcessStartResult()));

        bool reattached = await provider.TryReattachAsync(
            CreateSession(CreateTemporaryDirectory()),
            new RecordingObserver());

        Assert.False(provider.SupportsReattach);
        Assert.False(reattached);
    }

    private static ExecutionPrompt CreatePrompt()
    {
        return new ExecutionPrompt
        {
            Text = "prompt text",
            Metadata = new ExecutionPromptMetadata
            {
                RepositoryPath = "repo",
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
            RepositoryPath = repositoryPath
        };
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
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
            string? standardInput = null,
            Func<string, Task>? onStandardOutput = null,
            Func<string, Task>? onStandardError = null,
            Func<int?, Task>? onExit = null)
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

        public Task<IAgentProcess> StartInteractiveAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingObserver : IExecutionProviderObserver
    {
        public Task OnStdOutAsync(string text)
        {
            return Task.CompletedTask;
        }

        public Task OnStdErrAsync(string text)
        {
            return Task.CompletedTask;
        }

        public Task OnProviderExitedAsync(int? exitCode)
        {
            return Task.CompletedTask;
        }

        public Task OnProviderCancelledAsync(string reason)
        {
            return Task.CompletedTask;
        }
    }
}
