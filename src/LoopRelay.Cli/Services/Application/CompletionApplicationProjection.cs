using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Cli.Services.Application;

internal sealed class CompletionApplicationProjection(Repository repository)
{
    private readonly ICompletionAuthorityProjection _projection = new CompletionAuthorityProjection(
        new CanonicalCompletionAuthorityStore(repository));

    public Task<CompletionAuthorityProjectionSnapshot> ProjectAsync(
        CancellationToken cancellationToken = default) =>
        _projection.ProjectAsync(cancellationToken);
}
