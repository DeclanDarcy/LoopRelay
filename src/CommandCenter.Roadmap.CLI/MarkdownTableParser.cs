namespace CommandCenter.Roadmap.Cli;

internal static class MarkdownTableParser
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseTables(string markdown)
    {
        var tables = new List<IReadOnlyDictionary<string, string>>();
        string[] lines = markdown.Split('\n');

        for (int index = 0; index < lines.Length - 1; index++)
        {
            string headerLine = lines[index].Trim();
            string separatorLine = lines[index + 1].Trim();
            if (!IsTableLine(headerLine) || !IsSeparatorLine(separatorLine))
            {
                continue;
            }

            string[] headers = SplitRow(headerLine);
            index += 2;
            while (index < lines.Length && IsTableLine(lines[index].Trim()))
            {
                string[] values = SplitRow(lines[index].Trim());
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int column = 0; column < headers.Length; column++)
                {
                    row[headers[column]] = column < values.Length ? values[column] : string.Empty;
                }

                tables.Add(row);
                index++;
            }
        }

        return tables;
    }

    public static IReadOnlyDictionary<string, string> ParseFieldTable(string markdown, string sectionHeading)
    {
        string section = ExtractSection(markdown, sectionHeading);
        foreach (IReadOnlyDictionary<string, string> row in ParseTables(section))
        {
            if (row.TryGetValue("Field", out string? field) && row.TryGetValue("Value", out string? value))
            {
                // Field/value tables are easier to consume as a single dictionary.
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (IReadOnlyDictionary<string, string> fieldRow in ParseTables(section))
                {
                    if (fieldRow.TryGetValue("Field", out string? fieldName) &&
                        fieldRow.TryGetValue("Value", out string? fieldValue))
                    {
                        fields[fieldName] = fieldValue;
                    }
                }

                return fields;
            }
        }

        throw new MarkdownParseException($"No field/value table found under `{sectionHeading}`.");
    }

    public static string ExtractSection(string markdown, string sectionHeading)
    {
        int start = markdown.IndexOf(sectionHeading, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new MarkdownParseException($"Required section missing: {sectionHeading}");
        }

        int next = markdown.IndexOf("\n## ", start + sectionHeading.Length, StringComparison.Ordinal);
        return next < 0 ? markdown[start..] : markdown[start..next];
    }

    private static bool IsTableLine(string line) => line.StartsWith('|') && line.EndsWith('|');

    private static bool IsSeparatorLine(string line)
    {
        if (!IsTableLine(line))
        {
            return false;
        }

        string[] cells = SplitRow(line);
        return cells.Length > 0 && cells.All(cell => cell.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Trim().Length == 0);
    }

    private static string[] SplitRow(string line) =>
        line.Trim('|')
            .Split('|')
            .Select(cell => cell.Trim().Replace("\\|", "|", StringComparison.Ordinal))
            .ToArray();
}

internal sealed class MarkdownParseException(string message) : Exception(message);
