using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Infrastructure.Models.Trust;
using LoopRelay.Infrastructure.Primitives.Trust;

namespace LoopRelay.Infrastructure.Tests.Services;

public sealed class TrustPolicyTests
{
    [Fact]
    public void DangerFullAccessMapsToFullWorkspaceAndNetworkAuthority()
    {
        var policy = TrustPolicy.FromSandboxProfile(
            new SandboxProfile("danger-full-access", true, true, false));

        Assert.Equal(WorkspaceAuthority.FullAccess, policy.Workspace);
        Assert.Equal(NetworkAuthority.Allowed, policy.Network);
        Assert.Equal(ApprovalAuthority.Never, policy.Approval);
        Assert.Equal("danger-full-access", policy.ToEvidence().Sandbox);
    }

    [Fact]
    public void ReadOnlyMapsToDeniedNetworkAndReadOnlyWorkspace()
    {
        var policy = TrustPolicy.FromSandboxProfile(
            new SandboxProfile("read-only", false, false, false),
            ExecutionAuthority.OneShot);

        Assert.Equal(WorkspaceAuthority.ReadOnly, policy.Workspace);
        Assert.Equal(NetworkAuthority.Denied, policy.Network);
        Assert.Equal("OneShot", policy.ToEvidence().Execution);
    }
}
