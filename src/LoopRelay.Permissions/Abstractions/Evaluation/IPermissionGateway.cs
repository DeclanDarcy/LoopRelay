using LoopRelay.Permissions.Models.Evaluation;

namespace LoopRelay.Permissions.Abstractions.Evaluation;

public interface IPermissionGateway
{
    byte[] Evaluate(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory);

    byte[] Evaluate(ReadOnlySpan<byte> payload, PermissionGatewayContext context) =>
        Evaluate(payload, context.RepoIdentity, context.WorkingDirectory);
}
