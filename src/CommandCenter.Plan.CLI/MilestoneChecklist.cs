namespace CommandCenter.Plan.Cli;

/// <summary>
/// The strict checkbox-counting rule shared by the main loop's epic-complete gate
/// (<c>CommandCenter.Cli.MilestoneGate.CountCheckboxes</c>) and this pipeline's false-closure guard on
/// ExtractMilestones output. Ported verbatim: per line TrimStart; ``` toggles a code-fence skip; line
/// length must be >= 6; only the exact characters "- [x] " / "- [X] " / "- [ ] " count — nothing else
/// (no other bullet character, no missing trailing space, no unrecognized mark).
/// </summary>
internal static class MilestoneChecklist
{
    public static (int Total, int Completed) CountCheckboxes(string content)
    {
        int total = 0;
        int completed = 0;
        bool insideFence = false;

        foreach (ReadOnlySpan<char> rawLine in content.AsSpan().EnumerateLines())
        {
            ReadOnlySpan<char> line = rawLine.TrimStart();
            if (line.StartsWith("```"))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence || line.Length < 6)
            {
                continue;
            }

            if (line[0] != '-' || line[1] != ' ' || line[2] != '[' || line[4] != ']' || line[5] != ' ')
            {
                continue;
            }

            char mark = line[3];
            if (mark == ' ')
            {
                total++;
            }
            else if (mark is 'x' or 'X')
            {
                total++;
                completed++;
            }
        }

        return (total, completed);
    }
}
