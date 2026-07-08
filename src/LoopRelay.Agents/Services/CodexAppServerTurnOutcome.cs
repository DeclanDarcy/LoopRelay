using System.Text;
using System.Text.Json;
using LoopRelay.Agents.Models;

namespace LoopRelay.Agents.Services;

/// <summary>The accumulated result of one Codex app-server turn.</summary>
public sealed record CodexAppServerTurnOutcome(
    string Output,
    AgentTokenUsage? Usage,
    AgentTurnState State,
    string? FailureMessage);
