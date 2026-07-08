using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed class ExecutionDispositionParser
{
    public ExecutionDisposition Parse(string markdown)
    {
        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTable(markdown, "## Execution Disposition");
        return new ExecutionDisposition(
            RequiredStatus(fields),
            RequiredAllowed(fields, "Confidence", CommonAllowedValues.Confidence),
            Required(fields, "Evidence Summary"),
            RequiredCommand(fields));
    }

    private static ExecutionDispositionStatus RequiredStatus(IReadOnlyDictionary<string, string> fields)
    {
        string value = Required(fields, "Status");
        return ExecutionDispositionProtocol.TryParseStatus(value, out ExecutionDispositionStatus status)
            ? status
            : throw new MarkdownParseException($"Execution disposition status has unsupported value `{value}`.");
    }

    private static ExecutionDispositionCommand RequiredCommand(IReadOnlyDictionary<string, string> fields)
    {
        string value = Required(fields, "Next Step");
        return ExecutionDispositionProtocol.TryParseCommand(value, out ExecutionDispositionCommand command)
            ? command
            : throw new MarkdownParseException($"Execution disposition command has unsupported value `{value}`.");
    }

    private static string RequiredAllowed(IReadOnlyDictionary<string, string> fields, string field, IReadOnlyList<string> allowed)
    {
        string value = Required(fields, field);
        string? match = allowed.FirstOrDefault(allowedValue => string.Equals(allowedValue, value, StringComparison.Ordinal));
        return match ?? throw new MarkdownParseException($"Execution disposition field `{field}` has unsupported value `{value}`.");
    }

    private static string Required(IReadOnlyDictionary<string, string> fields, string field)
    {
        if (!fields.TryGetValue(field, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new MarkdownParseException($"Required execution disposition field missing: {field}");
        }

        return value.Trim();
    }
}
