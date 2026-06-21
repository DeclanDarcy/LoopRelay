using CommandCenter.Continuity;
using CommandCenter.Continuity.Abstractions;

namespace CommandCenter.Backend.Endpoints;

public static class ContinuityEndpoints
{
    public static IEndpointRouteBuilder MapContinuityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetDiagnostics();
        app.MapGenerateReport();
        app.MapListReports();
        return app;
    }

    private static void MapGetDiagnostics(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/continuity/diagnostics", async (
            Guid repositoryId,
            IContinuityDiagnosticsService diagnosticsService) =>
        {
            try
            {
                return Results.Ok(await diagnosticsService.GetDiagnosticsAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

    private static void MapGenerateReport(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/repositories/{repositoryId:guid}/continuity/reports", async (
            Guid repositoryId,
            IContinuityReportService reportService) =>
        {
            try
            {
                return Results.Ok(await reportService.GenerateReportAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (IOException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
            catch (UnauthorizedAccessException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });

    private static void MapListReports(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/repositories/{repositoryId:guid}/continuity/reports", async (
            Guid repositoryId,
            IContinuityReportService reportService) =>
        {
            try
            {
                return Results.Ok(await reportService.ListReportsAsync(repositoryId));
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });
}
