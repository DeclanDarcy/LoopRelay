using LoopRelay.Orchestration.Abstractions;

namespace LoopRelay.Cli.Tests.Services;


/// <summary>
/// In-memory sandbox workspace factory (Stage 2). Records created/disposed roots. A test not about isolation can
/// set Root = repository.Path so the sandbox becomes transparent (the in-place rewrite effect resolves to the
/// repo path) and existing repo-writing transfer scripts keep working unchanged.
/// </summary>
internal sealed class FakeSandboxWorkspaceFactory : ISandboxWorkspaceFactory
{
    public string Root { get; init; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cc-cli-fake-sandbox", Guid.NewGuid().ToString("N"));

    public List<string> Created { get; } = new();

    public List<string> Disposed { get; } = new();

    public int CreatedCount => Created.Count;

    public string Resolve(string relativePath) =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, relativePath));

    public Task<ISandboxWorkspace> CreateAsync(string label, CancellationToken cancellationToken = default)
    {
        Created.Add(Root);
        return Task.FromResult<ISandboxWorkspace>(new FakeSandboxWorkspace(this));
    }

    private sealed class FakeSandboxWorkspace(FakeSandboxWorkspaceFactory owner) : ISandboxWorkspace
    {
        public string RootPath => owner.Root;

        public ValueTask DisposeAsync()
        {
            owner.Disposed.Add(owner.Root);
            return ValueTask.CompletedTask;
        }
    }
}
