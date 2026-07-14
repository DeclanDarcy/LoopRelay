using System.Text.RegularExpressions;

namespace LoopRelay.Infrastructure.Services.Artifacts;

public static class NumberedArtifactSequence
{
    public static int NextNumber(IEnumerable<string> artifactPaths, Regex numberPattern)
    {
        int max = 0;
        foreach (string path in artifactPaths)
        {
            Match match = numberPattern.Match(Path.GetFileName(path));
            if (match.Success &&
                match.Groups.Count > 1 &&
                int.TryParse(match.Groups[1].Value, out int number) &&
                number > max)
            {
                max = number;
            }
        }

        return max + 1;
    }
}
