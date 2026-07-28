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
            // Staged (tracked), not left untracked: routine observation (Task 3.7) no longer scans
            // untracked files, so this "dirty" fixture must exercise a tracked change to still be dirty.
            RunGit(root, "add", ".agents/state.md");
            RepositoryObservation dirty = await new RepositoryObserver().ObserveAsync(root);
            Assert.True(dirty.GitFacts.HasWorkingTreeChanges);
            Assert.Equal("ordinary-directory", dirty.GitFacts.AgentsTopology);

            RunGit(root, "commit", "-m", "agents");
            RunGit(root, "checkout", "--detach", "HEAD");
            RepositoryObservation detached = await new RepositoryObserver().ObserveAsync(root);
            Assert.True(detached.GitFacts.IsDetached);
            Assert.Equal("detached", detached.GitFacts.CurrentBranch);
        }
        finally
        {
            DeleteGitFixture(root);
        }
    }

    /// <summary>
    /// Task 3.7: routine observation was rebounded to tracked changes
    /// (<c>--untracked-files=no</c>), so an untracked-only file must no longer flip
    /// <see cref="ObservedGitFacts.HasWorkingTreeChanges"/> to <c>true</c> the way the pre-change
    /// algorithm (<see cref="LegacyGitStatus"/>, still run here for comparison) would have.
    /// </summary>
    [Fact]
    public async Task UntrackedOnlyChangesNoLongerMarkTheWorkingTreeDirty()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-git-observation-untracked-").FullName;
        try
        {
            RunGit(root, "init");
            RunGit(root, "config", "user.email", "certification@example.invalid");
            RunGit(root, "config", "user.name", "Loop Relay Certification");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "initial\n");
            RunGit(root, "add", "README.md");
            RunGit(root, "commit", "-m", "initial");

            // Untracked-only change: a new file that has never been `git add`ed.
            await File.WriteAllTextAsync(Path.Combine(root, "scratch.txt"), "untracked\n");

            RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(root);
            (bool Dirty, string Branch) legacy = LegacyGitStatus(root);

            Assert.False(observation.GitFacts.HasWorkingTreeChanges);
            Assert.True(legacy.Dirty);
            Assert.Equal(legacy.Branch, observation.GitFacts.CurrentBranch);
        }
        finally
        {
            DeleteGitFixture(root);
        }
    }

    /// <summary>
    /// Characterization guard for Task 3.7: for changes to tracked files, branch and dirty semantics
    /// must be identical between the pre-change algorithm (<see cref="LegacyGitStatus"/>,
    /// <c>--untracked-files=normal</c>) and the bounded observation now in production
    /// (<c>--untracked-files=no</c>) — only untracked-only changes are meant to differ.
    /// </summary>
    [Theory]
    [InlineData("modify-tracked-file")]
    [InlineData("stage-new-tracked-file")]
    public async Task TrackedFileChangesAgreeBetweenLegacyAndBoundedObservation(string scenario)
    {
        string root = Directory.CreateTempSubdirectory("looprelay-git-observation-tracked-").FullName;
        try
        {
            RunGit(root, "init");
            RunGit(root, "config", "user.email", "certification@example.invalid");
            RunGit(root, "config", "user.name", "Loop Relay Certification");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "initial\n");
            RunGit(root, "add", "README.md");
            RunGit(root, "commit", "-m", "initial");

            switch (scenario)
            {
                case "modify-tracked-file":
                    await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "changed\n");
                    break;
                case "stage-new-tracked-file":
                    await File.WriteAllTextAsync(Path.Combine(root, "added.txt"), "added\n");
                    RunGit(root, "add", "added.txt");
                    break;
            }

            RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(root);
            (bool Dirty, string Branch) legacy = LegacyGitStatus(root);

            Assert.True(observation.GitFacts.HasWorkingTreeChanges);
            Assert.Equal(legacy.Dirty, observation.GitFacts.HasWorkingTreeChanges);
            Assert.Equal(legacy.Branch, observation.GitFacts.CurrentBranch);
        }
        finally
        {
            DeleteGitFixture(root);
        }
    }

    /// <summary>Companion to the tracked-change guard above: a clean tree must agree too.</summary>
    [Fact]
    public async Task CleanRepositoryAgreesBetweenLegacyAndBoundedObservation()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-git-observation-clean-").FullName;
        try
        {
            RunGit(root, "init");
            RunGit(root, "config", "user.email", "certification@example.invalid");
            RunGit(root, "config", "user.name", "Loop Relay Certification");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "initial\n");
            RunGit(root, "add", "README.md");
            RunGit(root, "commit", "-m", "initial");

            RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(root);
            (bool Dirty, string Branch) legacy = LegacyGitStatus(root);

            Assert.False(observation.GitFacts.HasWorkingTreeChanges);
            Assert.Equal(legacy.Dirty, observation.GitFacts.HasWorkingTreeChanges);
            Assert.Equal(legacy.Branch, observation.GitFacts.CurrentBranch);
        }
        finally
        {
            DeleteGitFixture(root);
        }
    }

    /// <summary>
    /// The pre-Task-3.7 algorithm, verbatim: <c>git status --porcelain=v1 --branch --untracked-files=normal</c>
    /// (untracked files walked and included in the dirty check), parsed exactly the way
    /// <c>RepositoryObserver.ObserveGit</c> parsed it before this task bounded routine observation to
    /// tracked changes. Kept only to characterize the behavior difference for untracked-only changes;
    /// never exercised in production.
    /// </summary>
    private static (bool Dirty, string Branch) LegacyGitStatus(string root)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("status");
        startInfo.ArgumentList.Add("--porcelain=v1");
        startInfo.ArgumentList.Add("--branch");
        startInfo.ArgumentList.Add("--untracked-files=normal");
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("git did not start");
        string output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        if (!process.WaitForExit(5000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("legacy git status did not exit within 5 seconds.");
        }

        string[] lines = output.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string heading = lines.FirstOrDefault(line => line.StartsWith("## ", StringComparison.Ordinal)) ?? "## unknown";
        bool detached = heading.Contains("HEAD (no branch)", StringComparison.Ordinal) ||
            heading.StartsWith("## HEAD (detached", StringComparison.Ordinal);
        string branch = detached
            ? "detached"
            : heading[3..].Split(new[] { "...", " " }, StringSplitOptions.RemoveEmptyEntries)[0];
        bool dirty = lines.Any(line => !line.StartsWith("## ", StringComparison.Ordinal));
        return (dirty, branch);
    }

    /// <summary>
    /// Git leaves objects under <c>.git</c> read-only on Windows; a plain recursive delete throws
    /// <see cref="UnauthorizedAccessException"/> unless attributes are cleared first.
    /// </summary>
    private static void DeleteGitFixture(string root)
    {
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
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

        // Bounded: a wedged git (credential prompt, locked index, hung filter) must fail this test rather
        // than hang the whole run forever. Two minutes is far above any legitimate fixture git command.
        if (!process.WaitForExit(120_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // It exited on its own between the timeout and the kill; the timeout is still the failure.
            }

            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} did not exit within 120 seconds.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(process.StandardError.ReadToEnd());
        }
    }
}
