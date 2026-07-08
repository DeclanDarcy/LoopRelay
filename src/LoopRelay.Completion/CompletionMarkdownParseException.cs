using System.Text;

namespace LoopRelay.Completion;

public sealed class CompletionMarkdownParseException(string message) : Exception(message);
