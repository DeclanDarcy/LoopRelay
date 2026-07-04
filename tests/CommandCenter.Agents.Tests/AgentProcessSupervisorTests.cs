using System.Runtime.CompilerServices;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;

namespace CommandCenter.Agents.Tests;

public sealed class AgentProcessSupervisorTests
{
    [Fact]
    public async Task ObserveCompletionAsyncTransitionsToExitedAndInvokesCallback()
    {
        var process = new FakeAgentProcess();
        var supervisor = new AgentProcessSupervisor(process);
        int? observedExitCode = null;

        process.Complete(0);
        AgentProcessSupervisionResult result = await supervisor.ObserveCompletionAsync(
            exitCode =>
            {
                observedExitCode = exitCode;
                return Task.CompletedTask;
            });

        Assert.Equal(AgentProcessState.Exited, result.State);
        Assert.Equal(AgentProcessState.Exited, supervisor.State);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, supervisor.ExitCode);
        Assert.Equal(0, observedExitCode);
        Assert.Equal(
            [AgentProcessEventKind.ProcessStarted, AgentProcessEventKind.ProcessCompleted],
            supervisor.Events.Select(processEvent => processEvent.Kind).ToArray());
        Assert.Equal([1, 2], supervisor.Events.Select(processEvent => processEvent.Sequence).ToArray());
    }

    [Fact]
    public async Task CompletionTransitionsToExitedWithoutCompatibilityCallback()
    {
        var process = new FakeAgentProcess();
        var supervisor = new AgentProcessSupervisor(process);

        process.Complete(2);
        AgentProcessSupervisionResult result = await supervisor.Completion;

        Assert.Equal(AgentProcessState.Exited, result.State);
        Assert.Equal(AgentProcessState.Exited, supervisor.State);
        Assert.Equal(2, supervisor.ExitCode);
    }

    [Fact]
    public async Task CancelAsyncDisposesProcessAndTransitionsToCanceled()
    {
        var process = new FakeAgentProcess();
        var supervisor = new AgentProcessSupervisor(process);

        await supervisor.CancelAsync();

        Assert.True(process.WasDisposed);
        Assert.Equal(AgentProcessState.Canceled, supervisor.State);
        Assert.Equal(AgentProcessState.Canceled, process.State);
        Assert.Equal(
            [AgentProcessEventKind.ProcessStarted, AgentProcessEventKind.ProcessCancelled],
            supervisor.Events.Select(processEvent => processEvent.Kind).ToArray());
    }

    [Fact]
    public async Task CompletionFailureTransitionsToFailed()
    {
        var process = new FakeAgentProcess();
        var supervisor = new AgentProcessSupervisor(process);

        process.Fail(new InvalidOperationException("process observation failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.ObserveCompletionAsync());
        Assert.Equal(AgentProcessState.Failed, supervisor.State);
        Assert.Equal(
            [AgentProcessEventKind.ProcessStarted, AgentProcessEventKind.ProcessFailed],
            supervisor.Events.Select(processEvent => processEvent.Kind).ToArray());
    }

    [Fact]
    public void AgentProcessEventStreamRecordsStableIdentityAndOrderedFacts()
    {
        var stream = new AgentProcessEventStream();

        AgentProcessEvent output = stream.Record(
            processId: 123,
            AgentProcessEventKind.ProcessOutput,
            AgentProcessState.Running,
            outputStream: AgentProcessOutputStream.StandardOutput,
            content: "line");
        AgentProcessEvent completed = stream.Record(
            processId: 123,
            AgentProcessEventKind.ProcessCompleted,
            AgentProcessState.Exited,
            exitCode: 0);

        Assert.NotEqual(Guid.Empty, output.EventId);
        Assert.Equal(1, output.Sequence);
        Assert.Equal(2, completed.Sequence);
        Assert.Equal(AgentProcessOutputStream.StandardOutput, output.OutputStream);
        Assert.Equal("line", output.Content);
        Assert.Equal(0, completed.ExitCode);
    }

    private sealed class FakeAgentProcess : IAgentProcess
    {
        private readonly TaskCompletionSource completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ProcessId => 123;

        public AgentProcessState State { get; private set; } = AgentProcessState.Running;

        public int? ExitCode { get; private set; }

        public bool HasExited => State is AgentProcessState.Exited or AgentProcessState.Canceled;

        public Task Completion => completion.Task;

        public bool WasDisposed { get; private set; }

        public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CompleteInputAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> ReadOutputLinesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync()
        {
            WasDisposed = true;
            State = AgentProcessState.Canceled;
            completion.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public void Complete(int exitCode)
        {
            ExitCode = exitCode;
            State = AgentProcessState.Exited;
            completion.SetResult();
        }

        public void Fail(Exception exception)
        {
            State = AgentProcessState.Failed;
            completion.SetException(exception);
        }
    }
}
