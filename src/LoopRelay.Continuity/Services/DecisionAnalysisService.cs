using System.Security.Cryptography;
using System.Text;
using LoopRelay.Continuity.Abstractions;
using LoopRelay.Continuity.Models;
using LoopRelay.Continuity.Primitives;

namespace LoopRelay.Continuity.Services;

public sealed class DecisionAnalysisService : IDecisionAnalysisService
{
    public DecisionAnalysisResult Analyze(IReadOnlyList<DecisionArtifactInput> decisionArtifacts)
    {
        var signals = new List<DecisionSignal>();
        var warnings = new List<string>();

        foreach (DecisionArtifactInput artifact in decisionArtifacts
            .OrderByDescending(artifact => artifact.IsCurrent)
            .ThenBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            foreach (string bullet in ExtractBullets(artifact.Content))
            {
                DecisionSignal signal = AnalyzeBullet(bullet, artifact.RelativePath, artifact.IsCurrent);
                signals.Add(signal);
            }
        }

        int tacticalCount = signals.Count(signal => signal.Taxonomy == DecisionTaxonomy.TacticalDecision);
        if (tacticalCount >= 3)
        {
            warnings.Add($"{tacticalCount} tactical decision signal(s) remain in decision history; avoid promoting execution detail into operational context.");
        }

        int historicalCount = signals.Count(signal => signal.Taxonomy == DecisionTaxonomy.HistoricalDecision);
        if (historicalCount >= 3)
        {
            warnings.Add($"{historicalCount} historical decision signal(s) detected; avoid replaying completed milestone history into current understanding.");
        }

        ContinuityDecisionConsequence[] consequences = FindConsequences(signals).ToArray();
        ContinuityDecisionContradiction[] contradictions = FindContradictions(signals).ToArray();
        warnings.AddRange(contradictions.Select(contradiction => contradiction.CompatibilityWarning));

        foreach (DecisionSignal signal in signals.Where(IsDurable).Where(signal => string.IsNullOrWhiteSpace(signal.Rationale)))
        {
            warnings.Add($"Decision rationale may be missing for durable decision: {signal.Statement}");
        }

        return new DecisionAnalysisResult
        {
            Signals = signals,
            Consequences = consequences,
            Contradictions = contradictions,
            Warnings = warnings
        };
    }

    private static DecisionSignal AnalyzeBullet(string bullet, string sourceRelativePath, bool isCurrent)
    {
        string statement = StripDecisionPrefix(bullet);
        string[] openQuestions = ExtractOpenQuestions(statement).ToArray();
        string? rationale = ExtractRationale(statement);
        string[] constraints = ExtractConstraints(statement).ToArray();
        string[] consequences = ExtractConsequences(statement).ToArray();
        bool retired = ContainsAny(statement, "superseded", "retired", "deprecated", "replaced", "no longer");
        DecisionTaxonomyBasis taxonomyBasis = Classify(statement, isCurrent, retired);

        return new DecisionSignal
        {
            DecisionId = CreateItemId(sourceRelativePath, statement),
            Taxonomy = taxonomyBasis.Taxonomy,
            TaxonomyBasis = taxonomyBasis,
            Statement = statement,
            Rationale = rationale,
            ConstraintsIntroduced = constraints,
            Consequences = consequences,
            OpenQuestions = openQuestions,
            IsSupersededOrRetired = retired,
            SourceRelativePath = sourceRelativePath
        };
    }

    private static DecisionTaxonomyBasis Classify(string statement, bool isCurrent, bool retired)
    {
        var candidates = new List<TaxonomyMatch>();

        if (retired || !isCurrent)
        {
            candidates.Add(new TaxonomyMatch(
                DecisionTaxonomy.HistoricalDecision,
                retired ? "superseded-or-retired" : "historical-artifact",
                retired
                    ? MatchEvidence(statement, "superseded", "retired", "deprecated", "replaced", "no longer")
                    : [$"Artifact version: historical"]));
        }

        string[] architecturalTerms =
        [
            "architecture",
            "architectural",
            "authority",
            "boundary",
            "workflow authority",
            "repository-owned",
            "artifact",
            "session",
            "backend",
            "service",
            "provider boundary",
            "operational context"
        ];
        string[] tacticalTerms =
        [
            "slice",
            "temporary",
            "one-time",
            "workaround",
            "verification",
            "build",
            "test",
            "passed",
            "commit",
            "push",
            "stage",
            "complete",
            "completed",
            "next slice"
        ];
        string[] strategicTerms =
        [
            "must",
            "should",
            "avoid",
            "remain",
            "continue",
            "priority",
            "guardrail",
            "epic",
            "durable",
            "stable",
            "future",
            "reviewable",
            "deterministic",
            "conservative"
        ];

        string[] architecturalEvidence = MatchEvidence(statement, architecturalTerms);
        if (architecturalEvidence.Length > 0)
        {
            candidates.Add(new TaxonomyMatch(
                DecisionTaxonomy.ArchitecturalDecision,
                "architectural-continuity-keywords",
                architecturalEvidence));
        }

        string[] tacticalEvidence = MatchEvidence(statement, tacticalTerms);
        if (tacticalEvidence.Length > 0)
        {
            candidates.Add(new TaxonomyMatch(
                DecisionTaxonomy.TacticalDecision,
                "tactical-execution-keywords",
                tacticalEvidence));
        }

        string[] strategicEvidence = MatchEvidence(statement, strategicTerms);
        if (strategicEvidence.Length > 0)
        {
            candidates.Add(new TaxonomyMatch(
                DecisionTaxonomy.StrategicDecision,
                "strategic-policy-keywords",
                strategicEvidence));
        }

        if (candidates.Count == 0)
        {
            return new DecisionTaxonomyBasis
            {
                Taxonomy = DecisionTaxonomy.TacticalDecision,
                IsHeuristicFallback = true,
                FallbackReason = "No taxonomy rules matched; defaulted to tactical so unclassified text does not become durable operational context.",
                Diagnostics = ["No taxonomy keyword evidence matched the decision statement."]
            };
        }

        TaxonomyMatch selected = candidates
            .OrderBy(match => GetTaxonomyPrecedence(match.Taxonomy))
            .First();
        string[] diagnostics = candidates.Count > 1
            ? [$"Ambiguous taxonomy match resolved to {selected.Taxonomy} by precedence over {string.Join(", ", candidates.Where(match => match.Taxonomy != selected.Taxonomy).Select(match => match.Taxonomy).Distinct())}."]
            : [];

        return new DecisionTaxonomyBasis
        {
            Taxonomy = selected.Taxonomy,
            MatchedRules = candidates.Select(match => match.Rule).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            MatchedEvidence = candidates.SelectMany(match => match.Evidence).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Diagnostics = diagnostics
        };
    }

    private static int GetTaxonomyPrecedence(DecisionTaxonomy taxonomy)
    {
        return taxonomy switch
        {
            DecisionTaxonomy.HistoricalDecision => 0,
            DecisionTaxonomy.ArchitecturalDecision => 1,
            DecisionTaxonomy.TacticalDecision => 2,
            DecisionTaxonomy.StrategicDecision => 3,
            _ => 4
        };
    }

    private static string[] MatchEvidence(string value, params string[] candidates)
    {
        return candidates
            .Where(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => $"Matched `{candidate}` in `{value}`")
            .ToArray();
    }

    private static IEnumerable<string> ExtractBullets(string markdown)
    {
        foreach (string rawLine in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length > 2 &&
                (line[0] == '-' || line[0] == '*' || line[0] == '+') &&
                char.IsWhiteSpace(line[1]))
            {
                string text = line[2..].Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }
        }
    }

    private static string StripDecisionPrefix(string value)
    {
        foreach (string prefix in new[] { "Decision:", "Decision signal:", "Authorized decision:" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[prefix.Length..].Trim();
            }
        }

        return value.Trim();
    }

    private static string? ExtractRationale(string statement)
    {
        foreach (string marker in new[] { " because ", " since ", " so that " })
        {
            int index = statement.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && index + marker.Length < statement.Length)
            {
                return statement[(index + marker.Length)..].Trim().TrimEnd('.');
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractConstraints(string statement)
    {
        if (ContainsAny(statement, "must", "must not", "should", "should not", "required", "mandatory", "avoid"))
        {
            yield return statement;
        }
    }

    private static IEnumerable<string> ExtractConsequences(string statement)
    {
        foreach (string marker in new[] { "therefore", "as a result", "consequence:" })
        {
            int index = statement.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                yield return statement[index..].Trim();
            }
        }
    }

    private static IEnumerable<string> ExtractOpenQuestions(string statement)
    {
        if (statement.EndsWith("?", StringComparison.Ordinal) ||
            statement.Contains("open decision", StringComparison.OrdinalIgnoreCase) ||
            statement.Contains("open question", StringComparison.OrdinalIgnoreCase))
        {
            yield return statement;
        }
    }

    private static IEnumerable<ContinuityDecisionConsequence> FindConsequences(IReadOnlyList<DecisionSignal> signals)
    {
        foreach (DecisionSignal signal in signals)
        {
            foreach (string consequence in signal.Consequences)
            {
                yield return new ContinuityDecisionConsequence
                {
                    ConsequenceId = CreateItemId(signal.DecisionId, consequence),
                    OriginatingDecision = CreateReference(signal),
                    OperationalStatement = $"Decision consequence: {consequence}",
                    AffectedArea = DetermineAffectedArea(signal.Statement),
                    SupportingEvidence = BuildConsequenceEvidence(signal, consequence),
                    OperationalImpact = BuildOperationalImpact(signal, consequence)
                };
            }
        }
    }

    private static IEnumerable<ContinuityDecisionContradiction> FindContradictions(IReadOnlyList<DecisionSignal> signals)
    {
        DecisionSignal[] activeDurableSignals = signals
            .Where(IsDurable)
            .Where(signal => !signal.IsSupersededOrRetired)
            .ToArray();

        for (int leftIndex = 0; leftIndex < activeDurableSignals.Length; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < activeDurableSignals.Length; rightIndex++)
            {
                DecisionSignal left = activeDurableSignals[leftIndex];
                DecisionSignal right = activeDurableSignals[rightIndex];

                if (IsNegated(left.Statement) != IsNegated(right.Statement) &&
                    string.Equals(RemoveNegation(left.Statement), RemoveNegation(right.Statement), StringComparison.OrdinalIgnoreCase))
                {
                    DecisionContradictionSeverity severity = DetermineSeverity(left, right);
                    string warning = $"Contradictory decision signals require review: `{left.Statement}` conflicts with `{right.Statement}`.";
                    yield return new ContinuityDecisionContradiction
                    {
                        ContradictionId = CreateItemId(left.DecisionId, right.DecisionId),
                        DecisionA = CreateReference(left),
                        DecisionB = CreateReference(right),
                        ConflictType = DecisionContradictionConflictType.DirectNegation,
                        ConflictEvidence =
                        [
                            $"Decision A normalized without negation: {RemoveNegation(left.Statement).Trim()}",
                            $"Decision B normalized without negation: {RemoveNegation(right.Statement).Trim()}",
                            $"Decision A negated: {IsNegated(left.Statement)}",
                            $"Decision B negated: {IsNegated(right.Statement)}"
                        ],
                        Severity = severity,
                        ResolutionGuidance = severity is DecisionContradictionSeverity.Critical or DecisionContradictionSeverity.High
                            ? "Resolve the conflicting durable decision before promoting the operational context."
                            : "Review the conflicting durable decision before relying on this context.",
                        CompatibilityWarning = warning
                    };
                }
            }
        }
    }

    private static bool IsDurable(DecisionSignal signal)
    {
        return signal.Taxonomy is DecisionTaxonomy.ArchitecturalDecision or DecisionTaxonomy.StrategicDecision;
    }

    private static bool IsNegated(string value)
    {
        return ContainsAny(value, "must not", "should not", "do not", "does not", "cannot", "avoid");
    }

    private static string RemoveNegation(string value)
    {
        string normalized = Normalize(value)
            .Replace(" must not ", " must ", StringComparison.OrdinalIgnoreCase)
            .Replace(" should not ", " should ", StringComparison.OrdinalIgnoreCase)
            .Replace(" do not ", " do ", StringComparison.OrdinalIgnoreCase)
            .Replace(" does not ", " does ", StringComparison.OrdinalIgnoreCase)
            .Replace(" cannot ", " can ", StringComparison.OrdinalIgnoreCase)
            .Replace(" avoid ", " ", StringComparison.OrdinalIgnoreCase);

        return normalized;
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        return $" {string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))} ";
    }

    private static ContinuityDecisionReference CreateReference(DecisionSignal signal)
    {
        return new ContinuityDecisionReference
        {
            DecisionId = signal.DecisionId,
            SourceRelativePath = signal.SourceRelativePath,
            Statement = signal.Statement,
            Taxonomy = signal.Taxonomy
        };
    }

    private static IReadOnlyList<string> BuildConsequenceEvidence(DecisionSignal signal, string consequence)
    {
        var evidence = new List<string>
        {
            $"Source artifact: {signal.SourceRelativePath}",
            $"Decision statement: {signal.Statement}",
            $"Consequence statement: {consequence}"
        };

        if (!string.IsNullOrWhiteSpace(signal.Rationale))
        {
            evidence.Add($"Rationale: {signal.Rationale}");
        }

        return evidence;
    }

    private static string DetermineAffectedArea(string value)
    {
        if (ContainsAny(value, "workflow", "gate", "lifecycle"))
        {
            return "Workflow";
        }

        if (ContainsAny(value, "operational context", "continuity", "handoff"))
        {
            return "Operational context";
        }

        if (ContainsAny(value, "architecture", "authority", "boundary", "service", "backend"))
        {
            return "Architecture";
        }

        if (ContainsAny(value, "decision", "governance", "review"))
        {
            return "Decision governance";
        }

        if (ContainsAny(value, "execution", "prompt", "provider"))
        {
            return "Execution";
        }

        return "General";
    }

    private static string BuildOperationalImpact(DecisionSignal signal, string consequence)
    {
        return $"Applying `{signal.Statement}` changes {DetermineAffectedArea(signal.Statement).ToLowerInvariant()} behavior: {consequence}";
    }

    private static DecisionContradictionSeverity DetermineSeverity(DecisionSignal left, DecisionSignal right)
    {
        string combined = $"{left.Statement} {right.Statement}";
        if (ContainsAny(combined, "authority", "boundary", "must own", "must not own"))
        {
            return DecisionContradictionSeverity.Critical;
        }

        if (ContainsAny(combined, "must", "cannot"))
        {
            return DecisionContradictionSeverity.High;
        }

        if (ContainsAny(combined, "should", "avoid"))
        {
            return DecisionContradictionSeverity.Medium;
        }

        return DecisionContradictionSeverity.Low;
    }

    private static string CreateItemId(string section, string text)
    {
        string normalized = string.Join(
            ' ',
            $"{section}:{text}".Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"{NormalizeIdPrefix(section)}-{Convert.ToHexString(bytes)[..12].ToLowerInvariant()}";
    }

    private static string NormalizeIdPrefix(string value)
    {
        return string.Join(
            '-',
            value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record TaxonomyMatch(
        DecisionTaxonomy Taxonomy,
        string Rule,
        IReadOnlyList<string> Evidence);
}
