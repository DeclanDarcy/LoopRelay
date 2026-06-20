using CommandCenter.Backend;
using CommandCenter.Backend.Execution;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionRegistrationTests
{
    [Fact]
    public async Task ProgramRegistersExecutionBoundaryServices()
    {
        await using var app = Program.CreateApp([]);

        Assert.IsAssignableFrom<IExecutionContextService>(
            app.Services.GetRequiredService<IExecutionContextService>());
        Assert.IsAssignableFrom<IExecutionSessionService>(
            app.Services.GetRequiredService<IExecutionSessionService>());
        Assert.IsAssignableFrom<IExecutionPromptBuilder>(
            app.Services.GetRequiredService<IExecutionPromptBuilder>());
        Assert.IsAssignableFrom<IExecutionMonitoringService>(
            app.Services.GetRequiredService<IExecutionMonitoringService>());
        Assert.IsAssignableFrom<IHandoffService>(
            app.Services.GetRequiredService<IHandoffService>());
        Assert.IsAssignableFrom<IExecutionProvider>(
            app.Services.GetRequiredService<IExecutionProvider>());
        Assert.IsAssignableFrom<IGitService>(
            app.Services.GetRequiredService<IGitService>());
    }

    [Fact]
    public async Task ExecutionStateEnumsSerializeAsHttpJsonStrings()
    {
        await using var app = Program.CreateApp([]);
        var jsonOptions = app.Services.GetRequiredService<IOptions<JsonOptions>>().Value;

        var json = JsonSerializer.Serialize(
            new { executionState = RepositoryExecutionState.Ready },
            jsonOptions.SerializerOptions);

        Assert.Contains("\"executionState\":\"Ready\"", json);
    }
}
