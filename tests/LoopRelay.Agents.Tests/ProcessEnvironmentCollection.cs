namespace LoopRelay.Agents.Tests;

/// <summary>
/// Serializes every test class that mutates process-global state — environment variables,
/// the current working directory, or real child processes. These resources are shared by the
/// whole test process, so xUnit's <see cref="Xunit.CollectionDefinitionAttribute.DisableParallelization"/>
/// = <see langword="true"/> runs this collection with no other collection executing concurrently,
/// while every other (independent) class stays fully parallel.
/// </summary>
[Xunit.CollectionDefinition("ProcessEnvironment", DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
}
