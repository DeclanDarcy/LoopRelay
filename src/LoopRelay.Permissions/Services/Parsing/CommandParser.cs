using System.Text;
using LoopRelay.Permissions.Abstractions.Parsing;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Primitives.Parsing;
using LoopRelay.Permissions.Services.Evaluation;

namespace LoopRelay.Permissions.Services.Parsing;

public sealed class CommandParser : ICommandParser
{
    private readonly PermissionPolicyOptions policy;

    public CommandParser()
        : this(PermissionPolicyOptions.Default)
    {
    }

    public CommandParser(PermissionPolicyOptions policy)
    {
        this.policy = PermissionPolicyFactory.MergeWithMinimum(policy);
    }

    public ParseResult Parse(string toolName, string? rawCommand)
    {
        if (rawCommand is null)
        {
            return new ParseResult(
                [new ParsedCommand(toolName, null, [], [])],
                HasUnknownSyntax: false,
                UnknownSyntaxReason: null);
        }

        string[] segments = Policy.PermissionConstants.ChainSplitter.Split(rawCommand)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        var commands = new List<ParsedCommand>();
        foreach (string segment in segments)
        {
            string trimmed = segment.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            (string[]? tokens, string? reason) = Tokenize(trimmed);
            if (tokens is null)
            {
                return new ParseResult(commands.ToArray(), HasUnknownSyntax: true, reason);
            }

            if (tokens.Length > 0)
            {
                commands.Add(BuildParsedCommand(tokens));
            }
        }

        if (commands.Count == 0)
        {
            return new ParseResult([], HasUnknownSyntax: true, "Unsupported shell construct: empty command");
        }

        return new ParseResult(commands.ToArray(), HasUnknownSyntax: false, UnknownSyntaxReason: null);
    }

    internal static (string[]? Tokens, string? UnsupportedReason) Tokenize(string input)
    {
        string? unsupported = DetectUnsupportedSyntax(input);
        if (unsupported is not null)
        {
            return (null, unsupported);
        }

        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inDoubleQuotes = false;
        bool inSingleQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                continue;
            }

            if (c == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                continue;
            }

            if (!inDoubleQuotes && !inSingleQuotes && char.IsWhiteSpace(c))
            {
                FlushToken(tokens, current);
                continue;
            }

            current.Append(c);
        }

        if (inDoubleQuotes || inSingleQuotes)
        {
            return (null, "Unsupported shell construct: unbalanced quotes");
        }

        FlushToken(tokens, current);
        return (tokens.ToArray(), null);
    }

    private static void FlushToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }

    private static string? DetectUnsupportedSyntax(string input)
    {
        if (input.Contains("$(", StringComparison.Ordinal))
        {
            return "Unsupported shell construct: subshell expansion";
        }

        if (input.Contains('`'))
        {
            return "Unsupported shell construct: backtick execution";
        }

        if (input.Contains("<(", StringComparison.Ordinal) || input.Contains(">(", StringComparison.Ordinal))
        {
            return "Unsupported shell construct: process substitution";
        }

        if (input.Contains("<<<", StringComparison.Ordinal) || input.Contains("<<", StringComparison.Ordinal))
        {
            return "Unsupported shell construct: here-document";
        }

        for (int i = 0; i < input.Length - 1; i++)
        {
            if (input[i] != '$')
            {
                continue;
            }

            char next = input[i + 1];
            if (char.IsLetter(next) || next == '_' || next == '{')
            {
                return "Unsupported shell construct: environment variable";
            }
        }

        return null;
    }

    private ParsedCommand BuildParsedCommand(string[] tokens)
    {
        string command = tokens[0];
        string? subcommand = null;
        int startIndex = 1;

        if (tokens.Length > 1 && policy.CommandsWithSubcommands.Contains(command))
        {
            string candidate = tokens[1];
            if (!candidate.StartsWith("-", StringComparison.Ordinal))
            {
                subcommand = candidate;
                startIndex = 2;
            }
        }

        var flags = new List<string>();
        var args = new List<string>();
        for (int i = startIndex; i < tokens.Length; i++)
        {
            if (tokens[i].StartsWith("-", StringComparison.Ordinal))
            {
                flags.Add(tokens[i]);
            }
            else
            {
                args.Add(tokens[i]);
            }
        }

        return new ParsedCommand(command, subcommand, flags.ToArray(), args.ToArray());
    }
}
