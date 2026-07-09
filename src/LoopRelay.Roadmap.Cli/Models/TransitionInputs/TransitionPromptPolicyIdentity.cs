using System.Security.Cryptography;
using System.Text;

namespace LoopRelay.Roadmap.Cli.Models.TransitionInputs;

internal sealed record TransitionPromptPolicyIdentity(
    string Mode,
    IReadOnlyDictionary<string, string> Inputs,
    string Hash)
{
    public static TransitionPromptPolicyIdentity None { get; } =
        Create("none-v1", new SortedDictionary<string, string>(StringComparer.Ordinal));

    public static TransitionPromptPolicyIdentity Create(
        string mode,
        IReadOnlyDictionary<string, string> inputs)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            throw new ArgumentException("Prompt policy mode cannot be empty.", nameof(mode));
        }

        var sortedInputs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in inputs)
        {
            sortedInputs[key] = value;
        }

        string hash = ComputeHash(mode, sortedInputs);
        return new TransitionPromptPolicyIdentity(mode, sortedInputs, hash);
    }

    private static string ComputeHash(string mode, IReadOnlyDictionary<string, string> inputs)
    {
        var builder = new StringBuilder();
        AppendField(builder, "mode", mode);
        foreach ((string name, string value) in inputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            AppendField(builder, $"input.{name}", value);
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void AppendField(StringBuilder builder, string name, string value)
    {
        builder
            .Append(name.Length)
            .Append(':')
            .Append(name)
            .Append('=')
            .Append(value.Length)
            .Append(':')
            .Append(value)
            .AppendLine();
    }
}
