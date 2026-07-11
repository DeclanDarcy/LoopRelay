using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Sessions;

namespace LoopRelay.Agents.Tests.Services;

/// <summary>
/// M7/D5: capability gaps are typed outcomes, never silent fallbacks. Codex declares every
/// capability, so these gaps are only reachable through a future provider — the tests pin the
/// contract a second provider would negotiate against.
/// </summary>
public sealed class AgentCapabilityNegotiationTests
{
    private static readonly AgentRuntimeCapabilities FullCapabilities =
        new("codex", OneShotExecution: true, PersistentSessions: true, SessionResume: true);

    [Fact]
    public void Fully_capable_provider_passes_open_and_one_shot_negotiation()
    {
        AgentCapabilityNegotiation.EnsureCanOpenSession(FullCapabilities, Spec(resumeThreadId: "thread-1"));
        AgentCapabilityNegotiation.EnsureCanRunOneShot(FullCapabilities);
    }

    [Fact]
    public void Resume_request_against_a_provider_without_resume_is_a_typed_capability_gap()
    {
        AgentRuntimeCapabilities noResume = FullCapabilities with { SessionResume = false };

        AgentCapabilityException exception = Assert.Throws<AgentCapabilityException>(
            () => AgentCapabilityNegotiation.EnsureCanOpenSession(noResume, Spec(resumeThreadId: "thread-1")));

        Assert.Equal(nameof(AgentRuntimeCapabilities.SessionResume), exception.Capability);
        // The M2 vocabulary rule: every cannot-proceed condition names the actual failure.
        Assert.StartsWith("MissingRuntimeCapability:", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Blocked", exception.Message, StringComparison.Ordinal);

        // A fresh (non-resuming) open on the same provider still negotiates cleanly.
        AgentCapabilityNegotiation.EnsureCanOpenSession(noResume, Spec(resumeThreadId: null));
    }

    [Fact]
    public void Missing_persistent_or_one_shot_capability_is_a_typed_capability_gap()
    {
        AgentCapabilityException persistent = Assert.Throws<AgentCapabilityException>(
            () => AgentCapabilityNegotiation.EnsureCanOpenSession(
                FullCapabilities with { PersistentSessions = false },
                Spec(resumeThreadId: null)));
        Assert.Equal(nameof(AgentRuntimeCapabilities.PersistentSessions), persistent.Capability);

        AgentCapabilityException oneShot = Assert.Throws<AgentCapabilityException>(
            () => AgentCapabilityNegotiation.EnsureCanRunOneShot(
                FullCapabilities with { OneShotExecution = false }));
        Assert.Equal(nameof(AgentRuntimeCapabilities.OneShotExecution), oneShot.Capability);
    }

    [Fact]
    public void One_shot_transport_normalization_is_idempotent_and_appends_exactly_one_newline()
    {
        Assert.Equal("prompt\n", AgentPromptTransport.EnsureTrailingNewline("prompt"));
        Assert.Equal("prompt\n", AgentPromptTransport.EnsureTrailingNewline("prompt\n"));
        Assert.Equal(
            AgentPromptTransport.EnsureTrailingNewline("prompt"),
            AgentPromptTransport.EnsureTrailingNewline(AgentPromptTransport.EnsureTrailingNewline("prompt")));
    }

    private static AgentSessionSpec Spec(string? resumeThreadId) =>
        new(
            SessionIdentity.New(),
            "repo",
            SessionRole.Decision,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            workingDirectory: null,
            resumeThreadId: resumeThreadId);
}
