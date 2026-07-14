namespace LoopRelay.Completion.Models.Prompts;

public sealed record CompletionRuntimePromptInvocation(
    string RuntimePromptName,
    string ProjectContext = "",
    string SecondaryInput = "",
    string Label = "");
