using System.Text;
using LoopRelay.Projections.Models;

namespace LoopRelay.Projections.Services;

internal static class MarkdownTableParser
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseTablesStrict(string markdown) =>
        ParseTables(markdown, strict: true);

    public static void ValidateTables(string markdown)
    {
        _ = ParseTablesStrict(markdown);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseTables(string markdown, bool strict)
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
                string rowLine = lines[index].Trim();
                string[] values = SplitRow(rowLine);
                if (strict && values.Length != headers.Length)
                {
                    throw new MarkdownParseException(
                        $"Malformed Markdown table row has {values.Length} cells but expected {headers.Length}: {rowLine}");
                }

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

    private static string[] SplitRow(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        var cells = new List<string>();
        var current = new StringBuilder();
        for (int index = 0; index < trimmed.Length; index++)
        {
            char value = trimmed[index];
            if (value == '\\' && index + 1 < trimmed.Length && (trimmed[index + 1] == '|' || trimmed[index + 1] == '\\'))
            {
                current.Append(trimmed[index + 1]);
                index++;
                continue;
            }

            if (value == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(value);
        }

        cells.Add(current.ToString().Trim());
        return cells.ToArray();
    }
}
