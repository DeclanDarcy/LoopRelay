using LoopRelay.Permissions.Primitives.Requests;

namespace LoopRelay.Permissions.Abstractions.Evaluation;

public interface IPermissionAdapter
{
    PermissionRequest Parse(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory);

    byte[] BuildResponse(PermissionRequest request, PermissionResult result);
}
