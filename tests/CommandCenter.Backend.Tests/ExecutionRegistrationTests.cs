using CommandCenter.Backend;
using CommandCenter.Core.Continuity;
using CommandCenter.Execution;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Primitives;
using Microsoft.AspNetCore.Builder;

namespace CommandCenter.Backend.Tests;

public sealed class ExecutionRegistrationTests
{
    [Fact]
    public async Task ProgramRegistersExecutionBoundaryServices()
    {
        await using WebApplication app = Program.CreateApp([]);

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
        Assert.IsAssignableFrom<IOperationalContextParser>(
            app.Services.GetRequiredService<IOperationalContextParser>());
        Assert.IsAssignableFrom<IUnderstandingCompressionService>(
            app.Services.GetRequiredService<IUnderstandingCompressionService>());
        Assert.IsAssignableFrom<IOperationalContextProposalStore>(
            app.Services.GetRequiredService<IOperationalContextProposalStore>());
        Assert.IsAssignableFrom<IOperationalContextGenerationService>(
            app.Services.GetRequiredService<IOperationalContextGenerationService>());
        Assert.IsAssignableFrom<IOperationalContextReviewService>(
            app.Services.GetRequiredService<IOperationalContextReviewService>());
        Assert.IsAssignableFrom<IOperationalContextLifecycleService>(
            app.Services.GetRequiredService<IOperationalContextLifecycleService>());
    }

    [Fact]
    public async Task ExecutionStateEnumsSerializeAsHttpJsonStrings()
    {
        await using WebApplication app = Program.CreateApp([]);
        JsonOptions jsonOptions = app.Services.GetRequiredService<IOptions<JsonOptions>>().Value;

        string json = JsonSerializer.Serialize(
            new { executionState = RepositoryExecutionState.Ready },
            jsonOptions.SerializerOptions);

        Assert.Contains("\"executionState\":\"Ready\"", json);
    }
}
