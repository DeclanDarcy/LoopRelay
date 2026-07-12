namespace LoopRelay.Permissions.Models.Configuration;

public enum AgentModel
{
    Gpt53CodexSpark,
    Gpt54Mini,
    Gpt55,
    Gpt56Luna,
    Gpt56Terra,
    Gpt56Sol,
}

public enum AgentEffort
{
    Low,
    Medium,
    High,
    XHigh,
}

public enum AgentConfigurationAuthority
{
    Brain,
    Execution,
    Policy,
}

public sealed record BrainConfiguration
{
    public BrainConfiguration(AgentModel model, AgentEffort effort)
    {
        if (!Enum.IsDefined(model))
        {
            throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported Brain model.");
        }

        if (!Enum.IsDefined(effort))
        {
            throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unsupported Brain effort.");
        }

        Model = model;
        Effort = effort;
    }

    public AgentModel Model { get; }

    public AgentEffort Effort { get; }
}

public static class AgentConfigurationCatalog
{
    private static readonly (AgentModel Value, string Name)[] Models =
    [
        (AgentModel.Gpt53CodexSpark, "gpt-5.3-codex-spark"),
        (AgentModel.Gpt54Mini, "gpt-5.4-mini"),
        (AgentModel.Gpt55, "gpt-5.5"),
        (AgentModel.Gpt56Luna, "gpt-5.6-luna"),
        (AgentModel.Gpt56Terra, "gpt-5.6-terra"),
        (AgentModel.Gpt56Sol, "gpt-5.6-sol"),
    ];

    private static readonly (AgentEffort Value, string Name)[] Efforts =
    [
        (AgentEffort.Low, "low"),
        (AgentEffort.Medium, "medium"),
        (AgentEffort.High, "high"),
        (AgentEffort.XHigh, "xhigh"),
    ];

    public static IReadOnlyList<string> AllowedModelNames { get; } =
        Array.AsReadOnly(Models.Select(item => item.Name).ToArray());

    public static IReadOnlyList<string> AllowedEffortNames { get; } =
        Array.AsReadOnly(Efforts.Select(item => item.Name).ToArray());

    public static string Format(AgentModel model) =>
        Models.FirstOrDefault(item => item.Value == model).Name
        ?? throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported agent model.");

    public static string Format(AgentEffort effort) =>
        Efforts.FirstOrDefault(item => item.Value == effort).Name
        ?? throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unsupported agent effort.");

    public static AgentModel ParseModel(string value, string propertyName = "Model") =>
        TryParseModel(value, out AgentModel model)
            ? model
            : throw new ArgumentException(
                $"{propertyName} must be one of: {string.Join(", ", AllowedModelNames)}.",
                propertyName);

    public static AgentEffort ParseEffort(string value, string propertyName = "Effort") =>
        TryParseEffort(value, out AgentEffort effort)
            ? effort
            : throw new ArgumentException(
                $"{propertyName} must be one of: {string.Join(", ", AllowedEffortNames)}.",
                propertyName);

    public static bool TryParseModel(string? value, out AgentModel model)
    {
        foreach ((AgentModel candidate, string name) in Models)
        {
            if (string.Equals(value, name, StringComparison.Ordinal))
            {
                model = candidate;
                return true;
            }
        }

        model = default;
        return false;
    }

    public static bool TryParseEffort(string? value, out AgentEffort effort)
    {
        foreach ((AgentEffort candidate, string name) in Efforts)
        {
            if (string.Equals(value, name, StringComparison.Ordinal))
            {
                effort = candidate;
                return true;
            }
        }

        effort = default;
        return false;
    }
}
