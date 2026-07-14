using System.Diagnostics;
using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Orchestration.Tests.Resolution;

public sealed class GitObservationTests
{
    [Fact]
    public async Task ObserverReportsCleanDirtyDetachedAndAgentsTopologyFromRealGit()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-git-observation-").FullName;
        try
        {
            RunGit(root, "init");
            RunGit(root, "config", "user.email", "certification@example.invalid");
            RunGit(root, "config", "user.name", "Loop Relay Certification");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "initial\n");
            RunGit(root, "add", "README.md");
            RunGit(root, "commit", "-m", "initial");
            RepositoryObservation clean = await new RepositoryObserver().ObserveAsync(root);

            Assert.True(clean.GitFacts.IsRepository);
            Assert.False(clean.GitFacts.HasWorkingTreeChanges);
            Assert.False(clean.GitFacts.IsDetached);

            Directory.CreateDirectory(Path.Combine(root, ".agents"));
            await File.WriteAllTextAsync(Path.Combine(root, ".agents", "state.md"), "dirty\n");
            RepositoryObservation dirty = await new RepositoryObserver().ObserveAsync(root);
            Assert.True(dirty.GitFacts.HasWorkingTreeChanges);
            Assert.Equal("ordinary-directory", dirty.GitFacts.AgentsTopology);

            RunGit(root, "add", ".agents/state.md");
            RunGit(root, "commit", "-m", "agents");
            RunGit(root, "checkout", "--detach", "HEAD");
            RepositoryObservation detached = await new RepositoryObserver().ObserveAsync(root);
            Assert.True(detached.GitFacts.IsDetached);
            Assert.Equal("detached", detached.GitFacts.CurrentBranch);
        }
        finally
        {
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    private static void RunGit(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(process.StandardError.ReadToEnd());
        }
    }
}
