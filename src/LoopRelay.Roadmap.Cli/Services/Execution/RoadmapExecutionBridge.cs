using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Models.Trust;
using LoopRelay.Infrastructure.Primitives.Trust;
using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed class RoadmapExecutionBridge(
    IAgentRuntime runtime,
    RoadmapArtifacts artifacts,
    Repository repository,
    ILoopConsole console,
    RoadmapExecutionOptions? options = null) : IRoadmapExecutionBridge
{
    public async Task<RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken)
    {
        RoadmapExecutionOptions effectiveOptions = options ?? RoadmapExecutionOptions.Default;
        effectiveOptions.Validate();

        string prompt = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ExecutionPrompt);
        var renderer = new ConsoleTurnRenderer(console);
        AgentSessionSpec spec = AgentSpecs.ExecutionBridge(repository, effectiveOptions);
        bool persistent = spec.Sandbox.RequiresApproval;
        string evidencePath = await WriteExecutionPostureEvidenceAsync(
            spec,
            persistent ? ExecutionAuthority.PersistentSession : ExecutionAuthority.OneShot,
            effectiveOptions);

        AgentTurnResult result;
        if (persistent)
        {
            IAgentSession? session = null;
            try
            {
                session = await runtime.OpenSessionAsync(spec, cancellationToken);
                result = await session.RunTurnAsync(prompt, renderer.Stream, cancellationToken);
            }
            finally
            {
                if (session is not null)
                {
                    await runtime.CloseSessionAsync(session);
                }
            }
        }
        else
        {
            result = await runtime.RunOneShotAsync(
                spec,
                prompt,
                renderer.Stream,
                cancellationToken);
        }

        renderer.EchoIfSilent(result.Output);
        return result.State == AgentTurnState.Completed
            ? RoadmapExecutionTransportResult.Completed(result.Output, evidencePath)
            : RoadmapExecutionTransportResult.Failed(
                result.State.ToString(),
                result.Diagnostics ?? $"Execution bridge ended in state {result.State}.",
                result.Output,
                evidencePath);
    }

    private async Task<string> WriteExecutionPostureEvidenceAsync(
        AgentSessionSpec spec,
        ExecutionAuthority execution,
        RoadmapExecutionOptions options)
    {
        TrustPolicyEvidence evidence = TrustPolicy.FromSandboxProfile(spec.Sandbox, execution).ToEvidence();
        string mode = options.IsElevated ? "Elevated" : "Default";
        string content =
            $"""
            # Roadmap Execution Trust Posture

            | Field | Value |
            |---|---|
            | Mode | {mode} |
            | Sandbox | {evidence.Sandbox} |
            | Workspace | {evidence.Workspace} |
            | Network | {evidence.Network} |
            | Approval | {evidence.Approval} |
            | Execution | {evidence.Execution} |
            | Elevated Reason | {Escape(options.ElevatedReason ?? "None")} |
            | Recorded At | {DateTimeOffset.UtcNow:O} |
            """;

        return await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.ExecutionEvidenceDirectory,
            "execution-trust-posture",
            content);
    }

    private static string Escape(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}
