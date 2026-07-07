using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Services;

public sealed class PermissionGateway(
    IPermissionAdapter adapter,
    IPermissionHandler handler,
    OperationPermissionHandler? operationHandler = null) : IPermissionGateway
{
    public byte[] Evaluate(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory)
    {
        return Evaluate(payload, new PermissionGatewayContext(repoIdentity, workingDirectory));
    }

    public byte[] Evaluate(ReadOnlySpan<byte> payload, PermissionGatewayContext context)
    {
        var request = adapter.Parse(payload, context.RepoIdentity, context.WorkingDirectory);
        var result = context.OperationScope is null
            ? handler.Evaluate(request)
            : (operationHandler ?? new OperationPermissionHandler()).Evaluate(request, context.OperationScope);
        return adapter.BuildResponse(request, result);
    }
}
