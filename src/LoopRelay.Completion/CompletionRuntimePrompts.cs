using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Completion;

public static class CompletionRuntimePromptNames
{
    public const string CreateRoadmapCompletionContext = "CreateRoadmapCompletionContext";
    public const string EvaluateEpicCompletionAndDrift = "EvaluateEpicCompletionAndDrift";
    public const string SynthesizeCompletedEpic = "SynthesizeCompletedEpic";
    public const string UpdateRoadmapCompletionContext = "UpdateRoadmapCompletionContext";
}
