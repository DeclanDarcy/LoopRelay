using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.Cli;
using LoopRelay.Roadmap.Cli.Services.Persistence;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Cli;

public sealed class CliArgumentsTests
{
    [Fact]
    public void TryParse_requires_repo_dir()
    {
        Assert.False(CliArguments.TryParse([], out RoadmapCliInvocation _, out string error));
        Assert.Contains("REPO_DIR", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_existing_repo_dir()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse([repo.Root], out RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(RoadmapCliCommand.Run, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
        Assert.Equal("workspace-write", parsed.ExecutionOptions.SandboxIdentifier);
        Assert.False(parsed.ExecutionOptions.AllowNetwork);
        Assert.True(parsed.ExecutionOptions.RequiresApproval);
    }

    [Theory]
    [InlineData("status", (int)RoadmapCliCommand.Status)]
    [InlineData("run", (int)RoadmapCliCommand.Run)]
    [InlineData("unblock", (int)RoadmapCliCommand.Unblock)]
    [InlineData("storage-init", (int)RoadmapCliCommand.StorageInit)]
    [InlineData("storage-import", (int)RoadmapCliCommand.StorageImport)]
    [InlineData("storage-export", (int)RoadmapCliCommand.StorageExport)]
    [InlineData("storage-sync", (int)RoadmapCliCommand.StorageSync)]
    [InlineData("storage-verify", (int)RoadmapCliCommand.StorageVerify)]
    public void TryParse_accepts_leading_command(string command, int expectedValue)
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse([command, repo.Root], out RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal((RoadmapCliCommand)expectedValue, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
    }

    [Fact]
    public void TryParse_accepts_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse([repo.Root, "unblock"], out RoadmapCliInvocation parsed, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(RoadmapCliCommand.Unblock, parsed.Command);
        Assert.Equal(Path.GetFullPath(repo.Root), parsed.Repository.Path);
    }

    [Fact]
    public void TryParse_rejects_unsupported_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.False(CliArguments.TryParse([repo.Root, "repair"], out RoadmapCliInvocation _, out string error));

        Assert.Contains("Unsupported roadmap command", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_elevated_execution_reason_after_leading_command()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse(
            ["run", repo.Root, "--elevated", "Needs package registry"],
            out RoadmapCliInvocation parsed,
            out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal("danger-full-access", parsed.ExecutionOptions.SandboxIdentifier);
        Assert.True(parsed.ExecutionOptions.AllowNetwork);
        Assert.Equal("Needs package registry", parsed.ExecutionOptions.ElevatedReason);
    }

    [Fact]
    public void TryParse_accepts_elevated_execution_reason_after_trailing_command()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse(
            [repo.Root, "run", "--elevated", "Needs package registry"],
            out RoadmapCliInvocation parsed,
            out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal("danger-full-access", parsed.ExecutionOptions.SandboxIdentifier);
        Assert.True(parsed.ExecutionOptions.AllowNetwork);
    }

    [Fact]
    public void TryParse_rejects_elevated_execution_without_reason()
    {
        using var repo = new TempRepo();

        Assert.False(CliArguments.TryParse([repo.Root, "--elevated"], out RoadmapCliInvocation _, out string error));

        Assert.Contains("requires a non-empty reason", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_storage_domain_and_force_flags()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse(
            ["storage-sync", repo.Root, "--domain", "core,execution-evidence", "--force-import"],
            out RoadmapCliInvocation parsed,
            out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(RoadmapCliCommand.StorageSync, parsed.Command);
        Assert.Contains(WorkspaceSyncDomain.Core, parsed.StorageOptions!.Domains!);
        Assert.Contains(WorkspaceSyncDomain.ExecutionEvidence, parsed.StorageOptions.Domains!);
        Assert.True(parsed.StorageOptions.ForceImport);
        Assert.False(parsed.StorageOptions.ForceExport);
    }

    [Fact]
    public void TryParse_rejects_unknown_storage_domain()
    {
        using var repo = new TempRepo();

        Assert.False(CliArguments.TryParse(
            ["storage-export", repo.Root, "--domain", "unknown"],
            out RoadmapCliInvocation _,
            out string error));

        Assert.Contains("Unsupported storage domain", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_storage_verify_full_roundtrip()
    {
        using var repo = new TempRepo();

        Assert.True(CliArguments.TryParse(
            ["storage-verify", repo.Root, "--full-roundtrip"],
            out RoadmapCliInvocation parsed,
            out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(RoadmapCliCommand.StorageVerify, parsed.Command);
        Assert.True(parsed.StorageOptions!.FullRoundtrip);
    }
}
