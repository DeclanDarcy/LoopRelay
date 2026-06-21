namespace CommandCenter.Core.Continuity;

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

        foreach (string warning in FindContradictions(signals))
        {
            warnings.Add(warning);
        }

        foreach (DecisionSignal signal in signals.Where(IsDurable).Where(signal => string.IsNullOrWhiteSpace(signal.Rationale)))
        {
            warnings.Add($"Decision rationale may be missing for durable decision: {signal.Statement}");
        }

        return new DecisionAnalysisResult
        {
            Signals = signals,
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

        return new DecisionSignal
        {
            Taxonomy = Classify(statement, isCurrent, retired),
            Statement = statement,
            Rationale = rationale,
            ConstraintsIntroduced = constraints,
            Consequences = consequences,
            OpenQuestions = openQuestions,
            IsSupersededOrRetired = retired,
            SourceRelativePath = sourceRelativePath
        };
    }

    private static DecisionTaxonomy Classify(string statement, bool isCurrent, bool retired)
    {
        if (retired || !isCurrent)
        {
            return DecisionTaxonomy.HistoricalDecision;
        }

        if (ContainsAny(statement, "architecture", "architectural", "authority", "boundary", "workflow authority", "repository-owned", "artifact", "session", "backend", "service", "provider boundary", "operational context"))
        {
            return DecisionTaxonomy.ArchitecturalDecision;
        }

        if (ContainsAny(statement, "slice", "temporary", "one-time", "workaround", "verification", "build", "test", "passed", "commit", "push", "stage", "complete", "completed", "next slice"))
        {
            return DecisionTaxonomy.TacticalDecision;
        }

        if (ContainsAny(statement, "must", "should", "avoid", "remain", "continue", "priority", "guardrail", "roadmap", "durable", "stable", "future", "reviewable", "deterministic", "conservative"))
        {
            return DecisionTaxonomy.StrategicDecision;
        }

        return DecisionTaxonomy.TacticalDecision;
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

    private static IEnumerable<string> FindContradictions(IReadOnlyList<DecisionSignal> signals)
    {
        DecisionSignal[] activeDurableSignals = signals
            .Where(IsDurable)
            .Where(signal => !signal.IsSupersededOrRetired)
            .ToArray();

        foreach (DecisionSignal left in activeDurableSignals)
        {
            foreach (DecisionSignal right in activeDurableSignals)
            {
                if (ReferenceEquals(left, right))
                {
                    continue;
                }

                if (IsNegated(left.Statement) != IsNegated(right.Statement) &&
                    string.Equals(RemoveNegation(left.Statement), RemoveNegation(right.Statement), StringComparison.OrdinalIgnoreCase))
                {
                    yield return $"Contradictory decision signals require review: `{left.Statement}` conflicts with `{right.Statement}`.";
                    yield break;
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
}
