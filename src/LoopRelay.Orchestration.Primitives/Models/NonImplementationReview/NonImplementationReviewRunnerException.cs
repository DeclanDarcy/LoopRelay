using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationReviewRunnerException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
