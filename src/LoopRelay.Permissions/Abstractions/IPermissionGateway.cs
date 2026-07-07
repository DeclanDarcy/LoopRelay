namespace LoopRelay.Permissions.Abstractions;

public interface IPermissionGateway
{
    byte[] Evaluate(ReadOnlySpan<byte> payload, string repoIdentity, string workingDirectory);
}
