using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Reasoning.Tests;

public sealed class ReasoningReferenceFactoryTests
{
    [Fact]
    public void CreatesStableSourceDomainReferences()
    {
        ReasoningReference decision = ReasoningReferenceFactory.Decision("DEC-0001", "Use reasoning events", "decision-fingerprint");
        ReasoningReference proposal = ReasoningReferenceFactory.Proposal("PROP-0001", "Resolve capture design", "proposal-fingerprint");
        ReasoningReference candidate = ReasoningReferenceFactory.Candidate("CAND-0001", "Capture design", "candidate-fingerprint");
        ReasoningReference governance = ReasoningReferenceFactory.GovernanceFinding(
            "GOV-0001",
            "governance.202606230000000000000",
            "Conflicting resolved decisions",
            "Resolved decisions conflict.",
            "finding-fingerprint");
        ReasoningReference operationalContext = ReasoningReferenceFactory.OperationalContextProposal(
            "oc-proposal-1",
            "Semantic change: ConstraintAdded",
            "Human review remains required.",
            "oc-fingerprint");
        ReasoningReference handoff = ReasoningReferenceFactory.Handoff(
            ".agents/handoffs/handoff.md",
            "Direction shifted.",
            "handoff-fingerprint");
        ReasoningReference executionOutput = ReasoningReferenceFactory.ExecutionOutput(
            "00000000-0000-0000-0000-000000000001",
            "Milestone: .agents/milestones/m2-cross-artifact-capture.md",
            "execution-fingerprint");
        ReasoningReference artifact = ReasoningReferenceFactory.Artifact(
            ".agents/plan.md",
            "Milestone 2",
            "Add reference helpers.",
            "artifact-fingerprint");

        Assert.Equal((ReasoningReferenceKind.Decision, "DEC-0001", ".agents/decisions/records/DEC-0001/decision.json"), Key(decision));
        Assert.Equal((ReasoningReferenceKind.Proposal, "PROP-0001", ".agents/decisions/proposals/PROP-0001/proposal.json"), Key(proposal));
        Assert.Equal((ReasoningReferenceKind.Candidate, "CAND-0001", ".agents/decisions/candidates/CAND-0001/candidate.json"), Key(candidate));
        Assert.Equal((ReasoningReferenceKind.GovernanceFinding, "GOV-0001", ".agents/decisions/governance/governance.202606230000000000000.json"), Key(governance));
        Assert.Equal((ReasoningReferenceKind.OperationalContextRevision, "oc-proposal-1", ".agents/operational_context/proposals/oc-proposal-1/metadata.json"), Key(operationalContext));
        Assert.Equal((ReasoningReferenceKind.Handoff, ".agents/handoffs/handoff.md", ".agents/handoffs/handoff.md"), Key(handoff));
        Assert.Equal((ReasoningReferenceKind.ExecutionOutput, "00000000-0000-0000-0000-000000000001", null), Key(executionOutput));
        Assert.Equal((ReasoningReferenceKind.Artifact, ".agents/plan.md", ".agents/plan.md"), Key(artifact));
    }

    [Theory]
    [InlineData("../plan.md")]
    [InlineData("/absolute/path.md")]
    [InlineData("C:\\repo\\file.md")]
    public void RejectsUnsafeReferencePaths(string unsafePath)
    {
        Assert.Throws<ArgumentException>(() => ReasoningReferenceFactory.Artifact(unsafePath));
    }

    [Theory]
    [InlineData("../DEC-0001")]
    [InlineData("/DEC-0001")]
    [InlineData("DEC\\0001")]
    public void RejectsUnsafeReferenceIds(string unsafeId)
    {
        Assert.Throws<ArgumentException>(() => ReasoningReferenceFactory.Decision(unsafeId));
    }

    private static (ReasoningReferenceKind Kind, string Id, string? RelativePath) Key(ReasoningReference reference)
    {
        return (reference.Kind, reference.Id, reference.RelativePath);
    }
}
