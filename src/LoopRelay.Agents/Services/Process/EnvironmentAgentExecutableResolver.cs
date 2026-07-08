using LoopRelay.Agents.Abstractions;

namespace LoopRelay.Agents.Services.Process;

public sealed class EnvironmentAgentExecutableResolver : IAgentExecutableResolver
{
    public const string ExecutableEnvironmentVariable = "CODEX_EXECUTABLE";

    public string Resolve() =>
        Environment.GetEnvironmentVariable(ExecutableEnvironmentVariable) is { Length: > 0 } path
            ? path
            : throw new Exception($"Environment variable {ExecutableEnvironmentVariable} was not found.");
}
