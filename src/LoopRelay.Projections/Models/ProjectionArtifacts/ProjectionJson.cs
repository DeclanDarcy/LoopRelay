using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Projections.Models.ProjectionArtifacts;

internal static class ProjectionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
