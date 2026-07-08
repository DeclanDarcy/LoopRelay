using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoopRelay.Roadmap.Cli;

internal sealed class RepositoryWorkSemanticExecutor(RoadmapArtifacts artifacts, ILoopConsole? console = null)
{
    private const string SubjectType = "RepositoryWork";
    private const string ProtocolId = "repositorywork.semantic-execution.v1";
    private const string ProtocolOwner = "RepositoryWork semantic execution";
    private const string ConsumerScope = "repositorywork.semantic-evaluation";

    public async Task<RepositoryWorkSemanticExecutionResult> ExecuteAsync(
        RepositoryWorkSemanticRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        string runId = Guid.NewGuid().ToString("N");
        var writtenArtifacts = new List<string>();

        RepositoryWorkSemanticLedgerDocument ledger = await LoadLedgerAsync();
        RepositoryWorkSubjectIdentityDocument subject = await LoadOrCreateSubjectAsync(startedAt, writtenArtifacts);
        RepositoryWorkProtocolDefinitionRecord protocol = CreateProtocolDefinition();
        await WriteJsonAsync(RepositoryWorkSemanticArtifactPaths.ProtocolDefinition, protocol, writtenArtifacts);

        var intent = new RepositoryWorkIntentRecord(
            $"intent-{runId}",
            subject.SubjectId,
            request.Operation,
            request.IntentSummary,
            request.SourcePath,
            startedAt);

        string? sourceContent = await artifacts.ReadAsync(request.SourcePath);
        RepositoryWorkSourceCaptureRecord? sourceCapture = null;
        if (sourceContent is not null)
        {
            sourceCapture = await CaptureSourceAsync(runId, subject, intent, request.SourcePath, sourceContent, startedAt, writtenArtifacts);
        }

        RepositoryWorkAdmissionRecord admission = EvaluateAdmission(runId, subject, intent, request, sourceCapture, startedAt);
        await WriteJsonAsync(admission.AdmissionPath, admission, writtenArtifacts);

        if (admission.Outcome != RepositoryWorkAdmissionOutcome.Admitted)
        {
            return await PersistNonAdmittedRunAsync(
                ledger,
                runId,
                startedAt,
                subject,
                intent,
                sourceCapture,
                admission,
                writtenArtifacts);
        }

        int version = NextVersion(ledger);
        string? supersededVersionPath = LastPromotedSummaryVersion(ledger);
        bool currentSummaryWasMissingOrStale = await CurrentSummaryIsMissingOrStaleAsync(sourceCapture);

        RepositoryWorkInteractionRecord interaction = await ExecuteInteractionAsync(
            runId,
            subject,
            protocol,
            intent,
            sourceCapture!,
            admission,
            startedAt,
            writtenArtifacts);

        RepositoryWorkEvidenceRecord evidence = await BindEvidenceAsync(
            runId,
            subject,
            sourceCapture!,
            interaction,
            writtenArtifacts);

        RepositoryWorkBlockerRecord? blocker = null;
        if (currentSummaryWasMissingOrStale)
        {
            blocker = await CreateRecoverableBlockerAsync(runId, subject, intent, evidence, startedAt, writtenArtifacts);
        }

        RepositoryWorkArtifactRecord candidate = await CreateCandidateArtifactAsync(
            version,
            subject,
            intent,
            sourceCapture!,
            admission,
            interaction,
            evidence,
            writtenArtifacts);

        RepositoryWorkArtifactValidationRecord artifactValidation = ValidateCandidate(candidate, sourceCapture!, startedAt);
        RepositoryWorkDecisionRecord decision = AcceptPromotionDecision(subject, evidence, artifactValidation, startedAt);

        RepositoryWorkPromotionRecord promotion = await PromoteCandidateAsync(
            candidate,
            decision,
            version,
            supersededVersionPath,
            writtenArtifacts);

        RepositoryWorkStateEntryRecord stateEntry = new(
            $"state-{runId}",
            subject.SubjectId,
            RepositoryWorkLifecycleState.StateCurrent,
            ProtocolOwner,
            interaction.InteractionId,
            evidence.EvidenceId,
            "State entry agrees with accepted evidence, artifact validation, promotion decision, and RepositoryWork lifecycle.",
            [candidate.Path, promotion.AuthoritativeVersionPath, promotion.CurrentArtifactPath],
            [decision.DecisionId],
            [RepositoryWorkSemanticRequest.ExecutionOperation, RepositoryWorkSemanticRequest.ReportOperation],
            "active",
            DateTimeOffset.UtcNow);
        await WriteJsonAsync(RepositoryWorkSemanticArtifactPaths.CurrentState, stateEntry, writtenArtifacts);

        RepositoryWorkRecoveryReviewRecord? recoveryReview = null;
        if (blocker is not null)
        {
            recoveryReview = await RecoverBlockerAsync(runId, subject, blocker, promotion, stateEntry, writtenArtifacts);
        }

        RepositoryWorkCertificationRecord certification = await CertifyCompletionAsync(
            subject,
            interaction,
            evidence,
            decision,
            promotion,
            stateEntry,
            writtenArtifacts);

        RepositoryWorkDistillationRecord distillation = await DistillCurrentUnderstandingAsync(
            version,
            subject,
            evidence,
            certification,
            promotion,
            writtenArtifacts);

        RepositoryWorkCapabilityDeclarationRecord capability = CreateCapabilityDeclaration(subject, protocol);
        await WriteJsonAsync(RepositoryWorkSemanticArtifactPaths.CapabilityDeclaration, capability, writtenArtifacts);
        RepositoryWorkCapabilityEvaluationRecord capabilityEvaluation = await EvaluateCapabilityAsync(
            capability,
            subject,
            protocol,
            writtenArtifacts);

        string reportPath = RepositoryWorkSemanticArtifactPaths.RunReport(runId);
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        RepositoryWorkSemanticRunRecord run = new(
            runId,
            startedAt,
            completedAt,
            "RepositoryWork semantic path executed from subject identity through capability declaration.",
            RepositoryWorkSemanticArtifactPaths.Ledger,
            "Inspect the ledger, current state, promoted summary, recovery review, certification, distillation, and capability conformance report.",
            "RepositoryWork identity is semantic continuity governed by protocol admission, not command reachability or file placement.",
            intent,
            sourceCapture,
            admission,
            interaction,
            evidence,
            candidate,
            artifactValidation,
            decision,
            promotion,
            stateEntry,
            blocker,
            recoveryReview,
            certification,
            distillation,
            capability,
            capabilityEvaluation,
            reportPath);

        string report = RenderExecutionReport(subject, run);
        await WriteTextAsync(reportPath, report, writtenArtifacts);
        await WriteTextAsync(RepositoryWorkSemanticArtifactPaths.LatestReport, report, writtenArtifacts);
        await AppendLedgerAsync(ledger, run, writtenArtifacts);

        console?.Info($"RepositoryWork semantic execution completed: {subject.SubjectId}");
        console?.Info($"Semantic report: {reportPath}");

        return new RepositoryWorkSemanticExecutionResult(
            RepositoryWorkAdmissionOutcome.Admitted,
            subject.SubjectId,
            runId,
            reportPath,
            writtenArtifacts.Distinct(StringComparer.Ordinal).ToArray());
    }

    private async Task<RepositoryWorkSubjectIdentityDocument> LoadOrCreateSubjectAsync(
        DateTimeOffset now,
        List<string> writtenArtifacts)
    {
        string? existing = await artifacts.ReadAsync(RepositoryWorkSemanticArtifactPaths.SubjectIdentity);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            RepositoryWorkSubjectIdentityDocument loaded = DeserializeRequired<RepositoryWorkSubjectIdentityDocument>(
                existing,
                RepositoryWorkSemanticArtifactPaths.SubjectIdentity);
            if (!string.Equals(loaded.SchemaVersion, RepositoryWorkSubjectIdentityDocument.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new RoadmapStepException(
                    $"RepositoryWork subject identity has unsupported schema version `{loaded.SchemaVersion}`.");
            }

            return loaded;
        }

        RepositoryWorkRepositoryIdentity repositoryIdentity = await ResolveRepositoryIdentityAsync();
        string subjectId = $"repository-work:{ShortHash($"{SubjectType}:{repositoryIdentity.Kind}:{repositoryIdentity.Value}")}";
        var subject = new RepositoryWorkSubjectIdentityDocument(
            RepositoryWorkSubjectIdentityDocument.CurrentSchemaVersion,
            subjectId,
            SubjectType,
            repositoryIdentity,
            "Ownerless repositories remain inspectable; mutation and acceptance require declared RepositoryWork authority scopes.",
            CreateAuthorityScopeDeclarations(),
            Enum.GetNames<RepositoryWorkLifecycleState>(),
            CreateInvariantDeclarations(),
            now,
            now);

        await WriteJsonAsync(RepositoryWorkSemanticArtifactPaths.SubjectIdentity, subject, writtenArtifacts);
        return subject;
    }

    private async Task<RepositoryWorkRepositoryIdentity> ResolveRepositoryIdentityAsync()
    {
        string? origin = await TryReadGitOriginAsync();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            string normalized = NormalizeRepositoryIdentityValue(origin);
            return new RepositoryWorkRepositoryIdentity("git-origin", normalized, Sha256(normalized));
        }

        string fallback = string.IsNullOrWhiteSpace(artifacts.Repository.Name)
            ? "ownerless-local-repository"
            : $"ownerless-local:{artifacts.Repository.Name.Trim()}";
        return new RepositoryWorkRepositoryIdentity("repository-name", fallback, Sha256(fallback));
    }

    private async Task<string?> TryReadGitOriginAsync()
    {
        string? config = await artifacts.ReadAsync(".git/config");
        if (string.IsNullOrWhiteSpace(config))
        {
            return null;
        }

        bool inOrigin = false;
        foreach (string rawLine in config.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inOrigin = string.Equals(line, "[remote \"origin\"]", StringComparison.Ordinal);
                continue;
            }

            if (inOrigin && line.StartsWith("url =", StringComparison.Ordinal))
            {
                return line["url =".Length..].Trim();
            }
        }

        return null;
    }

    private static IReadOnlyList<RepositoryWorkAuthorityScope> CreateAuthorityScopeDeclarations() =>
    [
        new(RepositoryWorkAuthorityScopes.RepositoryRead, "permission", ProtocolOwner, "Read repository facts for subject-bound inspection."),
        new(RepositoryWorkAuthorityScopes.SemanticExecution, "protocol-admission", ProtocolOwner, "Admit the RepositoryWork semantic execution protocol."),
        new(RepositoryWorkAuthorityScopes.ArtifactPromotion, "mutation", ProtocolOwner, "Transfer authority from candidate artifact to current artifact version."),
        new(RepositoryWorkAuthorityScopes.DecisionAcceptance, "acceptance", ProtocolOwner, "Persist evidence-bound decisions."),
        new(RepositoryWorkAuthorityScopes.StateEntry, "mutation", ProtocolOwner, "Enter current RepositoryWork semantic state."),
        new(RepositoryWorkAuthorityScopes.RecoveryReview, "acceptance", ProtocolOwner, "Recover or retain a blocker through preserved evidence."),
        new(RepositoryWorkAuthorityScopes.Certification, "acceptance", ProtocolOwner, "Certify completion claims from evidence."),
        new(RepositoryWorkAuthorityScopes.Distillation, "mutation", ProtocolOwner, "Update current understanding without rewriting history."),
        new(RepositoryWorkAuthorityScopes.CapabilityEvaluation, "acceptance", ProtocolOwner, "Evaluate capability declarations without granting runtime authority."),
        new(RepositoryWorkAuthorityScopes.Report, "report", ProtocolOwner, "Render non-mutating semantic reports."),
    ];

    private static IReadOnlyList<RepositoryWorkInvariantDeclaration> CreateInvariantDeclarations() =>
    [
        new("no-subjectless-interaction", "No governed interaction may execute without a RepositoryWork subject identity."),
        new("no-intentless-interaction", "No governed interaction may execute without subject-bound intent."),
        new("no-authority-without-scope", "Permission, mutation, and acceptance authority must name a scope."),
        new("report-fields-do-not-create-authority", "Report fields and rendered summaries never create execution or mutation authority."),
    ];

    private static RepositoryWorkProtocolDefinitionRecord CreateProtocolDefinition() => new(
        RepositoryWorkProtocolDefinitionRecord.CurrentSchemaVersion,
        ProtocolId,
        ProtocolOwner,
        SubjectType,
        "RepositoryWorkSemanticExecution or RepositoryWorkSemanticReport intent bound to one RepositoryWork subject.",
        ["subject identity", "intent", "source snapshot for execution", "authority scopes"],
        ["Captured source hash must match the observed source snapshot for execution."],
        RepositoryWorkAuthorityScopes.DefaultExecutionScopes,
        [
            "no-subjectless-interaction",
            "no-intentless-interaction",
            "no-authority-without-scope",
            "report-fields-do-not-create-authority",
        ],
        [
            RepositoryWorkAdmissionOutcome.Admitted,
            RepositoryWorkAdmissionOutcome.Denied,
            RepositoryWorkAdmissionOutcome.Blocked,
            RepositoryWorkAdmissionOutcome.ReportOnly,
            RepositoryWorkAdmissionOutcome.Unsupported,
        ]);

    private async Task<RepositoryWorkSourceCaptureRecord> CaptureSourceAsync(
        string runId,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkIntentRecord intent,
        string sourcePath,
        string sourceContent,
        DateTimeOffset capturedAt,
        List<string> writtenArtifacts)
    {
        string hash = Sha256(sourceContent);
        string capturedView = RenderCapturedSourceView(subject, intent, sourcePath, hash, sourceContent, capturedAt);
        await WriteTextAsync(RepositoryWorkSemanticArtifactPaths.CapturedSourceView, capturedView, writtenArtifacts);

        return new RepositoryWorkSourceCaptureRecord(
            $"source-{runId}",
            subject.SubjectId,
            intent.IntentId,
            sourcePath,
            "repository artifact read",
            hash,
            Encoding.UTF8.GetByteCount(sourceContent),
            "fresh when repository bytes match captured SHA-256",
            "repository-authored-input",
            ConsumerScope,
            RepositoryWorkSemanticArtifactPaths.CapturedSourceView,
            capturedAt);
    }

    private RepositoryWorkAdmissionRecord EvaluateAdmission(
        string runId,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkIntentRecord intent,
        RepositoryWorkSemanticRequest request,
        RepositoryWorkSourceCaptureRecord? sourceCapture,
        DateTimeOffset createdAt)
    {
        bool isExecution = string.Equals(request.Operation, RepositoryWorkSemanticRequest.ExecutionOperation, StringComparison.Ordinal);
        bool isReport = string.Equals(request.Operation, RepositoryWorkSemanticRequest.ReportOperation, StringComparison.Ordinal);
        IReadOnlyList<string> requiredScopes = isReport
            ? [RepositoryWorkAuthorityScopes.RepositoryRead, RepositoryWorkAuthorityScopes.Report]
            : RepositoryWorkAuthorityScopes.DefaultExecutionScopes;

        IReadOnlyList<RepositoryWorkCheckRecord> authorityChecks = requiredScopes
            .Select(scope => new RepositoryWorkCheckRecord(
                $"authority:{scope}",
                request.AuthorityScopes.Contains(scope, StringComparer.Ordinal),
                scope,
                request.AuthorityScopes.Contains(scope, StringComparer.Ordinal)
                    ? "Requested authority includes the required scope."
                    : "Requested authority is missing the required scope."))
            .ToArray();

        IReadOnlyList<RepositoryWorkCheckRecord> invariantChecks =
        [
            new("no-subjectless-interaction", !string.IsNullOrWhiteSpace(subject.SubjectId), subject.SubjectId, "Subject identity is present."),
            new("no-intentless-interaction", !string.IsNullOrWhiteSpace(intent.Summary), intent.IntentId, "Subject-bound intent is present."),
            new(
                "no-authority-without-scope",
                request.AuthorityScopes.All(scope => !string.IsNullOrWhiteSpace(scope)),
                string.Join(", ", request.AuthorityScopes),
                "Every requested authority value names a scope."),
            new(
                "report-fields-do-not-create-authority",
                isReport || requiredScopes.All(scope => !string.Equals(scope, RepositoryWorkAuthorityScopes.Report, StringComparison.Ordinal) || request.AuthorityScopes.Contains(RepositoryWorkAuthorityScopes.Report, StringComparer.Ordinal)),
                RepositoryWorkAuthorityScopes.Report,
                "Report authority is recorded separately and is not counted as mutation or acceptance authority."),
        ];

        RepositoryWorkCheckRecord sourceFreshness = isReport
            ? new RepositoryWorkCheckRecord("source-freshness", true, request.SourcePath, "Report-only inspection does not require a source snapshot.")
            : new RepositoryWorkCheckRecord(
                "source-freshness",
                sourceCapture is not null,
                request.SourcePath,
                sourceCapture is null
                    ? "Required source snapshot is missing."
                    : "Source snapshot was captured with a content hash freshness rule.");

        RepositoryWorkAdmissionOutcome outcome;
        string reason;
        if (!isExecution && !isReport)
        {
            outcome = RepositoryWorkAdmissionOutcome.Unsupported;
            reason = $"No RepositoryWork protocol accepts operation `{request.Operation}`.";
        }
        else if (!sourceFreshness.Passed)
        {
            outcome = RepositoryWorkAdmissionOutcome.Blocked;
            reason = sourceFreshness.Reason;
        }
        else if (authorityChecks.Any(check => !check.Passed))
        {
            outcome = RepositoryWorkAdmissionOutcome.Denied;
            reason = "Requested authority does not satisfy protocol scope requirements.";
        }
        else if (invariantChecks.Any(check => !check.Passed))
        {
            outcome = RepositoryWorkAdmissionOutcome.Denied;
            reason = "One or more constitutional invariants failed.";
        }
        else if (isReport)
        {
            outcome = RepositoryWorkAdmissionOutcome.ReportOnly;
            reason = "Report-only protocol admitted; no mutation authority is granted.";
        }
        else
        {
            outcome = RepositoryWorkAdmissionOutcome.Admitted;
            reason = "Protocol admitted subject-bound intent under scoped authority.";
        }

        return new RepositoryWorkAdmissionRecord(
            $"admission-{runId}",
            ProtocolId,
            subject.SubjectId,
            intent.IntentId,
            request.Operation,
            outcome,
            reason,
            authorityChecks,
            invariantChecks,
            sourceFreshness,
            RepositoryWorkSemanticArtifactPaths.Admission(runId),
            createdAt);
    }

    private async Task<RepositoryWorkSemanticExecutionResult> PersistNonAdmittedRunAsync(
        RepositoryWorkSemanticLedgerDocument ledger,
        string runId,
        DateTimeOffset startedAt,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkIntentRecord intent,
        RepositoryWorkSourceCaptureRecord? sourceCapture,
        RepositoryWorkAdmissionRecord admission,
        List<string> writtenArtifacts)
    {
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        string reportPath = RepositoryWorkSemanticArtifactPaths.RunReport(runId);
        var run = new RepositoryWorkSemanticRunRecord(
            runId,
            startedAt,
            completedAt,
            $"RepositoryWork semantic execution stopped at admission: {admission.Outcome}.",
            admission.AdmissionPath,
            "Inspect the admission record to see protocol, authority, source freshness, and invariant checks.",
            "Protocol admission remains the only path from intent to governed work.",
            intent,
            sourceCapture,
            admission,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            reportPath);

        string report = RenderExecutionReport(subject, run);
        await WriteTextAsync(reportPath, report, writtenArtifacts);
        await WriteTextAsync(RepositoryWorkSemanticArtifactPaths.LatestReport, report, writtenArtifacts);
        await AppendLedgerAsync(ledger, run, writtenArtifacts);

        console?.Warn($"RepositoryWork semantic admission returned {admission.Outcome}: {admission.Reason}");
        return new RepositoryWorkSemanticExecutionResult(
            admission.Outcome,
            subject.SubjectId,
            runId,
            reportPath,
            writtenArtifacts.Distinct(StringComparer.Ordinal).ToArray());
    }

    private async Task<bool> CurrentSummaryIsMissingOrStaleAsync(RepositoryWorkSourceCaptureRecord? sourceCapture)
    {
        string? current = await artifacts.ReadAsync(RepositoryWorkSemanticArtifactPaths.CurrentSummary);
        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        return sourceCapture is not null &&
            !current.Contains(sourceCapture.SnapshotHash, StringComparison.Ordinal);
    }

    private async Task<RepositoryWorkInteractionRecord> ExecuteInteractionAsync(
        string runId,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkProtocolDefinitionRecord protocol,
        RepositoryWorkIntentRecord intent,
        RepositoryWorkSourceCaptureRecord sourceCapture,
        RepositoryWorkAdmissionRecord admission,
        DateTimeOffset startedAt,
        List<string> writtenArtifacts)
    {
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        string observationPath = RepositoryWorkSemanticArtifactPaths.Observation(runId);
        string observationContent = RenderObservation(subject, intent, sourceCapture, admission, observedAt);
        await WriteTextAsync(observationPath, observationContent, writtenArtifacts);

        var observation = new RepositoryWorkObservationRecord(
            $"observation-{runId}",
            subject.SubjectId,
            "source-and-admission-observation",
            "Observed source capture, protocol admission, authority checks, and invariant checks before artifact generation.",
            observationPath,
            observedAt);

        bool accepted = sourceCapture.SnapshotHash.Length == 64 &&
            admission.Outcome == RepositoryWorkAdmissionOutcome.Admitted &&
            admission.AuthorityChecks.All(check => check.Passed) &&
            admission.InvariantChecks.All(check => check.Passed);
        var validation = new RepositoryWorkValidationRecord(
            "RepositoryWorkObservationValidator",
            accepted,
            accepted
                ? "Observation accepted as evidence input."
                : "Observation rejected because source, admission, authority, or invariant checks were not accepted.",
            DateTimeOffset.UtcNow);

        return new RepositoryWorkInteractionRecord(
            $"interaction-{runId}",
            admission.AdmissionId,
            protocol.ProtocolId,
            subject.SubjectId,
            [
                new("subject", RepositoryWorkSemanticArtifactPaths.SubjectIdentity, Sha256(JsonSerializer.Serialize(subject, RoadmapJson.Options)), "semantic identity"),
                new("intent", intent.IntentId, Sha256(intent.Summary), "subject-bound intent"),
                new("source", sourceCapture.Origin, sourceCapture.SnapshotHash, "captured source snapshot"),
                new("admission", admission.AdmissionPath, Sha256(JsonSerializer.Serialize(admission, RoadmapJson.Options)), "protocol admission"),
            ],
            observation,
            validation,
            accepted ? "observation-accepted-for-evidence" : "observation-retained-without-evidence-authority",
            startedAt,
            DateTimeOffset.UtcNow);
    }

    private async Task<RepositoryWorkEvidenceRecord> BindEvidenceAsync(
        string runId,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkSourceCaptureRecord sourceCapture,
        RepositoryWorkInteractionRecord interaction,
        List<string> writtenArtifacts)
    {
        string evidencePath = RepositoryWorkSemanticArtifactPaths.Evidence(runId);
        string evidenceContent = RenderEvidence(subject, sourceCapture, interaction);
        string evidenceHash = Sha256(evidenceContent);
        await WriteTextAsync(evidencePath, evidenceContent, writtenArtifacts);

        return new RepositoryWorkEvidenceRecord(
            $"evidence-{runId}",
            subject.SubjectId,
            interaction.InteractionId,
            interaction.Observation.ObservationId,
            ConsumerScope,
            "validated-interaction-observation",
            evidencePath,
            evidenceHash,
            [sourceCapture.Origin, sourceCapture.CapturedArtifactPath, interaction.Observation.RawObservationPath],
            DateTimeOffset.UtcNow);
    }

    private async Task<RepositoryWorkBlockerRecord> CreateRecoverableBlockerAsync(
        string runId,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkIntentRecord intent,
        RepositoryWorkEvidenceRecord evidence,
        DateTimeOffset createdAt,
        List<string> writtenArtifacts)
    {
        string blockerPath = RepositoryWorkSemanticArtifactPaths.Blocker(runId);
        var blocker = new RepositoryWorkBlockerRecord(
            $"blocker-{runId}",
            subject.SubjectId,
            "current-semantic-summary-missing-or-stale",
            intent.IntentId,
            [evidence.EvidenceId],
            "The authoritative current semantic summary was missing or did not match the captured source hash at admission time.",
            "Promote a validated current semantic summary through RepositoryWork artifact promotion authority.",
            blockerPath,
            createdAt);

        await WriteTextAsync(blockerPath, RenderBlocker(blocker), writtenArtifacts);
        return blocker;
    }

    private async Task<RepositoryWorkArtifactRecord> CreateCandidateArtifactAsync(
        int version,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkIntentRecord intent,
        RepositoryWorkSourceCaptureRecord sourceCapture,
        RepositoryWorkAdmissionRecord admission,
        RepositoryWorkInteractionRecord interaction,
        RepositoryWorkEvidenceRecord evidence,
        List<string> writtenArtifacts)
    {
        string path = RepositoryWorkSemanticArtifactPaths.CandidateSummary(version);
        string content = RenderCurrentSummary(subject, intent, sourceCapture, admission, interaction, evidence, version);
        await WriteTextAsync(path, content, writtenArtifacts);

        return new RepositoryWorkArtifactRecord(
            $"artifact-current-summary-v{version:0000}",
            subject.SubjectId,
            ProtocolOwner,
            "current-semantic-summary",
            "markdown",
            version,
            "candidate",
            path,
            Sha256(content),
            [sourceCapture.CapturedArtifactPath, evidence.EvidencePath],
            RepositoryWorkAuthorityScopes.ArtifactPromotion);
    }

    private static RepositoryWorkArtifactValidationRecord ValidateCandidate(
        RepositoryWorkArtifactRecord candidate,
        RepositoryWorkSourceCaptureRecord sourceCapture,
        DateTimeOffset startedAt)
    {
        IReadOnlyList<RepositoryWorkCheckRecord> checks =
        [
            new("identity", candidate.SubjectId.Length > 0, candidate.SubjectId, "Candidate is bound to a subject identity."),
            new("role", string.Equals(candidate.Role, "current-semantic-summary", StringComparison.Ordinal), candidate.Role, "Candidate role is current-semantic-summary."),
            new("source-provenance", candidate.Provenance.Contains(sourceCapture.CapturedArtifactPath, StringComparer.Ordinal), sourceCapture.CapturedArtifactPath, "Candidate records captured source provenance."),
            new("freshness", candidate.Provenance.Count > 0 && sourceCapture.SnapshotHash.Length == 64, sourceCapture.SnapshotHash, "Candidate is tied to a hash-addressed source capture."),
            new("mutation-authority", string.Equals(candidate.MutationAuthority, RepositoryWorkAuthorityScopes.ArtifactPromotion, StringComparison.Ordinal), candidate.MutationAuthority, "Candidate requires artifact promotion authority."),
        ];

        return new RepositoryWorkArtifactValidationRecord(
            $"artifact-validation-{candidate.Version:0000}",
            candidate.ArtifactId,
            checks.All(check => check.Passed),
            checks,
            startedAt);
    }

    private static RepositoryWorkDecisionRecord AcceptPromotionDecision(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkEvidenceRecord evidence,
        RepositoryWorkArtifactValidationRecord validation,
        DateTimeOffset startedAt)
    {
        string accepted = validation.Accepted ? "promote-candidate" : "retain-candidate";
        return new RepositoryWorkDecisionRecord(
            $"decision-{ShortHash($"{evidence.EvidenceId}:{validation.ValidationId}")}",
            subject.SubjectId,
            "candidate-artifact-promotion",
            ["promote-candidate", "retain-candidate", "report-only"],
            "promote-candidate",
            accepted,
            evidence.EvidenceId,
            "RepositoryWorkArtifactPromotionValidator",
            RepositoryWorkAuthorityScopes.DecisionAcceptance,
            validation.Accepted ? "promote current semantic summary" : "retain candidate without authority transfer",
            startedAt);
    }

    private async Task<RepositoryWorkPromotionRecord> PromoteCandidateAsync(
        RepositoryWorkArtifactRecord candidate,
        RepositoryWorkDecisionRecord decision,
        int version,
        string? supersededVersionPath,
        List<string> writtenArtifacts)
    {
        if (!string.Equals(decision.AcceptedChoice, "promote-candidate", StringComparison.Ordinal))
        {
            return new RepositoryWorkPromotionRecord(
                $"promotion-{version:0000}",
                candidate.ArtifactId,
                decision.DecisionId,
                false,
                "none",
                RepositoryWorkSemanticArtifactPaths.CurrentSummary,
                RepositoryWorkSemanticArtifactPaths.SummaryVersion(version),
                supersededVersionPath,
                version,
                "Candidate retained because validation did not accept promotion.",
                DateTimeOffset.UtcNow);
        }

        string content = await artifacts.ReadRequiredAsync(candidate.Path);
        string versionPath = RepositoryWorkSemanticArtifactPaths.SummaryVersion(version);
        await WriteTextAsync(versionPath, content, writtenArtifacts);
        await WriteTextAsync(RepositoryWorkSemanticArtifactPaths.CurrentSummary, content, writtenArtifacts);

        return new RepositoryWorkPromotionRecord(
            $"promotion-{version:0000}",
            candidate.ArtifactId,
            decision.DecisionId,
            true,
            RepositoryWorkAuthorityScopes.ArtifactPromotion,
            RepositoryWorkSemanticArtifactPaths.CurrentSummary,
            versionPath,
            supersededVersionPath,
            version,
            "Candidate validation and evidence-bound decision authorized promotion.",
            DateTimeOffset.UtcNow);
    }

    private async Task<RepositoryWorkRecoveryReviewRecord> RecoverBlockerAsync(
        string runId,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkBlockerRecord blocker,
        RepositoryWorkPromotionRecord promotion,
        RepositoryWorkStateEntryRecord stateEntry,
        List<string> writtenArtifacts)
    {
        bool eligible = promotion.Accepted && stateEntry.State == RepositoryWorkLifecycleState.StateCurrent;
        string reviewPath = RepositoryWorkSemanticArtifactPaths.RecoveryReview(runId);
        var review = new RepositoryWorkRecoveryReviewRecord(
            $"recovery-{runId}",
            subject.SubjectId,
            "Recover current semantic summary freshness after authorized promotion.",
            blocker.BlockerId,
            eligible,
            promotion.AuthoritativeVersionPath,
            eligible,
            eligible ? "recovered" : "retained-blocker",
            eligible ? RepositoryWorkLifecycleState.Recovered : RepositoryWorkLifecycleState.Blocked,
            reviewPath,
            DateTimeOffset.UtcNow);

        await WriteTextAsync(reviewPath, RenderRecoveryReview(blocker, review), writtenArtifacts);
        return review;
    }

    private async Task<RepositoryWorkCertificationRecord> CertifyCompletionAsync(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkInteractionRecord interaction,
        RepositoryWorkEvidenceRecord evidence,
        RepositoryWorkDecisionRecord decision,
        RepositoryWorkPromotionRecord promotion,
        RepositoryWorkStateEntryRecord stateEntry,
        List<string> writtenArtifacts)
    {
        bool accepted = interaction.Validation.Accepted &&
            promotion.Accepted &&
            stateEntry.State == RepositoryWorkLifecycleState.StateCurrent;
        var certification = new RepositoryWorkCertificationRecord(
            $"certification-{ShortHash($"{evidence.EvidenceId}:{promotion.PromotionId}")}",
            subject.SubjectId,
            "RepositoryWork semantic execution reached current state through admitted protocol, evidence, decision, and promotion.",
            [evidence.EvidenceId, decision.DecisionId, promotion.PromotionId, stateEntry.StateEntryId],
            "RepositoryWorkCompletionCertificationPolicy",
            "Completion requires accepted observation evidence, promoted artifact, accepted decision, and current state entry.",
            accepted,
            accepted ? "certified" : "rejected",
            accepted ? RepositoryWorkLifecycleState.CompletionCertified : RepositoryWorkLifecycleState.StateCurrent,
            DateTimeOffset.UtcNow);

        await WriteJsonAsync(RepositoryWorkSemanticArtifactPaths.CompletionCertification, certification, writtenArtifacts);
        return certification;
    }

    private async Task<RepositoryWorkDistillationRecord> DistillCurrentUnderstandingAsync(
        int version,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkEvidenceRecord evidence,
        RepositoryWorkCertificationRecord certification,
        RepositoryWorkPromotionRecord promotion,
        List<string> writtenArtifacts)
    {
        string versionPath = RepositoryWorkSemanticArtifactPaths.UnderstandingVersion(version);
        string content = RenderCurrentUnderstanding(subject, evidence, certification, promotion, version);
        await WriteTextAsync(versionPath, content, writtenArtifacts);
        await WriteTextAsync(RepositoryWorkSemanticArtifactPaths.CurrentUnderstanding, content, writtenArtifacts);

        return new RepositoryWorkDistillationRecord(
            $"distillation-{version:0000}",
            subject.SubjectId,
            evidence.EvidenceId,
            RepositoryWorkSemanticArtifactPaths.CurrentUnderstanding,
            versionPath,
            "Current understanding placement is valid because certified evidence and promoted summary are referenced by lineage.",
            [evidence.EvidencePath, promotion.AuthoritativeVersionPath, RepositoryWorkSemanticArtifactPaths.CompletionCertification],
            DateTimeOffset.UtcNow);
    }

    private static RepositoryWorkCapabilityDeclarationRecord CreateCapabilityDeclaration(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkProtocolDefinitionRecord protocol) => new(
        "repositorywork-semantic-execution",
        "RepositoryWork Semantic Execution",
        [subject.SubjectType],
        [RepositoryWorkSemanticRequest.ExecutionOperation, RepositoryWorkSemanticRequest.ReportOperation],
        [protocol.ProtocolId],
        protocol.AuthorityScopes,
        ["captured source view", "raw interaction observation"],
        ["validated interaction evidence"],
        ["candidate current semantic summary", "promoted current semantic summary", "current understanding"],
        ["candidate artifact promotion decision", "completion certification decision", "capability acceptance decision"],
        ["candidate-to-current promotion", "supersession", "source-to-artifact provenance", "evidence-to-decision consumption"],
        Enum.GetNames<RepositoryWorkLifecycleState>(),
        ["StateCurrent", "Recovered", "CompletionCertified", "Distilled", "CapabilityEvaluated"],
        protocol.Invariants,
        ["latest semantic report", "capability conformance report"],
        ["current semantic summary missing-or-stale blocker recovery"],
        ["semantic ledger replay by run record"],
        ["future capability supersession must preserve ledger lineage"]);

    private async Task<RepositoryWorkCapabilityEvaluationRecord> EvaluateCapabilityAsync(
        RepositoryWorkCapabilityDeclarationRecord declaration,
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkProtocolDefinitionRecord protocol,
        List<string> writtenArtifacts)
    {
        IReadOnlyList<string> missing = MissingCapabilitySemantics(declaration);
        RepositoryWorkCapabilityEvaluationOutcome outcome = missing.Count == 0
            ? RepositoryWorkCapabilityEvaluationOutcome.Accepted
            : RepositoryWorkCapabilityEvaluationOutcome.BlockedForMissingSemantics;
        string decision = missing.Count == 0
            ? "accepted"
            : $"blocked-for-missing-semantics: {string.Join(", ", missing)}";
        var evaluation = new RepositoryWorkCapabilityEvaluationRecord(
            $"capability-evaluation-{ShortHash($"{declaration.CapabilityId}:{subject.SubjectId}:{protocol.ProtocolId}")}",
            declaration.CapabilityId,
            outcome,
            missing,
            RepositoryWorkSemanticArtifactPaths.CapabilityConformanceReport,
            decision,
            DateTimeOffset.UtcNow);

        await WriteTextAsync(
            RepositoryWorkSemanticArtifactPaths.CapabilityConformanceReport,
            RenderCapabilityConformanceReport(declaration, evaluation),
            writtenArtifacts);
        return evaluation;
    }

    private static IReadOnlyList<string> MissingCapabilitySemantics(RepositoryWorkCapabilityDeclarationRecord declaration)
    {
        var missing = new List<string>();
        AddIfEmpty(missing, declaration.OwnedSubjects, "subjects");
        AddIfEmpty(missing, declaration.AcceptedIntents, "intents");
        AddIfEmpty(missing, declaration.Protocols, "protocols");
        AddIfEmpty(missing, declaration.AuthorityScopes, "authority");
        AddIfEmpty(missing, declaration.SourcesAndObservations, "sources-and-observations");
        AddIfEmpty(missing, declaration.Evidence, "evidence");
        AddIfEmpty(missing, declaration.Artifacts, "artifacts");
        AddIfEmpty(missing, declaration.Decisions, "decisions");
        AddIfEmpty(missing, declaration.RelationTypes, "relations");
        AddIfEmpty(missing, declaration.States, "states");
        AddIfEmpty(missing, declaration.LifecycleMovements, "lifecycle");
        AddIfEmpty(missing, declaration.Invariants, "invariants");
        AddIfEmpty(missing, declaration.Reports, "reports");
        AddIfEmpty(missing, declaration.Recovery, "recovery");
        AddIfEmpty(missing, declaration.Replay, "replay");
        AddIfEmpty(missing, declaration.Retirement, "retirement");
        return missing;
    }

    private static void AddIfEmpty(List<string> missing, IReadOnlyList<string> values, string name)
    {
        if (values.Count == 0)
        {
            missing.Add(name);
        }
    }

    private async Task<RepositoryWorkSemanticLedgerDocument> LoadLedgerAsync()
    {
        string? content = await artifacts.ReadAsync(RepositoryWorkSemanticArtifactPaths.Ledger);
        if (string.IsNullOrWhiteSpace(content))
        {
            return RepositoryWorkSemanticLedgerDocument.Empty;
        }

        RepositoryWorkSemanticLedgerDocument ledger = DeserializeRequired<RepositoryWorkSemanticLedgerDocument>(
            content,
            RepositoryWorkSemanticArtifactPaths.Ledger);
        if (!string.Equals(ledger.SchemaVersion, RepositoryWorkSemanticLedgerDocument.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new RoadmapStepException($"RepositoryWork semantic ledger has unsupported schema version `{ledger.SchemaVersion}`.");
        }

        return ledger;
    }

    private async Task AppendLedgerAsync(
        RepositoryWorkSemanticLedgerDocument ledger,
        RepositoryWorkSemanticRunRecord run,
        List<string> writtenArtifacts)
    {
        RepositoryWorkSemanticLedgerDocument updated = ledger with
        {
            Runs = [..ledger.Runs, run],
        };
        await WriteJsonAsync(RepositoryWorkSemanticArtifactPaths.Ledger, updated, writtenArtifacts);
    }

    private static int NextVersion(RepositoryWorkSemanticLedgerDocument ledger)
    {
        int max = ledger.Runs
            .Select(run => run.Promotion?.Version ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return max + 1;
    }

    private static string? LastPromotedSummaryVersion(RepositoryWorkSemanticLedgerDocument ledger) =>
        ledger.Runs
            .Where(run => run.Promotion?.Accepted == true)
            .Select(run => run.Promotion?.AuthoritativeVersionPath)
            .LastOrDefault(path => !string.IsNullOrWhiteSpace(path));

    private async Task WriteTextAsync(string path, string content, List<string> writtenArtifacts)
    {
        await artifacts.WriteAsync(path, content.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? content
            : content + Environment.NewLine);
        writtenArtifacts.Add(path);
    }

    private async Task WriteJsonAsync<T>(string path, T value, List<string> writtenArtifacts)
    {
        await artifacts.WriteAsync(path, JsonSerializer.Serialize(value, RoadmapJson.Options) + Environment.NewLine);
        writtenArtifacts.Add(path);
    }

    private static T DeserializeRequired<T>(string content, string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, RoadmapJson.Options)
                ?? throw new RoadmapStepException($"Structured semantic artifact is empty: {path}.");
        }
        catch (JsonException exception)
        {
            throw new RoadmapStepException($"Structured semantic artifact is invalid JSON at {path}: {exception.Message}");
        }
    }

    private static string RenderCapturedSourceView(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkIntentRecord intent,
        string sourcePath,
        string hash,
        string sourceContent,
        DateTimeOffset capturedAt) =>
        $"""
        # RepositoryWork Captured Source View

        | Field | Value |
        |---|---|
        | Subject | {Escape(subject.SubjectId)} |
        | Intent | {Escape(intent.IntentId)} |
        | Origin | {Escape(sourcePath)} |
        | Snapshot SHA-256 | {hash} |
        | Freshness Rule | fresh when repository bytes match captured SHA-256 |
        | Trust Boundary | repository-authored-input |
        | Consumer Scope | {ConsumerScope} |
        | Captured At | {capturedAt:O} |
        | Source Authority | The origin remains the source authority; this artifact is only a captured representation. |

        ## Source Content

        {sourceContent}
        """;

    private static string RenderObservation(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkIntentRecord intent,
        RepositoryWorkSourceCaptureRecord source,
        RepositoryWorkAdmissionRecord admission,
        DateTimeOffset observedAt) =>
        $"""
        # RepositoryWork Interaction Observation

        | Field | Value |
        |---|---|
        | Subject | {Escape(subject.SubjectId)} |
        | Intent | {Escape(intent.IntentId)} |
        | Protocol | {Escape(admission.ProtocolId)} |
        | Admission | {admission.Outcome} |
        | Source | {Escape(source.Origin)} |
        | Source SHA-256 | {source.SnapshotHash} |
        | Observed At | {observedAt:O} |

        ## Authority Checks

        {RenderChecks(admission.AuthorityChecks)}

        ## Invariant Checks

        {RenderChecks(admission.InvariantChecks)}
        """;

    private static string RenderEvidence(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkSourceCaptureRecord source,
        RepositoryWorkInteractionRecord interaction) =>
        $"""
        # RepositoryWork Evidence

        | Field | Value |
        |---|---|
        | Subject | {Escape(subject.SubjectId)} |
        | Interaction | {Escape(interaction.InteractionId)} |
        | Observation | {Escape(interaction.Observation.ObservationId)} |
        | Validation Accepted | {interaction.Validation.Accepted} |
        | Validation Reason | {Escape(interaction.Validation.Reason)} |
        | Source Snapshot | {source.SnapshotHash} |
        | Consumer Scope | {ConsumerScope} |

        This evidence binds a raw interaction observation to RepositoryWork only after validation accepted it.
        """;

    private static string RenderBlocker(RepositoryWorkBlockerRecord blocker) =>
        $"""
        # RepositoryWork Blocker

        | Field | Value |
        |---|---|
        | Blocker | {Escape(blocker.BlockerId)} |
        | Subject | {Escape(blocker.SubjectId)} |
        | Type | {Escape(blocker.BlockerType)} |
        | Original Intent | {Escape(blocker.OriginalIntentId)} |
        | Evidence | {Escape(string.Join(", ", blocker.EvidenceIds))} |
        | Reason | {Escape(blocker.Reason)} |
        | Required Recovery | {Escape(blocker.RequiredRecovery)} |
        | Created At | {blocker.CreatedAt:O} |

        Recovery must preserve this blocker record and cannot reinterpret the original condition as success.
        """;

    private static string RenderCurrentSummary(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkIntentRecord intent,
        RepositoryWorkSourceCaptureRecord source,
        RepositoryWorkAdmissionRecord admission,
        RepositoryWorkInteractionRecord interaction,
        RepositoryWorkEvidenceRecord evidence,
        int version) =>
        $"""
        # RepositoryWork Current Semantic Summary

        | Field | Value |
        |---|---|
        | Artifact Role | current-semantic-summary |
        | Version | {version:0000} |
        | Subject | {Escape(subject.SubjectId)} |
        | Subject Type | {Escape(subject.SubjectType)} |
        | Intent | {Escape(intent.IntentId)} |
        | Protocol | {Escape(admission.ProtocolId)} |
        | Admission | {admission.Outcome} |
        | Interaction | {Escape(interaction.InteractionId)} |
        | Evidence | {Escape(evidence.EvidenceId)} |
        | Source Origin | {Escape(source.Origin)} |
        | Source SHA-256 | {source.SnapshotHash} |
        | Report Authority | none; this report does not create execution, mutation, acceptance, or certification authority. |

        ## Executable Outcome

        RepositoryWork can now execute a governed semantic interaction from subject-bound intent through admission, evidence, artifact candidate, decision, promotion, state entry, recovery review, certification, distillation, and capability evaluation.

        ## Durable Evidence

        Durable evidence is recorded in `{Escape(evidence.EvidencePath)}` and the semantic ledger.

        ## Evaluation Gate

        HITL or automation can inspect subject identity, admission checks, interaction validation, evidence binding, candidate validation, accepted decision, promoted artifact version, current state, recovery review, certification, distillation, and capability conformance report.

        ## Irreversible Commitment

        RepositoryWork identity is semantic continuity. Candidate artifacts cannot become current by being written; they require evidence-bound promotion authority.
        """;

    private static string RenderRecoveryReview(
        RepositoryWorkBlockerRecord blocker,
        RepositoryWorkRecoveryReviewRecord review) =>
        $"""
        # RepositoryWork Recovery Review

        | Field | Value |
        |---|---|
        | Recovery Review | {Escape(review.RecoveryReviewId)} |
        | Blocker | {Escape(blocker.BlockerId)} |
        | Subject | {Escape(review.SubjectId)} |
        | Recovery Intent | {Escape(review.RecoveryIntent)} |
        | Eligible | {review.Eligible} |
        | Repair Input | {Escape(review.RepairInput)} |
        | Validation Accepted | {review.ValidationAccepted} |
        | Decision | {Escape(review.Decision)} |
        | Target State | {review.TargetState} |
        | Reviewed At | {review.ReviewedAt:O} |

        The original blocker remains preserved at `{Escape(blocker.BlockerPath)}`.
        """;

    private static string RenderCurrentUnderstanding(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkEvidenceRecord evidence,
        RepositoryWorkCertificationRecord certification,
        RepositoryWorkPromotionRecord promotion,
        int version) =>
        $"""
        # RepositoryWork Current Understanding

        | Field | Value |
        |---|---|
        | Version | {version:0000} |
        | Subject | {Escape(subject.SubjectId)} |
        | Evidence | {Escape(evidence.EvidenceId)} |
        | Certification | {Escape(certification.CertificationId)} |
        | Promoted Summary | {Escape(promotion.AuthoritativeVersionPath)} |
        | Lineage Preserved | true |

        Current understanding: RepositoryWork semantic execution is governed by subject identity, protocol admission, validated evidence, candidate-to-current artifact promotion, evidence-bound decision, state agreement, recovery review, certification, distillation, and capability conformance.

        This artifact updates current understanding without rewriting historical evidence or prior promoted summaries.
        """;

    private static string RenderCapabilityConformanceReport(
        RepositoryWorkCapabilityDeclarationRecord declaration,
        RepositoryWorkCapabilityEvaluationRecord evaluation)
    {
        string missing = evaluation.MissingSemantics.Count == 0
            ? "None"
            : string.Join(", ", evaluation.MissingSemantics);
        return $"""
        # RepositoryWork Capability Conformance

        | Field | Value |
        |---|---|
        | Capability | {Escape(declaration.CapabilityName)} |
        | Capability ID | {Escape(declaration.CapabilityId)} |
        | Outcome | {evaluation.Outcome} |
        | Decision | {Escape(evaluation.Decision)} |
        | Missing Semantics | {Escape(missing)} |
        | Evaluated At | {evaluation.EvaluatedAt:O} |

        The conformance report evaluates whether the capability belongs inside the governed system. It does not grant runtime authority by itself.
        """;
    }

    private static string RenderExecutionReport(
        RepositoryWorkSubjectIdentityDocument subject,
        RepositoryWorkSemanticRunRecord run) =>
        $"""
        # RepositoryWork Semantic Report

        | Field | Value |
        |---|---|
        | Run | {Escape(run.RunId)} |
        | Subject | {Escape(subject.SubjectId)} |
        | Admission | {run.Admission.Outcome} |
        | Report Authority | none; this report cannot mutate state or grant execution authority. |
        | Started At | {run.StartedAt:O} |
        | Completed At | {run.CompletedAt:O} |

        ## Executable Outcome

        {run.ExecutableOutcome}

        ## Durable Evidence

        {run.DurableEvidence}

        ## Evaluation Gate

        {run.EvaluationGate}

        ## Irreversible Commitment

        {run.IrreversibleCommitment}

        ## Key Artifacts

        - Subject identity: `{RepositoryWorkSemanticArtifactPaths.SubjectIdentity}`
        - Protocol definition: `{RepositoryWorkSemanticArtifactPaths.ProtocolDefinition}`
        - Admission: `{run.Admission.AdmissionPath}`
        - Ledger: `{RepositoryWorkSemanticArtifactPaths.Ledger}`
        - Current summary: `{RepositoryWorkSemanticArtifactPaths.CurrentSummary}`
        - Current state: `{RepositoryWorkSemanticArtifactPaths.CurrentState}`
        - Current understanding: `{RepositoryWorkSemanticArtifactPaths.CurrentUnderstanding}`
        - Capability conformance: `{RepositoryWorkSemanticArtifactPaths.CapabilityConformanceReport}`
        """;

    private static string RenderChecks(IReadOnlyList<RepositoryWorkCheckRecord> checks)
    {
        if (checks.Count == 0)
        {
            return "None";
        }

        var lines = new List<string>
        {
            "| Check | Passed | Scope | Reason |",
            "|---|---:|---|---|",
        };
        lines.AddRange(checks.Select(check =>
            $"| {Escape(check.Name)} | {check.Passed} | {Escape(check.Scope)} | {Escape(check.Reason)} |"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeRepositoryIdentityValue(string value) =>
        value.Trim().Replace('\\', '/');

    private static string Sha256(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ShortHash(string value) => Sha256(value)[..16];

    private static string Escape(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}
