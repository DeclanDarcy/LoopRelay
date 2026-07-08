using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Tests.Services.Sandboxing;

/// <summary>
/// The default sandbox workspace factory (Stage 2). It must give each transfer a fresh, real, empty directory
/// (so codex <c>--cd</c> can scope its sandbox to it) and clean the directory up on disposal (so transfers do not
/// leak temp trees). These are real-filesystem tests — the whole point of the seam is a real isolated cwd.
/// </summary>
public sealed class TempSandboxWorkspaceFactoryTests
{
    private static readonly TempSandboxWorkspaceFactory Factory = new();

    [Fact]
    public async Task Creates_a_fresh_empty_directory_that_exists()
    {
        await using ISandboxWorkspace workspace = await Factory.CreateAsync("test");

        Assert.True(Directory.Exists(workspace.RootPath));
        Assert.Empty(Directory.GetFileSystemEntries(workspace.RootPath));
    }

    [Fact]
    public async Task Resolves_a_repository_relative_path_to_an_absolute_location_under_the_root()
    {
        await using ISandboxWorkspace workspace = await Factory.CreateAsync("test");

        string resolved = workspace.Resolve(".agents/operational_context.md");

        Assert.True(Path.IsPathFullyQualified(resolved));
        Assert.StartsWith(
            Path.GetFullPath(workspace.RootPath) + Path.DirectorySeparatorChar,
            resolved,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("operational_context.md", resolved);
    }

    [Fact]
    public async Task Two_workspaces_get_distinct_roots()
    {
        await using ISandboxWorkspace a = await Factory.CreateAsync("test");
        await using ISandboxWorkspace b = await Factory.CreateAsync("test");

        Assert.NotEqual(a.RootPath, b.RootPath);
    }

    [Fact]
    public async Task Disposal_removes_the_workspace_directory_and_its_contents()
    {
        ISandboxWorkspace workspace = await Factory.CreateAsync("test");
        string root = workspace.RootPath;
        await File.WriteAllTextAsync(workspace.Resolve("marker.txt"), "x");
        Directory.CreateDirectory(Path.GetDirectoryName(workspace.Resolve(".agents/operational_context.md"))!);
        await File.WriteAllTextAsync(workspace.Resolve(".agents/operational_context.md"), "ctx");

        await workspace.DisposeAsync();

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Disposal_is_idempotent_and_tolerates_an_already_removed_directory()
    {
        ISandboxWorkspace workspace = await Factory.CreateAsync("test");
        Directory.Delete(workspace.RootPath, recursive: true);

        // A second removal (or a vanished directory) must NOT throw — a cleanup failure can never fail a transfer.
        await workspace.DisposeAsync();
    }
}
