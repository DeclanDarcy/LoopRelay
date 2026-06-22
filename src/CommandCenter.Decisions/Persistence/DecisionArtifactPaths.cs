using System.Text.RegularExpressions;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Decisions.Persistence;

internal static partial class DecisionArtifactPaths
{
    public const string SchemaVersion = "1";

    private const string DecisionsRoot = ".agents/decisions";
    private const string RecordsRoot = $"{DecisionsRoot}/records";
    private const string CandidatesRoot = $"{DecisionsRoot}/candidates";
    private const string ProposalsRoot = $"{DecisionsRoot}/proposals";

    public static string DecisionDirectory(string id)
    {
        return ArtifactPath.CombineRelative(RecordsRoot, ValidateId(id, "DEC"));
    }

    public static string CandidateDirectory(string id)
    {
        return ArtifactPath.CombineRelative(CandidatesRoot, ValidateId(id, "CAND"));
    }

    public static string ProposalDirectory(string id)
    {
        return ArtifactPath.CombineRelative(ProposalsRoot, ValidateId(id, "PROP"));
    }

    public static string DecisionJson(string id)
    {
        return ArtifactPath.CombineRelative(DecisionDirectory(id), "decision.json");
    }

    public static string CandidateJson(string id)
    {
        return ArtifactPath.CombineRelative(CandidateDirectory(id), "candidate.json");
    }

    public static string ProposalJson(string id)
    {
        return ArtifactPath.CombineRelative(ProposalDirectory(id), "proposal.json");
    }

    public static string HistoryJsonForDirectory(string relativeDirectory)
    {
        return ArtifactPath.CombineRelative(relativeDirectory, "history.json");
    }

    public static string Resolve(Repository repository, string relativePath)
    {
        return ArtifactPath.ResolveRepositoryPath(repository, relativePath);
    }

    public static string ResolveRoot(Repository repository, DecisionArtifactKind kind)
    {
        string relativePath = kind switch
        {
            DecisionArtifactKind.Decision => RecordsRoot,
            DecisionArtifactKind.Candidate => CandidatesRoot,
            DecisionArtifactKind.Proposal => ProposalsRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported decision artifact kind.")
        };

        return Resolve(repository, relativePath);
    }

    public static string ValidateId(string id, string prefix)
    {
        if (string.IsNullOrWhiteSpace(id) || !IdPattern().IsMatch(id))
        {
            throw new ArgumentException($"{prefix} id must match {prefix}-NNNN.", nameof(id));
        }

        if (!id.StartsWith($"{prefix}-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{prefix} id must start with {prefix}-.", nameof(id));
        }

        return id;
    }

    [GeneratedRegex("^[A-Z]+-[0-9]{4}$")]
    private static partial Regex IdPattern();
}

internal enum DecisionArtifactKind
{
    Decision,
    Candidate,
    Proposal
}
