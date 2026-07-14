using System.Diagnostics;
using LoopRelay.Application.Contracts;
using LoopRelay.Cli.Surface;
using LoopRelay.Cli.Services.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class CliSurfaceDependencyTests
{
    [Fact]
    public void Parser_and_renderer_assembly_references_only_application_and_framework_contracts()
    {
        string[] references = typeof(CliRequestParser).Assembly.GetReferencedAssemblies()
            .Select(item => item.Name ?? string.Empty).Order(StringComparer.Ordinal).ToArray();

        Assert.Contains("LoopRelay.Application", references);
        Assert.DoesNotContain(references, name => name is
            "LoopRelay.Core" or "LoopRelay.Orchestration" or "LoopRelay.Infrastructure" or
            "LoopRelay.Completion" or "LoopRelay.Agents" or "Microsoft.Data.Sqlite");
    }

    [Theory]
    [InlineData("run", typeof(RunWorkflowRequest))]
    [InlineData("eval", typeof(RunWorkflowRequest))]
    [InlineData("traditional", typeof(RunWorkflowRequest))]
    [InlineData("plan", typeof(RunWorkflowRequest))]
    [InlineData("execute", typeof(RunWorkflowRequest))]
    [InlineData("status", typeof(CanonicalStatusRequest))]
    [InlineData("storage verify", typeof(StorageOperationRequest))]
    [InlineData("storage init", typeof(StorageOperationRequest))]
    [InlineData("storage migrate", typeof(StorageOperationRequest))]
    [InlineData("storage export export.json", typeof(StorageOperationRequest))]
    [InlineData("storage sync", typeof(StorageOperationRequest))]
    [InlineData("import detect", typeof(ImportOperationRequest))]
    [InlineData("import preview import-1", typeof(ImportOperationRequest))]
    [InlineData("import execute import-1", typeof(ImportOperationRequest))]
    [InlineData("import verify import-1", typeof(ImportOperationRequest))]
    [InlineData("recovery inspect recovery-1", typeof(RecoveryOperationRequest))]
    [InlineData("recovery plan recovery-1", typeof(RecoveryOperationRequest))]
    [InlineData("recovery execute recovery-1", typeof(RecoveryOperationRequest))]
    [InlineData("interactions list", typeof(InteractionOperationRequest))]
    [InlineData("interactions show interaction-1", typeof(InteractionOperationRequest))]
    [InlineData("interactions respond interaction-1 answer", typeof(InteractionOperationRequest))]
    [InlineData("interactions cancel interaction-1 reason", typeof(InteractionOperationRequest))]
    [InlineData("completion status", typeof(CompletionOperationRequest))]
    [InlineData("completion reconcile closure-1", typeof(CompletionOperationRequest))]
    [InlineData("capabilities", typeof(CapabilityDiagnosticsRequest))]
    public void Production_parser_covers_the_public_request_matrix(string command, Type expected)
    {
        string repository = Directory.CreateTempSubdirectory("looprelay-cli-surface").FullName;
        string[] arguments = ["--repo", repository, .. command.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

        bool parsed = CliRequestParser.TryParse(arguments, out ParsedCliRequest result, out string error);

        Assert.True(parsed, error);
        Assert.IsType(expected, result.Request);
        Assert.Equal(Path.GetFullPath(repository), result.RepositoryPath);
    }

    [Theory]
    [InlineData("storage sync extra")]
    [InlineData("storage verify extra")]
    [InlineData("interactions list extra")]
    [InlineData("unblock")]
    public void Production_parser_rejects_retired_or_over_arity_commands(string command)
    {
        string repository = Directory.CreateTempSubdirectory("looprelay-cli-invalid").FullName;
        string[] arguments = ["--repo", repository, .. command.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

        bool parsed = CliRequestParser.TryParse(arguments, out _, out string error);

        Assert.False(parsed);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Result_renderer_is_byte_deterministic_and_uses_no_external_dependency()
    {
        var result = new LoopRelayResult(
            ApplicationCorrelationId.New(), ApplicationOutcomeKind.EffectsPending, "pending", 3,
            ["message"], [], new Dictionary<string, string>(), ["evidence"], ["warning"],
            ["effect"], [], [], ["action"]);

        RenderedCliResult first = CliResultRenderer.Render(result);
        RenderedCliResult second = CliResultRenderer.Render(result);

        Assert.Equal(string.Join('\n', first.Output), string.Join('\n', second.Output));
        Assert.Equal(string.Join('\n', first.Errors), string.Join('\n', second.Errors));
    }

    [Fact]
    public async Task Published_cli_exercises_the_full_non_provider_command_matrix_without_untyped_failures()
    {
        string repository = Directory.CreateTempSubdirectory("looprelay-published-matrix").FullName;
        string cli = typeof(LoopRelayCompositionRoot).Assembly.Location;
        try
        {
            var cases = new (string[] Arguments, int[] ExpectedExitCodes)[]
            {
                (["storage", "init"], [0]),
                (["status"], [0]),
                (["storage", "verify"], [0]),
                (["storage", "migrate"], [0, 4]),
                (["storage", "export"], [0]),
                (["storage", "sync"], [0]),
                (["import", "detect"], [0, 4]),
                (["import", "preview"], [4]),
                (["import", "execute"], [4]),
                (["import", "verify"], [4]),
                (["recovery", "inspect", "missing"], [4]),
                (["recovery", "plan", "missing"], [4]),
                (["recovery", "execute", "missing"], [4]),
                (["interactions", "list"], [0]),
                (["interactions", "show", "missing"], [4]),
                (["interactions", "respond", "missing", "answer"], [4]),
                (["interactions", "cancel", "missing"], [4]),
                (["completion", "status"], [3]),
                (["completion", "reconcile"], [3]),
                (["capabilities"], [0, 4]),
            };
            foreach ((string[] arguments, int[] expected) in cases)
            {
                ProcessResult result = await RunPublishedAsync(cli, repository, arguments);
                Assert.Contains(result.ExitCode, expected);
                Assert.DoesNotContain("Unhandled exception", result.Output, StringComparison.OrdinalIgnoreCase);
            }

            string outputDirectory = Path.GetDirectoryName(cli)!;
            Assert.False(File.Exists(Path.Combine(outputDirectory, "LoopRelay.Plan.Cli.dll")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "LoopRelay.Roadmap.Cli.dll")));
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    private static async Task<ProcessResult> RunPublishedAsync(
        string cli,
        string repository,
        IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(cli);
        start.ArgumentList.Add("--repo");
        start.ArgumentList.Add(repository);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not launch published CLI.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await output + await error);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
