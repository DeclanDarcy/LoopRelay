using LoopRelay.Orchestration.Abstractions;

namespace LoopRelay.Orchestration.Services;

/// <summary>
/// Default <see cref="ISandboxWorkspaceFactory"/>: each workspace is a fresh directory under the OS temp path.
/// Disposal removes it recursively, tolerating a locked/absent directory (a cleanup failure must never fail the
/// transfer). Uses the same <c>Path.GetTempPath()/LoopRelay.*/{guid}</c> convention as the rest of the code.
/// </summary>
public sealed class TempSandboxWorkspaceFactory : ISandboxWorkspaceFactory
{
    public Task<ISandboxWorkspace> CreateAsync(string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string root = Path.Combine(Path.GetTempPath(), "LoopRelay.Sandbox", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Task.FromResult<ISandboxWorkspace>(new TempSandboxWorkspace(root));
    }

    private sealed class TempSandboxWorkspace(string rootPath) : ISandboxWorkspace
    {
        public string RootPath => rootPath;

        public ValueTask DisposeAsync()
        {
            try
            {
                Directory.Delete(rootPath, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
