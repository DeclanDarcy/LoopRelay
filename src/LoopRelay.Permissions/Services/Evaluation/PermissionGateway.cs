using LoopRelay.Permissions.Abstractions.Evaluation;
using LoopRelay.Permissions.Models.Evaluation;

namespace LoopRelay.Permissions.Services.Evaluation;

public sealed class PermissionGateway(
    IPermissionAdapter _adapter,
    IPermissionHandler _handler,
    OperationPermissionHandler? _operationHandler = null) : IPermissionGateway
{
    public byte[] Evaluate(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory)
    {
        return Evaluate(payload, new PermissionGatewayContext(repoIdentity, workingDirectory));
    }

    public byte[] Evaluate(ReadOnlySpan<byte> payload, PermissionGatewayContext context)
    {
        var request = _adapter.Parse(payload, context.RepoIdentity, context.WorkingDirectory);
        var result = context.OperationScope is null
            ? _handler.Evaluate(request)
            : (_operationHandler ?? new OperationPermissionHandler()).Evaluate(request, context.OperationScope);
        return _adapter.BuildResponse(request, result);
    }
}
