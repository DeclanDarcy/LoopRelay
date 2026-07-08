using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Roadmap.Cli;

internal static class RoadmapJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
