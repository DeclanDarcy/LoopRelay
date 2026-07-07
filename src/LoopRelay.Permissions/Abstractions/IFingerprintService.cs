using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Abstractions;

public interface IFingerprintService
{
    string Compute(
        string toolName,
        string repoIdentity,
        string workingDirectory,
        CanonicalCommand[] commands);
}
