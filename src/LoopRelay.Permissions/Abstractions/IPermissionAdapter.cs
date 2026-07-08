using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Abstractions;

public interface IPermissionAdapter
{
    PermissionRequest Parse(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory);

    byte[] BuildResponse(PermissionRequest request, PermissionResult result);
}
