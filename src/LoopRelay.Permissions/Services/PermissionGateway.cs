using LoopRelay.Permissions.Abstractions;

namespace LoopRelay.Permissions.Services;

public sealed class PermissionGateway(IPermissionAdapter adapter, IPermissionHandler handler) : IPermissionGateway
{
    public byte[] Evaluate(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory)
    {
        var request = adapter.Parse(payload, repoIdentity, workingDirectory);
        var result = handler.Evaluate(request);
        return adapter.BuildResponse(request, result);
    }
}
