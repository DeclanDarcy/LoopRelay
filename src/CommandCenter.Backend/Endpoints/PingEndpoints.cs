namespace CommandCenter.Backend.Endpoints;

public static class PingEndpoints
{
    public static IEndpointRouteBuilder MapPingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPing();
        return app;
    }

    private static void MapPing(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/ping", () => "Pong");
}
