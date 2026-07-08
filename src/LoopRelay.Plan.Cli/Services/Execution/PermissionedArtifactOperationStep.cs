using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Plan.Cli.Abstractions;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Plan.Cli.Services.Cli;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;

namespace LoopRelay.Plan.Cli.Services.Execution;

internal sealed class PermissionedArtifactOperationStep(
    IAgentRuntime runtime,
    IArtifactStore store,
    PlanArtifacts artifacts,
    ILoopConsole console,
    Repository repository)
{
    private readonly IAgentRuntime _runtime = runtime;
    private readonly IArtifactStore _store = store;
    private readonly PlanArtifacts _artifacts = artifacts;
    private readonly ILoopConsole _console = console;
    private readonly Repository _repository = repository;
    public async Task RunAsync(ArtifactOperationPlan operation, CancellationToken cancellationToken)
    {
        string? changedGuardSnapshot = null;
        foreach (string read in operation.AllowedReads)
        {
            string? content = await _artifacts.ReadAsync(read);
            if (content is null)
            {
                throw new PlanStepException(
                    $"{operation.Label}: required input {read} was not found in the repository.");
            }

            if (operation.ChangedGuard is not null && string.Equals(read, operation.ChangedGuard, StringComparison.Ordinal))
            {
                changedGuardSnapshot = content;
            }
        }

        if (operation.ChangedGuard is not null && changedGuardSnapshot is null)
        {
            throw new PlanStepException(
                $"{operation.Label} is misconfigured: ChangedGuard {operation.ChangedGuard} is not among the operation's "
                + "AllowedReads, so there is no pre-turn snapshot to compare against.");
        }

        OperationPermissionProfile profile = operation.ToPermissionProfile(_repository);
        ArtifactMutationTransaction transaction =
            await ArtifactMutationTransaction.CaptureAsync(_store, _repository, profile);

        IAgentSession? session = null;
        bool keepChanges = false;
        try
        {
            var renderer = new ConsoleTurnRenderer(_console);
            session = await _runtime.OpenSessionAsync(
                AgentSpecs.ScopedArtifactOperation(_repository, profile),
                cancellationToken);
            AgentTurnResult result = await session.RunTurnAsync(
                operation.Prompt,
                renderer.Stream,
                cancellationToken);

            if (result.State != AgentTurnState.Completed)
            {
                throw new PlanStepException(WithDiagnostics(
                    $"{operation.Label} turn ended in state {result.State}.", result.Diagnostics));
            }

            renderer.EchoIfSilent(result.Output);
            await VerifyNoDeletesAsync(operation, transaction);
            await VerifyOutputsAsync(operation, changedGuardSnapshot);
            keepChanges = true;
        }
        catch
        {
            if (!keepChanges)
            {
                await transaction.RestoreAsync();
            }

            throw;
        }
        finally
        {
            if (session is not null)
            {
                await _runtime.CloseSessionAsync(session);
            }
        }
    }

    private async Task VerifyNoDeletesAsync(
        ArtifactOperationPlan operation,
        ArtifactMutationTransaction transaction)
    {
        IReadOnlyList<string> deleted = await transaction.DeletedSnapshotFilesAsync();
        if (deleted.Count > 0)
        {
            throw new PlanStepException(
                $"{operation.Label} deleted declared artifact(s): {string.Join(", ", deleted)}.");
        }
    }

    private async Task VerifyOutputsAsync(ArtifactOperationPlan operation, string? changedGuardSnapshot)
    {
        foreach (string requiredOutput in operation.RequiredOutputs)
        {
            if (!await _artifacts.ExistsAsync(requiredOutput))
            {
                throw new PlanStepException($"{operation.Label} did not produce {requiredOutput}.");
            }
        }

        if (operation.RequiredOutputGlob is { } requiredGlob)
        {
            IReadOnlyList<string> matches = await _artifacts.ListAbsoluteAsync(
                Resolve(requiredGlob.Directory),
                requiredGlob.Pattern);
            if (matches.Count == 0)
            {
                throw new PlanStepException(
                    $"{operation.Label} produced no files matching {requiredGlob.Directory}/{requiredGlob.Pattern}.");
            }

            if (operation.RequireChecklistInGlob)
            {
                int total = 0;
                foreach (string match in matches)
                {
                    string content = await _artifacts.ReadAbsoluteAsync(match) ?? string.Empty;
                    (int matchTotal, _) = MilestoneChecklist.CountCheckboxes(content);
                    total += matchTotal;
                }

                if (total == 0)
                {
                    throw new PlanStepException("extracted milestones contain no trackable checkboxes");
                }
            }
        }

        if (operation.ChangedGuard is { } changedGuard)
        {
            if (!await _artifacts.ExistsAsync(changedGuard))
            {
                throw new PlanStepException(
                    $"{operation.Label} left {changedGuard} missing — it must remain present.");
            }

            string changedContent = await _artifacts.ReadAsync(changedGuard) ?? string.Empty;
            if (string.Equals(changedContent, changedGuardSnapshot ?? string.Empty, StringComparison.Ordinal))
            {
                throw new PlanStepException(
                    $"{operation.Label} left {changedGuard} unchanged — the expected rewrite did not happen.");
            }
        }
    }

    private string Resolve(string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}
