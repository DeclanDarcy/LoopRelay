using CommandCenter.Agents.Abstractions;

namespace CommandCenter.Agents.Services;

public sealed class EnvironmentAgentExecutableResolver : IAgentExecutableResolver
{
    public const string ExecutableEnvironmentVariable = "CODEX_EXECUTABLE";

    public string Resolve() =>
        Environment.GetEnvironmentVariable(ExecutableEnvironmentVariable) is { Length: > 0 } path
            ? path
            : "codex";
}
