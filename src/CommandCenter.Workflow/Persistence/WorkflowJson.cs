using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandCenter.Workflow.Persistence;

public static class WorkflowJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
