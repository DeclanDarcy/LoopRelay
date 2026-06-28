namespace CommandCenter.Backend.Tests;

/// <summary>
/// Serializes every test class that boots a real ASP.NET Core host (<c>Program.CreateApp</c> +
/// <c>app.StartAsync</c>, real Kestrel listeners) and/or mutates process-global state — the
/// <c>COMMAND_CENTER_CONFIGURATION_PATH</c> / <c>COMMAND_CENTER_EXECUTION_SESSIONS_PATH</c>
/// environment variables that <see cref="CommandCenter.Core.Configuration.ApplicationConfigurationStore"/>
/// and <c>FileSystemExecutionSessionStore</c> read when constructed without an explicit path.
///
/// <para>
/// These resources are process-global: an env var set by one class is visible to every concurrently
/// booting app in the same test process, and the shared <c>configuration.json</c> / real ephemeral
/// Kestrel ports / thread-pool are contended when many app-booting tests run at once. xUnit's default
/// is to run distinct collections in parallel; <see cref="Xunit.CollectionDefinitionAttribute.DisableParallelization"/>
/// = <see langword="true"/> makes this collection run with no other collection executing concurrently,
/// so the app-booting / env-mutating classes are serialized against the entire rest of the suite while
/// every other (independent) class stays fully parallel.
/// </para>
///
/// <para>
/// Classes that already isolate their startup from shared process-global state per test (e.g. by
/// injecting an in-memory <c>IApplicationConfigurationStore</c>) are still serialized here because they
/// boot real Kestrel listeners, which remain a contended process resource under heavy parallel load.
/// </para>
/// </summary>
[Xunit.CollectionDefinition("ProcessEnvironment", DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
}
