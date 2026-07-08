using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Plan.Cli.Tests.Models;


/// <summary>A scripted codex turn: inspect (spec, prompt), optionally mutate the store, return a result.</summary>
internal sealed record ScriptedTurn(Func<AgentSessionSpec, string, IArtifactStore, AgentTurnResult> Handler);
