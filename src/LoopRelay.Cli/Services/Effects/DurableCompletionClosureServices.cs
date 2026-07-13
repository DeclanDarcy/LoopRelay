using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Cli.Services.Effects;

internal sealed class CanonicalCertifiedCompletionCandidateSink(
    Repository _repository,
    CanonicalCausalContext _causality) : ICertifiedCompletionCandidateSink
{
    public CompletionDecision? Decision { get; private set; }
    public CompletionCertificate? Certificate { get; private set; }
    public CompletionClosurePlan? Plan { get; private set; }

    public async Task PersistAsync(
        CertifiedCompletionCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (Decision is not null)
            throw new InvalidOperationException("A completion candidate was already persisted for this attempt.");
        var authority = new CompletionAuthority();
        Decision = authority.Decide(new CompletionDecisionInput(
            _causality.Run,
            _causality.Attempt,
            Cancelled: false,
            Failed: false,
            Waiting: false,
            ContinueExecution: false,
            CannotProceedReason: null,
            EvidenceIdentities: candidate.EvidenceIdentities
                .Append($"transition:{_causality.TransitionRun.Value}")
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            GateIdentities: [$"gate:milestone-completion:{_causality.Attempt.Value}"],
            ReviewIdentities: [$"review:completion-certification:{_causality.Attempt.Value}"]),
            DateTimeOffset.UtcNow);
        Certificate = CompletionCertificate.Create(Decision, DateTimeOffset.UtcNow);
        Plan = CompletionClosurePlan.Build(
            Decision,
            Certificate,
            nestedAgentsChanged: true,
            parentRepositoryChanged: true,
            plannedAt: DateTimeOffset.UtcNow);
        await new CanonicalCompletionAuthorityStore(_repository).PersistCertifiedCandidateAsync(
            Decision, Certificate, Plan, cancellationToken);
    }
}

internal sealed class DurableCompletionContextMaterializer(
    Repository _repository,
    CanonicalCausalContext _causality,
    IArtifactStore _artifactStore) : ICompletionContextMaterializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> MaterializeAsync(
        string roadmapCompletionContextPath,
        string evidenceDirectory,
        string evidenceStem,
        string content,
        CancellationToken cancellationToken)
    {
        var artifacts = new CompletionArtifacts(_artifactStore, _repository);
        string evidencePath = await artifacts.NextNumberedPathAsync(evidenceDirectory, evidenceStem);
        var workStore = new CanonicalEffectWorkStore(_repository);
        EffectWorkItem? archive = (await workStore.ReadBySemanticOperationAsync(
                "completion:archive-and-synthesize", cancellationToken))
            .SingleOrDefault(item => item.Intent.Causality == _causality);
        bool bootstrap = string.Equals(evidenceStem, "roadmap-completion-bootstrap", StringComparison.Ordinal);
        if (!bootstrap && archive is null)
            throw new InvalidOperationException("Completion context update requires the verified archive effect intent.");
        string contextSemanticOperation = bootstrap
            ? "completion:roadmap-context-bootstrap"
            : "completion:roadmap-context-materialization";
        EffectIntent context = Intent(
            contextSemanticOperation,
            roadmapCompletionContextPath,
            content,
            order: 10,
            archive is null ? [] : [archive.Intent.Identity]);
        EffectIntent evidence = Intent(
            "completion:roadmap-context-evidence",
            evidencePath,
            content,
            order: 11,
            [context.Identity]);
        await workStore.AppendPlanAsync([context, evidence], cancellationToken);
        var executor = new FilesystemWriteEffectExecutor(_repository);
        var worker = new EffectWorker(
            $"completion-context-{Environment.ProcessId}",
            workStore,
            new EffectExecutorRegistry([executor]),
            new FilesystemWriteEffectReconciler(_repository),
            TimeSpan.FromMinutes(5));
        var selected = new HashSet<EffectIntentIdentity> { context.Identity, evidence.Identity };
        for (int pass = 0; pass < 3; pass++)
            await worker.RunOnceAsync(cancellationToken, includePending: true, only: selected);
        foreach (EffectIntent intent in new[] { context, evidence })
        {
            EffectWorkItem item = await workStore.ReadAsync(intent.Identity, cancellationToken)
                ?? throw new InvalidOperationException("Completion context effect intent disappeared.");
            if (item.State != EffectLifecycle.Succeeded || item.Receipt is not { PostconditionSatisfied: true })
                throw new InvalidOperationException(
                    $"Completion context materialization did not produce a verified receipt; current state is {item.State}.");
        }
        return evidencePath;
    }

    private EffectIntent Intent(
        string semanticOperation,
        string relativePath,
        string content,
        int order,
        IReadOnlyList<EffectIntentIdentity> dependencies)
    {
        var payload = new FilesystemWriteEffectPayload(relativePath, content);
        string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        string payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        return new EffectIntent(
            EffectIntentIdentity.New(),
            _causality,
            semanticOperation,
            WorkspaceEffectExecutorKeys.FilesystemWrite,
            "1",
            new EffectTargetDescriptor("CompletionContextArtifact", relativePath, payloadJson),
            payloadJson,
            payloadHash,
            order,
            dependencies,
            EffectRequiredness.BlockingLocal,
            new EffectCondition("content-hash-not-yet-verified", payloadHash),
            new EffectCondition("content-hash-verified", payloadHash),
            "filesystem-content-hash",
            $"{semanticOperation}:{_causality.TransitionRun.Value}:{payloadHash}",
            DateTimeOffset.UtcNow);
    }
}
