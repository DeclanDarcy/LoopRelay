using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Completion;

public sealed record CompletionRuntimePromptInvocation(
    string RuntimePromptName,
    string ProjectContext = "",
    string SecondaryInput = "",
    string Label = "");
