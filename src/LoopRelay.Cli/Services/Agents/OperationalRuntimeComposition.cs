using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Agents.Services.Usage;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Agents;

/// <summary>
/// Composes the D3 operational wrappers around the production provider runtime, once, at the
/// runtime boundary, under the resolved policy (M7). Input-wait progress sits closest to the
/// provider (it measures actual provider latency per attempt); the telemetry/usage-limit gate
/// wraps it. The caller keeps causal-spine recording outermost so the spine records
/// caller-visible logical turns — usage-limit retries are telemetry-level evidence, not spine
/// turns. Non-production compositions (injected runtimes) skip this entirely: telemetry, quota
/// waits, and input-wait progress describe operating the real provider.
/// </summary>
internal static class OperationalRuntimeComposition
{
    public static IAgentRuntime Compose(
        IAgentRuntime runtime,
        ResolvedOperationalPolicy policy,
        Repository repository,
        IProcessRunner processRunner,
        ILoopConsole console)
    {
        IAgentRuntime composed = runtime;
        InputWaitObservationStore? inputWaitObservations = null;
        if (policy.InputWaitReporting)
        {
            inputWaitObservations = new InputWaitObservationStore();
            composed = new InputWaitProgressAgentRuntime(
                composed,
                new DeterministicAgentTokenEstimator(),
                new ConsoleInputWaitProgressRenderer(console),
                inputWaitObservations);
        }

        if (policy.SessionTelemetry || policy.UsageLimitWaitRetry)
        {
            var clock = new SystemClock();
            composed = new GatedAgentRuntime(
                composed,
                new UsageLimitDetector(clock, new TaskDelayScheduler(), console),
                SessionTelemetryComposition.CreateRecorder(
                    repository,
                    policy.SessionTelemetry,
                    new CodexUsageProbe(processRunner, new EnvironmentAgentExecutableResolver(), repository),
                    new EffectiveTokenCostModel(),
                    clock,
                    console),
                clock,
                SessionTelemetryComposition.RepoName(repository),
                inputWaitObservations,
                _retryOnUsageLimit: policy.UsageLimitWaitRetry);
        }

        return composed;
    }
}
