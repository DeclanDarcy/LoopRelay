using System.Net;
using CommandCenter.Backend;
using Microsoft.AspNetCore.Builder;

namespace CommandCenter.Backend.Tests;

public sealed class PingEndpointTests
{
    [Fact]
    public async Task PingReturnsPong()
    {
        await using WebApplication app = Program.CreateApp([]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + "/api/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Pong", await response.Content.ReadAsStringAsync());
    }
}
