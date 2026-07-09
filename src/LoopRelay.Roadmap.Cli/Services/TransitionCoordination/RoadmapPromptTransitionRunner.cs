using System.Diagnostics;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionState;

namespace LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

internal sealed class RoadmapPromptTransitionRunner(
    TransitionInputResolver _inputResolver,
    RoadmapPromptRunner _promptRunner,
    ITransitionJournalStore _journalStore,
    RoadmapTransitionPersistence _transitionPersistence,
    RoadmapRuntimePromptPolicy? _runtimePromptPolicy = null)
{
    public async Task<string> RunNormalAsync(
        RoadmapState from,
        RoadmapState to,
        string prompt,
        string projectionPath,
        string projectContext,
        string secondaryInput,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken,
        TransitionInputContext? inputContext = null)
    {
        PromptTransitionCompletion completion = await RunNormalWithCompletionAsync(
            from,
            to,
            prompt,
            projectionPath,
            projectContext,
            secondaryInput,
            outputs,
            cancellationToken,
            inputContext);
        return completion.Output;
    }

    public async Task<PromptTransitionCompletion> RunNormalWithCompletionAsync(
        RoadmapState from,
        RoadmapState to,
        string prompt,
        string projectionPath,
        string projectContext,
        string secondaryInput,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken,
        TransitionInputContext? inputContext = null)
    {
        TransitionInputSnapshot inputSnapshot = await ResolveInputSnapshotAsync(
            prompt,
            projectionPath,
            projectContext,
            secondaryInput,
            inputContext ?? TransitionInputContext.Empty);
        string correlationId = Guid.NewGuid().ToString("N");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        string outputList = string.Join(", ", outputs);
        await _transitionPersistence.SaveAsync(to, TransitionStatus.Started, from, to, prompt, projectionPath, outputList, "Pending", started, null, null, null);
        await _journalStore.AppendAsync(new TransitionJournalRecord("TransitionStarted", correlationId, started, from, to, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, 0, "Started", "None", null, inputSnapshot));

        try
        {
            string output = await _promptRunner.RunRuntimePromptAsync(prompt, projectContext, secondaryInput, cancellationToken);
            stopwatch.Stop();
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await _journalStore.AppendAsync(new TransitionJournalRecord("TransitionCompleted", correlationId, completed, from, to, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, stopwatch.ElapsedMilliseconds, "Completed", "None", null, inputSnapshot));
            await _transitionPersistence.SaveAsync(to, TransitionStatus.Completed, from, to, prompt, projectionPath, outputList, "Completed", started, completed, null, null);
            return new PromptTransitionCompletion(correlationId, started, completed, stopwatch.ElapsedMilliseconds, output, inputSnapshot);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            DateTimeOffset failed = DateTimeOffset.UtcNow;
            await _journalStore.AppendAsync(new TransitionJournalRecord("TransitionFailed", correlationId, failed, from, to, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, stopwatch.ElapsedMilliseconds, "Failed", "None", exception.Message, inputSnapshot));
            await _transitionPersistence.SaveAsync(
                RoadmapState.EvidenceBlocked,
                TransitionStatus.Failed,
                from,
                to,
                prompt,
                projectionPath,
                outputList,
                "Failed",
                started,
                failed,
                null,
                [new BlockerRow(exception.Message, "Review the transition failure and rerun.")],
                new RoadmapTransitionIntent("ResolveTransitionFailure", RoadmapState.EvidenceBlocked, outputs),
                ["Resolve blocker and rerun"]);
            throw RoadmapStepException.AlreadyPersisted(exception);
        }
    }

    public async Task<PromptTransitionCompletion> RunPromotionCandidateAsync(
        RoadmapState from,
        RoadmapState promotionTarget,
        string prompt,
        string projectionPath,
        string projectContext,
        string secondaryInput,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken,
        TransitionInputContext? inputContext = null)
    {
        TransitionInputSnapshot inputSnapshot = await ResolveInputSnapshotAsync(
            prompt,
            projectionPath,
            projectContext,
            secondaryInput,
            inputContext ?? TransitionInputContext.Empty);
        string correlationId = Guid.NewGuid().ToString("N");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        string outputList = string.Join(", ", outputs);
        await _transitionPersistence.SaveAsync(from, TransitionStatus.Started, from, promotionTarget, prompt, projectionPath, outputList, "Prompt Started", started, null, null, null);
        await _journalStore.AppendAsync(new TransitionJournalRecord("TransitionStarted", correlationId, started, from, promotionTarget, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, 0, "Started", "None", null, inputSnapshot));

        try
        {
            string output = await _promptRunner.RunRuntimePromptAsync(prompt, projectContext, secondaryInput, cancellationToken);
            stopwatch.Stop();
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await _journalStore.AppendAsync(new TransitionJournalRecord("PromptCompleted", correlationId, completed, from, promotionTarget, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, stopwatch.ElapsedMilliseconds, "PromptCompleted", "Output produced", null, inputSnapshot));
            await _transitionPersistence.SaveAsync(from, TransitionStatus.PromptCompleted, from, promotionTarget, prompt, projectionPath, outputList, "Prompt Completed", started, completed, null, null);
            return new PromptTransitionCompletion(correlationId, started, completed, stopwatch.ElapsedMilliseconds, output, inputSnapshot);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            DateTimeOffset failed = DateTimeOffset.UtcNow;
            await _journalStore.AppendAsync(new TransitionJournalRecord("TransitionFailed", correlationId, failed, from, promotionTarget, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, stopwatch.ElapsedMilliseconds, "Failed", "None", exception.Message, inputSnapshot));
            await _transitionPersistence.SaveAsync(
                RoadmapState.EvidenceBlocked,
                TransitionStatus.Failed,
                from,
                promotionTarget,
                prompt,
                projectionPath,
                outputList,
                "Runtime Failure",
                started,
                failed,
                null,
                [new BlockerRow(exception.Message, "Review the transition failure and rerun.")],
                new RoadmapTransitionIntent("ResolveTransitionFailure", RoadmapState.EvidenceBlocked, outputs),
                ["Resolve blocker and rerun"]);
            throw RoadmapStepException.AlreadyPersisted(exception);
        }
    }

    private async Task<TransitionInputSnapshot> ResolveInputSnapshotAsync(
        string prompt,
        string projectionPath,
        string projectContext,
        string secondaryInput,
        TransitionInputContext inputContext)
    {
        RoadmapRuntimePromptPolicy effectivePolicy = _runtimePromptPolicy ?? RoadmapRuntimePromptPolicy.Default;
        return await _inputResolver.ResolveAsync(new TransitionInputRequest(
            prompt,
            projectionPath,
            projectContext,
            secondaryInput,
            inputContext)
        {
            PromptPolicy = effectivePolicy.CreateIdentity(prompt),
        });
    }
}
