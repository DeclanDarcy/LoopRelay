using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Services.Process;

namespace LoopRelay.Agents.Tests.Services.Process;

[Collection("ProcessEnvironment")]
public sealed class ProcessRunnerStderrDrainTests
{
    [Fact]
    public async Task StartInteractiveAsync_WritesPromptAsBomlessUtf8()
    {
        if (!OperatingSystem.IsWindows()) return;

        string scriptPath = Path.Combine(Path.GetTempPath(), $"stdin-bytes-{Guid.NewGuid():N}.ps1");
        const string prompt = "ASCII—漢字🙂\n";
        await File.WriteAllTextAsync(scriptPath, """
            $inputStream = [Console]::OpenStandardInput()
            $memory = [System.IO.MemoryStream]::new()
            $inputStream.CopyTo($memory)
            [Convert]::ToHexString($memory.ToArray()).ToLowerInvariant()
            """);
        IAgentProcess? process = null;
        try
        {
            var runner = new ProcessRunner();
            process = await runner.StartInteractiveAsync(
                "pwsh", ["-NoProfile", "-File", scriptPath], Path.GetTempPath());
            await process.WriteStandardInputAsync(prompt);
            var lines = new List<string>();
            await foreach (string line in process.ReadOutputLinesAsync()) lines.Add(line);

            Assert.Equal(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(prompt)).ToLowerInvariant(),
                Assert.Single(lines));
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (process is not null) await process.DisposeAsync();
            File.Delete(scriptPath);
        }
    }

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

    [Fact]
    public async Task StartInteractiveAsync_RetainsStderrTailAndExitCode_WhenChildFails()
    {
        // Regression for the SILENT one-shot failure: codex refusing to run (stderr message, exit 1,
        // nothing on stdout) used to leave no trace — the drain discarded stderr and nobody consulted
        // the exit code. By the time the stdout line stream completes, the exit code and a bounded
        // stderr tail must both be observable on the IAgentProcess.
        string dir = Path.GetTempPath();
        string scriptPath;
        string file;
        string[] args;

        if (OperatingSystem.IsWindows())
        {
            scriptPath = Path.Combine(dir, $"fail-{Guid.NewGuid():N}.bat");
            await File.WriteAllTextAsync(
                scriptPath,
                "@echo off\r\necho Not inside a trusted directory marker 1>&2\r\nexit /b 7\r\n");
            file = "cmd.exe";
            args = ["/c", scriptPath];
        }
        else
        {
            scriptPath = Path.Combine(dir, $"fail-{Guid.NewGuid():N}.sh");
            await File.WriteAllTextAsync(
                scriptPath,
                "echo 'Not inside a trusted directory marker' 1>&2\nexit 7\n");
            file = "/bin/sh";
            args = [scriptPath];
        }

        IAgentProcess? process = null;
        try
        {
            var runner = new ProcessRunner();
            process = await runner.StartInteractiveAsync(file, args, dir);

            await foreach (string _ in process.ReadOutputLinesAsync())
            {
            }

            // Stream end must not race the exit-state capture: the exit code is already observable here.
            Assert.Equal(7, process.ExitCode);
            Assert.Contains("Not inside a trusted directory marker", process.ErrorSnapshot);
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
                // Best-effort cleanup of the temp script.
            }
        }
    }

    [Fact]
    public async Task DisposeAsync_WaitsForChildFileLocksToBeReleased()
    {
        if (!OperatingSystem.IsWindows()) return;

        string directory = Path.GetTempPath();
        string scriptPath = Path.Combine(directory, $"hold-lock-{Guid.NewGuid():N}.ps1");
        string lockPath = Path.Combine(directory, $"held-{Guid.NewGuid():N}.lock");
        await File.WriteAllTextAsync(scriptPath, """
            param([string]$Path)
            $stream = [System.IO.File]::Open(
                $Path,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
            try {
                [Console]::Out.WriteLine('LOCKED')
                [Console]::Out.Flush()
                [Console]::In.ReadLine() | Out-Null
            }
            finally {
                $stream.Dispose()
            }
            """);

        IAgentProcess? process = null;
        try
        {
            process = await new ProcessRunner().StartInteractiveAsync(
                "pwsh",
                ["-NoProfile", "-File", scriptPath, lockPath],
                directory);

            await foreach (string line in process.ReadOutputLinesAsync())
            {
                Assert.Equal("LOCKED", line);
                break;
            }

            Assert.Throws<IOException>(() => File.Open(
                lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None));

            await process.DisposeAsync();
            process = null;

            using FileStream reopened = File.Open(
                lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        finally
        {
            if (process is not null)
            {
                await process.DisposeAsync();
            }

            File.Delete(scriptPath);
            File.Delete(lockPath);
        }
    }
}
