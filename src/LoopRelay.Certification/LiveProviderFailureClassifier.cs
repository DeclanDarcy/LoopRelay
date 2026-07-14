namespace LoopRelay.Certification;

internal static class LiveProviderFailureClassifier
{
    public static CertificationClassification Classify(bool passed, string codexHome)
    {
        if (passed) return CertificationClassification.Passed;
        return HasQuotaExhaustion(codexHome)
            ? CertificationClassification.ProviderRegression
            : CertificationClassification.ProductRegression;
    }

    internal static bool HasQuotaExhaustion(string codexHome)
    {
        string sessions = Path.Combine(codexHome, "sessions");
        if (!Directory.Exists(sessions)) return false;
        try
        {
            foreach (string path in Directory.EnumerateFiles(sessions, "*.jsonl", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(path);
                bool exhausted = content.Contains("\"used_percent\":100", StringComparison.Ordinal) ||
                    content.Contains("\"used_percent\": 100", StringComparison.Ordinal);
                bool emptyCompletion = content.Contains("\"last_agent_message\":null", StringComparison.Ordinal) ||
                    content.Contains("\"last_agent_message\": null", StringComparison.Ordinal);
                if (exhausted && emptyCompletion) return true;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }
}
