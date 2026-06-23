using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Services;

public static class ReasoningReferenceFactory
{
    public static ReasoningReference Decision(
        string decisionId,
        string? title = null,
        string? fingerprint = null,
        string section = "Decision Record")
    {
        return Create(ReasoningReferenceKind.Decision, decisionId, DecisionPath(decisionId), section, title, fingerprint);
    }

    public static ReasoningReference Proposal(
        string proposalId,
        string? title = null,
        string? fingerprint = null,
        string section = "Proposal")
    {
        return Create(ReasoningReferenceKind.Proposal, proposalId, ProposalPath(proposalId), section, title, fingerprint);
    }

    public static ReasoningReference Candidate(
        string candidateId,
        string? title = null,
        string? fingerprint = null,
        string section = "Candidate")
    {
        return Create(ReasoningReferenceKind.Candidate, candidateId, CandidatePath(candidateId), section, title, fingerprint);
    }

    public static ReasoningReference GovernanceFinding(
        string findingId,
        string reportId,
        string? title = null,
        string? excerpt = null,
        string? fingerprint = null)
    {
        string section = string.IsNullOrWhiteSpace(title)
            ? $"Finding: {findingId}"
            : $"Finding: {title.Trim()}";
        return Create(ReasoningReferenceKind.GovernanceFinding, findingId, GovernanceReportPath(reportId), section, excerpt, fingerprint);
    }

    public static ReasoningReference OperationalContextRevision(
        string revisionId,
        string relativePath,
        string section,
        string? excerpt = null,
        string? fingerprint = null)
    {
        return Create(ReasoningReferenceKind.OperationalContextRevision, revisionId, relativePath, section, excerpt, fingerprint);
    }

    public static ReasoningReference OperationalContextProposal(
        string proposalId,
        string section,
        string? excerpt = null,
        string? fingerprint = null)
    {
        return OperationalContextRevision(
            proposalId,
            OperationalContextProposalPath(proposalId),
            section,
            excerpt,
            fingerprint);
    }

    public static ReasoningReference Handoff(
        string relativePath,
        string? excerpt = null,
        string? fingerprint = null,
        string section = "Handoff")
    {
        string normalizedPath = RequireRelativePath(relativePath, nameof(relativePath));
        return Create(ReasoningReferenceKind.Handoff, normalizedPath, normalizedPath, section, excerpt, fingerprint);
    }

    public static ReasoningReference ExecutionOutput(
        string executionOutputId,
        string? excerpt = null,
        string? fingerprint = null,
        string section = "Execution Output",
        string? relativePath = null)
    {
        return Create(ReasoningReferenceKind.ExecutionOutput, executionOutputId, relativePath, section, excerpt, fingerprint);
    }

    public static ReasoningReference ExecutionProjection(
        string executionProjectionId,
        string relativePath,
        string? excerpt = null,
        string? fingerprint = null,
        string section = "Execution Projection")
    {
        return Create(ReasoningReferenceKind.ExecutionProjection, executionProjectionId, relativePath, section, excerpt, fingerprint);
    }

    public static ReasoningReference Artifact(
        string relativePath,
        string? section = null,
        string? excerpt = null,
        string? fingerprint = null,
        string? id = null)
    {
        string normalizedPath = RequireRelativePath(relativePath, nameof(relativePath));
        return Create(
            ReasoningReferenceKind.Artifact,
            string.IsNullOrWhiteSpace(id) ? normalizedPath : id,
            normalizedPath,
            section,
            excerpt,
            fingerprint);
    }

    public static string DecisionPath(string decisionId)
    {
        return $".agents/decisions/records/{RequireId(decisionId, nameof(decisionId))}/decision.json";
    }

    public static string ProposalPath(string proposalId)
    {
        return $".agents/decisions/proposals/{RequireId(proposalId, nameof(proposalId))}/proposal.json";
    }

    public static string CandidatePath(string candidateId)
    {
        return $".agents/decisions/candidates/{RequireId(candidateId, nameof(candidateId))}/candidate.json";
    }

    public static string GovernanceReportPath(string reportId)
    {
        return $".agents/decisions/governance/{RequireId(reportId, nameof(reportId))}.json";
    }

    public static string OperationalContextProposalPath(string proposalId)
    {
        return $".agents/operational_context/proposals/{RequireId(proposalId, nameof(proposalId))}/metadata.json";
    }

    private static ReasoningReference Create(
        ReasoningReferenceKind kind,
        string id,
        string? relativePath,
        string? section,
        string? excerpt,
        string? fingerprint)
    {
        return new ReasoningReference(
            kind,
            RequireId(id, nameof(id)),
            NormalizeRelativePath(relativePath),
            NormalizeOptional(section),
            NormalizeOptional(excerpt),
            NormalizeOptional(fingerprint));
    }

    private static string RequireId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Reasoning reference id is required.", parameterName);
        }

        string normalized = value.Trim();
        if (normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.Contains("\\", StringComparison.Ordinal) ||
            normalized.Contains(":", StringComparison.Ordinal) ||
            normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Reasoning reference id is not safe: {value}", parameterName);
        }

        return normalized;
    }

    private static string RequireRelativePath(string value, string parameterName)
    {
        return NormalizeRelativePath(value) ?? throw new ArgumentException("Reasoning reference relative path is required.", parameterName);
    }

    private static string? NormalizeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Contains(":", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Reasoning reference path is not safe: {value}", nameof(value));
        }

        string normalized = trimmed.Replace('\\', '/');
        if (normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Reasoning reference path is not safe: {value}", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
