namespace LoopRelay.Orchestration.Tests.Architecture;

public sealed class InteractionBoundaryArchitectureTests
{
    [Fact]
    public void Workflow_kernel_recovery_effect_completion_storage_and_import_code_do_not_read_console_input()
    {
        string root = RepositoryRoot();
        string[] scopedDirectories =
        [
            Path.Combine(root, "src", "LoopRelay.Orchestration.Primitives", "Workflows"),
            Path.Combine(root, "src", "LoopRelay.Orchestration.Primitives", "Runtime"),
            Path.Combine(root, "src", "LoopRelay.Orchestration.Primitives", "Recovery"),
            Path.Combine(root, "src", "LoopRelay.Orchestration.Primitives", "Effects"),
            Path.Combine(root, "src", "LoopRelay.Orchestration.Primitives", "Interactions"),
            Path.Combine(root, "src", "LoopRelay.Orchestration.Primitives", "Persistence"),
            Path.Combine(root, "src", "LoopRelay.Completion"),
            Path.Combine(root, "src", "LoopRelay.Infrastructure", "Services", "Effects"),
            Path.Combine(root, "src", "LoopRelay.Core", "Services", "Persistence"),
        ];
        string[] forbidden = ["Console.Read(", "Console.ReadLine(", "Console.ReadKey(", "Console.In", "System.Console.In"];

        string[] violations = scopedDirectories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(file => forbidden.Any(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(root, file))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Interaction_broker_has_no_renderer_or_console_dependency()
    {
        Type broker = typeof(LoopRelay.Orchestration.Interactions.InteractionBroker);
        string[] dependencyNames = broker.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain(dependencyNames, name => name.Contains("Console", StringComparison.Ordinal));
        Assert.DoesNotContain(dependencyNames, name => name.Contains("Render", StringComparison.Ordinal));
        Assert.Contains(dependencyNames, name => name.Contains("IInteractionStore", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_and_observer_sources_issue_no_sql_or_schema_migration()
    {
        string root = RepositoryRoot();
        string[] files =
        [
            Path.Combine(root, "src", "LoopRelay.Cli", "Services", "Cli", "UnifiedCliRunner.cs"),
            Path.Combine(root, "src", "LoopRelay.Orchestration.Primitives", "Resolution", "RepositoryObserver.cs"),
        ];
        string[] forbidden = ["SqliteCommand", "CommandText", "EnsureSchemaAsync", "OpenReadWrite", "OpenReadWriteCreate"];

        foreach (string file in files)
        {
            string source = File.ReadAllText(file);
            Assert.DoesNotContain(forbidden, token => source.Contains(token, StringComparison.Ordinal));
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoopRelay.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
