using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Projections;

internal sealed class MarkdownParseException(string message) : Exception(message);
