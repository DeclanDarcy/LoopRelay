using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Services;

namespace CommandCenter.Backend.Tests;

[Collection("ProcessEnvironment")]
public sealed class ProcessRunnerStderrDrainTests
{
    [Fact]
    public async Task StartInteractiveAsync_DoesNotDeadlock_WhenChildFloodsStderr()
    {
        // Regression for the codex-exec hang: StartInteractiveAsync redirects standard error, and if
        // nothing reads it, a child that writes past the OS pipe buffer (a few KB) to stderr blocks
        // forever — which is exactly how `codex exec` without --json wedged the loop. AgentProcess's
        // StartErrorDrain must keep the pipe moving so the child can finish and the turn can complete.
        string dir = Path.GetTempPath();
        string scriptPath;
        string file;
        string[] args;

        if (OperatingSystem.IsWindows())
        {
            // ~20000 lines (~600 KB) to stderr only, then exit — far beyond any stderr pipe buffer.
            scriptPath = Path.Combine(dir, $"flood-{Guid.NewGuid():N}.bat");
            await File.WriteAllTextAsync(
                scriptPath,
                "@echo off\r\nfor /L %%i in (1,1,20000) do @echo XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX 1>&2\r\n");
            file = "cmd.exe";
            args = ["/c", scriptPath];
        }
        else
        {
            scriptPath = Path.Combine(dir, $"flood-{Guid.NewGuid():N}.sh");
            await File.WriteAllTextAsync(
                scriptPath,
                "i=0\nwhile [ $i -lt 20000 ]; do echo XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX 1>&2; i=$((i+1)); done\n");
            file = "/bin/sh";
            args = [scriptPath];
        }

        IAgentProcess? process = null;
        try
        {
            var runner = new ProcessRunner();
            process = await runner.StartInteractiveAsync(file, args, dir);

            // Stdout is empty; this enumeration completes only when the process EXITS. Without the stderr
            // drain the child blocks on its full stderr pipe and never exits, so this would never complete.
            IAgentProcess started = process;
            Task drainStdout = Task.Run(async () =>
            {
                await foreach (string _ in started.ReadOutputLinesAsync())
                {
                }
            });

            Task winner = await Task.WhenAny(drainStdout, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.True(
                winner == drainStdout,
                "StartInteractiveAsync deadlocked on heavy stderr — the stderr pipe was not drained.");
            await drainStdout; // surface any read exception
        }
        finally
        {
            if (process is not null)
            {
                await process.DisposeAsync();
            }

            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
                // Best-effort cleanup of the temp flood script.
            }
        }
    }
}
