using System.Security.Cryptography;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class RepositoryChangeSetDetectionException(string message)
    : InvalidOperationException(message);
