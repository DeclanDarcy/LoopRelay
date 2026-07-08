namespace LoopRelay.Completion.Models;

public sealed record CompletionRuntimePromptInvocation(
    string RuntimePromptName,
    string ProjectContext = "",
    string SecondaryInput = "",
    string Label = "");
