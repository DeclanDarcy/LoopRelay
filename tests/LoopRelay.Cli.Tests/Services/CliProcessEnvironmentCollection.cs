namespace LoopRelay.Cli.Tests.Services;

/// <summary>
/// Tests in this collection mutate process-global state (environment variables),
/// so xUnit's <see cref="Xunit.CollectionDefinitionAttribute.DisableParallelization"/>
/// runs them exclusively, after parallel collections complete.
/// Mirrors the "ProcessEnvironment" collection in LoopRelay.Agents.Tests.
/// </summary>
[Xunit.CollectionDefinition("CliProcessEnvironment", DisableParallelization = true)]
public sealed class CliProcessEnvironmentCollection;
