using LoopRelay.Permissions.Primitives.Parsing;

namespace LoopRelay.Permissions.Abstractions.Security;

public interface IFingerprintService
{
    string Compute(
        string toolName,
        string repoIdentity,
        string workingDirectory,
        CanonicalCommand[] commands);
}
