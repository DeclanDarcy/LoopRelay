using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapTransitionStatusSemanticExecutor(
    RoadmapArtifacts artifacts,
    RoadmapStateStore stateStore,
    RoadmapStartupPlanner startupPlanner,
    ILoopConsole? console = null)
{
    private const string ProtocolId = "roadmap-transition.status-report.v1";
    private const string SubjectType = "RoadmapTransition";
    private const string ConsumerScope = "roadmap-transition.status-report.semantic-evaluation";

    public async Task<RoadmapTransitionStatusSemanticExecutionResult> ExecuteAsync(
        RoadmapTransitionStatusSemanticRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        string runId = Guid.NewGuid().ToString("N");
        var writtenArtifacts = new List<string>();

        RoadmapTransitionStatusSemanticLedgerDocument ledger = await LoadLedgerAsync();
        RepositoryWorkSubjectIdentityDocument parent = await LoadParentSubjectAsync();
        RoadmapTransitionStatusSubjectIdentityDocument subject = await LoadOrCreateSubjectAsync(
            parent,
            startedAt,
            writtenArtifacts);

        RoadmapTransitionStatusProtocolDefinitionRecord protocol = CreateProtocolDefinition(parent);
        await WriteJsonAsync(RoadmapTransitionStatusSemanticArtifactPaths.ProtocolDefinition, protocol, writtenArtifacts);

        var intent = new RoadmapTransitionStatusIntentRecord(
            $"intent-{runId}",
            subject.SubjectId,
            parent.SubjectId,
            request.TransitionKey,
            request.IntentSummary,
            startedAt);

        SourceCaptureResult sourceCapture = await CaptureStateSourceAsync(
            runId,
            subject,
            intent,
            startedAt,
            writtenArtifacts);

        RoadmapTransitionStatusAdmissionRecord admission = EvaluateAdmission(
            runId,
            subject,
            intent,
            request,
            sourceCapture.Record,
            startedAt);
        await WriteJsonAsync(admission.AdmissionPath, admission, writtenArtifacts);

        if (admission.Outcome != RepositoryWorkAdmissionOutcome.ReportOnly)
        {
            return await PersistRunAsync(
                ledger,
                runId,
                startedAt,
                subject,
                intent,
                sourceCapture.Record,
                admission,
                null,
                null,
                null,
                null,
                writtenArtifacts);
        }

        string preStateMarker = sourceCapture.Record.SnapshotMarker;
        RoadmapStatusExecution legacyExecution = RoadmapStatusTransition.CreateExecution(sourceCapture.State, startupPlanner);
        string legacyObservationPath = RoadmapTransitionStatusSemanticArtifactPaths.LegacyStatusObservation(runId);
        await WriteTextAsync(
            legacyObservationPath,
            RenderStatusObservation("Legacy Roadmap Status Output", subject, intent, legacyExecution, startedAt),
            writtenArtifacts);

        var captureConsole = new CapturingLoopConsole();
        RoadmapStatusExecution semanticExecution = await new RoadmapStatusTransition(stateStore, startupPlanner)
            .ExecuteAsync(captureConsole, cancellationToken);
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        string semanticOutput = semanticExecution.RenderConsoleOutput();
        string observationPath = RoadmapTransitionStatusSemanticArtifactPaths.StatusObservation(runId);
        await WriteTextAsync(
            observationPath,
            RenderStatusObservation("Semantic Status Wrapper Observation", subject, intent, semanticExecution, observedAt),
            writtenArtifacts);
        var observation = new RoadmapTransitionStatusObservationRecord(
            $"observation-{runId}",
            subject.SubjectId,
            admission.AdmissionId,
            "legacy-status-output-as-observation",
            observationPath,
            Sha256(semanticOutput),
            semanticExecution.Outcome,
            semanticExecution.Behavior,
            observedAt);

        string postStateMarker = (await ReadEffectiveStateSourceAsync()).Marker;
        bool stateMutationDetected = !string.Equals(preStateMarker, postStateMarker, StringComparison.Ordinal);
        RoadmapTransitionStatusEquivalenceRecord equivalence = CompareEquivalence(
            runId,
            subject,
            legacyObservationPath,
            observationPath,
            legacyExecution,
            semanticExecution,
            stateMutationDetected);
        await WriteJsonAsync(equivalence.EquivalencePath, equivalence, writtenArtifacts);

        bool sourceBehaviorMatched = BehaviorMatches(sourceCapture.Record.ExpectedBehavior, semanticExecution.Behavior);
        bool validationAccepted = equivalence.Accepted && sourceBehaviorMatched && !stateMutationDetected;
        string validationReason = validationAccepted
            ? "Semantic status observation matched the captured source snapshot, legacy status behavior, and pre/post state marker."
            : ValidationFailureReason(equivalence, sourceBehaviorMatched, stateMutationDetected);

        RoadmapTransitionStatusEvidenceRecord? evidence = null;
        RoadmapTransitionStatusDecisionRecord? decision = null;
        if (validationAccepted)
        {
            string evidencePath = RoadmapTransitionStatusSemanticArtifactPaths.Evidence(runId);
            string evidenceContent = RenderEvidence(
                subject,
                sourceCapture.Record,
                observation,
                equivalence,
                validationReason);
            await WriteTextAsync(evidencePath, evidenceContent, writtenArtifacts);
            evidence = new RoadmapTransitionStatusEvidenceRecord(
                $"evidence-{runId}",
                subject.SubjectId,
                observation.ObservationId,
                sourceCapture.Record.CaptureId,
                ConsumerScope,
                evidencePath,
                Sha256(evidenceContent),
                true,
                validationReason,
                DateTimeOffset.UtcNow);

            string decisionPath = RoadmapTransitionStatusSemanticArtifactPaths.Decision(runId);
            decision = new RoadmapTransitionStatusDecisionRecord(
                $"decision-{runId}",
                subject.SubjectId,
                evidence.EvidenceId,
                "report-only-transition",
                "emit-report-only",
                "report artifact only",
                preStateMarker,
                postStateMarker,
                false,
                decisionPath,
                DateTimeOffset.UtcNow);
            await WriteJsonAsync(decisionPath, decision, writtenArtifacts);
        }

        return await PersistRunAsync(
            ledger,
            runId,
            startedAt,
            subject,
            intent,
            sourceCapture.Record,
            admission,
            observation,
            evidence,
            decision,
            equivalence,
            writtenArtifacts);
    }

    private async Task<RepositoryWorkSubjectIdentityDocument> LoadParentSubjectAsync()
    {
        string? content = await artifacts.ReadAsync(RepositoryWorkSemanticArtifactPaths.SubjectIdentity);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new RoadmapStepException(
                $"RepositoryWork parent subject identity is required before wrapping roadmap transitions: {RepositoryWorkSemanticArtifactPaths.SubjectIdentity}");
        }

        RepositoryWorkSubjectIdentityDocument subject = DeserializeRequired<RepositoryWorkSubjectIdentityDocument>(
            content,
            RepositoryWorkSemanticArtifactPaths.SubjectIdentity);
        if (!string.Equals(subject.SchemaVersion, RepositoryWorkSubjectIdentityDocument.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new RoadmapStepException(
                $"RepositoryWork parent subject identity has unsupported schema version `{subject.SchemaVersion}`.");
        }

        return subject;
    }

    private async Task<RoadmapTransitionStatusSubjectIdentityDocument> LoadOrCreateSubjectAsync(
        RepositoryWorkSubjectIdentityDocument parent,
        DateTimeOffset now,
        List<string> writtenArtifacts)
    {
        string? existing = await artifacts.ReadAsync(RoadmapTransitionStatusSemanticArtifactPaths.SubjectIdentity);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            RoadmapTransitionStatusSubjectIdentityDocument loaded =
                DeserializeRequired<RoadmapTransitionStatusSubjectIdentityDocument>(
                    existing,
                    RoadmapTransitionStatusSemanticArtifactPaths.SubjectIdentity);
            if (!string.Equals(loaded.SchemaVersion, RoadmapTransitionStatusSubjectIdentityDocument.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new RoadmapStepException(
                    $"RoadmapTransition:StatusReport subject identity has unsupported schema version `{loaded.SchemaVersion}`.");
            }

            if (!string.Equals(loaded.ParentSubjectId, parent.SubjectId, StringComparison.Ordinal))
            {
                throw new RoadmapStepException(
                    "RoadmapTransition:StatusReport subject identity belongs to a different RepositoryWork parent.");
            }

            return loaded;
        }

        string subjectId = $"repository-work:{ShortHash(parent.SubjectId)}:roadmap-transition-status-report:{ShortHash($"{parent.SubjectId}:{SubjectType}:StatusReport")}";
        var subject = new RoadmapTransitionStatusSubjectIdentityDocument(
            RoadmapTransitionStatusSubjectIdentityDocument.CurrentSchemaVersion,
            subjectId,
            parent.SubjectId,
            parent.SubjectType,
            SubjectType,
            RoadmapTransitionStatusSemanticRequest.SupportedTransitionKey,
            "RepositoryWork owns semantic authority for RoadmapTransition:StatusReport.",
            CreateAuthorityScopeDeclarations(),
            [
                "IntentCaptured",
                "SourceCaptured",
                "ProtocolAdmitted",
                "ObservationCaptured",
                "EvidenceBound",
                "ReportOnlyDecisionAccepted",
                "EquivalenceAccepted",
                "Reported",
            ],
            CreateInvariantDeclarations(),
            now,
            now);

        await WriteJsonAsync(RoadmapTransitionStatusSemanticArtifactPaths.SubjectIdentity, subject, writtenArtifacts);
        return subject;
    }

    private static IReadOnlyList<RoadmapTransitionStatusAuthorityScope> CreateAuthorityScopeDeclarations() =>
    [
        new(RoadmapTransitionStatusAuthorityScopes.ParentRepositoryWork, "protocol-admission", "RepositoryWork", "Admit child RoadmapTransition status reports under RepositoryWork authority."),
        new(RoadmapTransitionStatusAuthorityScopes.RoadmapStateRead, "permission", "RoadmapTransition:StatusReport", "Read current roadmap state source for report-only status."),
        new(RoadmapTransitionStatusAuthorityScopes.LegacyStatusObservation, "observation", "RoadmapTransition:StatusReport", "Execute legacy status behavior as non-authoritative observation."),
        new(RoadmapTransitionStatusAuthorityScopes.Report, "report", "RoadmapTransition:StatusReport", "Emit semantic transition reports without mutation authority."),
    ];

    private static IReadOnlyList<RoadmapTransitionStatusInvariantDeclaration> CreateInvariantDeclarations() =>
    [
        new("child-subject-requires-parent", "RoadmapTransition:StatusReport is governed only as a child of RepositoryWork."),
        new("protocol-admission-before-status-execution", "Status behavior cannot execute through the semantic wrapper before protocol admission."),
        new("report-output-is-observation-before-evidence", "Rendered status output is observation until validation binds it as evidence."),
        new("report-only-does-not-mutate-roadmap-state", "Report-only status decisions authorize report artifacts only."),
        new("behavior-equivalence-before-legacy-retirement", "Legacy status behavior remains available and cannot be retired by this wrapper."),
    ];

    private static RoadmapTransitionStatusProtocolDefinitionRecord CreateProtocolDefinition(
        RepositoryWorkSubjectIdentityDocument parent) => new(
        RoadmapTransitionStatusProtocolDefinitionRecord.CurrentSchemaVersion,
        ProtocolId,
        parent.SubjectId,
        SubjectType,
        RoadmapTransitionStatusSemanticRequest.SupportedTransitionKey,
        "Subject-bound intent to produce a report-only semantic view of the current roadmap state.",
        ["RepositoryWork parent subject", "RoadmapTransition:StatusReport child subject", "current roadmap state source snapshot", "report-only authority scopes"],
        ["Missing state is a valid source condition.", "Present state must parse before execution.", "Pre/post state source markers must match."],
        RoadmapTransitionStatusAuthorityScopes.DefaultReportOnlyScopes,
        CreateInvariantDeclarations().Select(invariant => invariant.InvariantId).ToArray(),
        [
            RepositoryWorkAdmissionOutcome.ReportOnly,
            RepositoryWorkAdmissionOutcome.Blocked,
            RepositoryWorkAdmissionOutcome.Denied,
            RepositoryWorkAdmissionOutcome.Unsupported,
        ]);

    private async Task<SourceCaptureResult> CaptureStateSourceAsync(
        string runId,
        RoadmapTransitionStatusSubjectIdentityDocument subject,
        RoadmapTransitionStatusIntentRecord intent,
        DateTimeOffset capturedAt,
        List<string> writtenArtifacts)
    {
        EffectiveStateSource source = await ReadEffectiveStateSourceAsync();
        RoadmapStateDocument? state = null;
        string? parseError = null;
        bool parsed = true;
        if (string.Equals(source.Condition, "present", StringComparison.Ordinal))
        {
            try
            {
                state = await stateStore.LoadReadOnlyAsync();
            }
            catch (RoadmapStepException exception)
            {
                parsed = false;
                parseError = exception.Message;
            }
        }

        string sourceCondition = parseError is null ? source.Condition : "malformed";
        RoadmapStatusBehaviorFields expectedBehavior = parsed
            ? RoadmapStatusTransition.CreateExecution(state, startupPlanner).Behavior
            : new RoadmapStatusBehaviorFields(
                sourceCondition,
                null,
                parseError ?? "Current roadmap state could not be parsed.",
                "Unknown",
                null,
                [],
                "Blocked");
        string path = RoadmapTransitionStatusSemanticArtifactPaths.CurrentRoadmapStateSource(runId);
        var record = new RoadmapTransitionStatusSourceSnapshotRecord(
            $"source-{runId}",
            subject.SubjectId,
            intent.IntentId,
            source.Path,
            sourceCondition,
            source.Marker,
            source.Hash,
            source.Bytes,
            parsed,
            parseError,
            expectedBehavior,
            "fresh when pre/post effective roadmap state source markers match",
            "roadmap-state-store-source",
            path,
            capturedAt);
        await WriteJsonAsync(path, record, writtenArtifacts);
        return new SourceCaptureResult(record, state);
    }

    private async Task<EffectiveStateSource> ReadEffectiveStateSourceAsync()
    {
        string? structured = await artifacts.ReadAsync(RoadmapArtifactPaths.StateJson);
        if (!string.IsNullOrWhiteSpace(structured))
        {
            return EffectiveStateSource.Present(RoadmapArtifactPaths.StateJson, structured);
        }

        string? legacy = await artifacts.ReadAsync(RoadmapArtifactPaths.State);
        if (!string.IsNullOrWhiteSpace(legacy))
        {
            return EffectiveStateSource.Present(RoadmapArtifactPaths.State, legacy);
        }

        if (structured is not null)
        {
            return EffectiveStateSource.Empty(RoadmapArtifactPaths.StateJson);
        }

        if (legacy is not null)
        {
            return EffectiveStateSource.Empty(RoadmapArtifactPaths.State);
        }

        return EffectiveStateSource.Missing(RoadmapArtifactPaths.StateJson);
    }

    private RoadmapTransitionStatusAdmissionRecord EvaluateAdmission(
        string runId,
        RoadmapTransitionStatusSubjectIdentityDocument subject,
        RoadmapTransitionStatusIntentRecord intent,
        RoadmapTransitionStatusSemanticRequest request,
        RoadmapTransitionStatusSourceSnapshotRecord source,
        DateTimeOffset createdAt)
    {
        IReadOnlyList<string> requiredScopes = RoadmapTransitionStatusAuthorityScopes.DefaultReportOnlyScopes;
        IReadOnlyList<RoadmapTransitionStatusCheckRecord> authorityChecks = requiredScopes
            .Select(scope => new RoadmapTransitionStatusCheckRecord(
                $"authority:{scope}",
                request.AuthorityScopes.Contains(scope, StringComparer.Ordinal),
                scope,
                request.AuthorityScopes.Contains(scope, StringComparer.Ordinal)
                    ? "Requested authority includes the required report-only scope."
                    : "Requested authority is missing the required report-only scope."))
            .ToArray();

        IReadOnlyList<string> forbiddenScopes = request.AuthorityScopes
            .Where(scope => RoadmapTransitionStatusAuthorityScopes.ForbiddenReportOnlyScopes.Contains(scope, StringComparer.Ordinal))
            .ToArray();
        IReadOnlyList<RoadmapTransitionStatusCheckRecord> invariantChecks =
        [
            new("child-subject-requires-parent", !string.IsNullOrWhiteSpace(subject.ParentSubjectId), subject.ParentSubjectId, "Child subject is bound to a RepositoryWork parent."),
            new("protocol-admission-before-status-execution", true, ProtocolId, "Admission is evaluated before legacy status execution."),
            new("report-output-is-observation-before-evidence", true, RoadmapTransitionStatusAuthorityScopes.Report, "Report output will be captured as observation before evidence binding."),
            new(
                "report-only-does-not-mutate-roadmap-state",
                forbiddenScopes.Count == 0,
                forbiddenScopes.Count == 0 ? "none" : string.Join(", ", forbiddenScopes),
                forbiddenScopes.Count == 0
                    ? "No mutation or execution authority was requested."
                    : "Report-only status cannot request mutation, execution, acceptance, state-entry, promotion, or certification authority."),
            new("behavior-equivalence-before-legacy-retirement", true, "legacy-status", "Legacy status remains available and equivalence will be recorded."),
        ];

        RoadmapTransitionStatusCheckRecord sourceFreshness = new(
            "source-freshness",
            source.Parsed,
            source.StateSourcePath,
            source.Parsed
                ? $"Current roadmap state source condition `{source.SourceCondition}` is acceptable for report-only status."
                : source.ParseError ?? "Current roadmap state source could not be parsed.");

        RepositoryWorkAdmissionOutcome outcome;
        string reason;
        if (!string.Equals(request.TransitionKey, RoadmapTransitionStatusSemanticRequest.SupportedTransitionKey, StringComparison.Ordinal))
        {
            outcome = RepositoryWorkAdmissionOutcome.Unsupported;
            reason = $"Protocol `{ProtocolId}` does not support transition key `{request.TransitionKey}`.";
        }
        else if (!sourceFreshness.Passed)
        {
            outcome = RepositoryWorkAdmissionOutcome.Blocked;
            reason = sourceFreshness.Reason;
        }
        else if (authorityChecks.Any(check => !check.Passed))
        {
            outcome = RepositoryWorkAdmissionOutcome.Denied;
            reason = "Requested authority does not satisfy report-only status scope requirements.";
        }
        else if (invariantChecks.Any(check => !check.Passed))
        {
            outcome = RepositoryWorkAdmissionOutcome.Denied;
            reason = "One or more report-only transition invariants failed.";
        }
        else
        {
            outcome = RepositoryWorkAdmissionOutcome.ReportOnly;
            reason = "Report-only status transition admitted; no mutation, acceptance, recovery, or certification authority granted.";
        }

        return new RoadmapTransitionStatusAdmissionRecord(
            $"admission-{runId}",
            ProtocolId,
            subject.SubjectId,
            intent.IntentId,
            request.TransitionKey,
            outcome,
            reason,
            authorityChecks,
            invariantChecks,
            sourceFreshness,
            RoadmapTransitionStatusSemanticArtifactPaths.Admission(runId),
            createdAt);
    }

    private async Task<RoadmapTransitionStatusSemanticExecutionResult> PersistRunAsync(
        RoadmapTransitionStatusSemanticLedgerDocument ledger,
        string runId,
        DateTimeOffset startedAt,
        RoadmapTransitionStatusSubjectIdentityDocument subject,
        RoadmapTransitionStatusIntentRecord intent,
        RoadmapTransitionStatusSourceSnapshotRecord source,
        RoadmapTransitionStatusAdmissionRecord admission,
        RoadmapTransitionStatusObservationRecord? observation,
        RoadmapTransitionStatusEvidenceRecord? evidence,
        RoadmapTransitionStatusDecisionRecord? decision,
        RoadmapTransitionStatusEquivalenceRecord? equivalence,
        List<string> writtenArtifacts)
    {
        string reportPath = RoadmapTransitionStatusSemanticArtifactPaths.RunReport(runId);
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        var run = new RoadmapTransitionStatusSemanticRunRecord(
            runId,
            startedAt,
            completedAt,
            admission.Outcome == RepositoryWorkAdmissionOutcome.ReportOnly
                ? "RoadmapTransition:StatusReport executed through RepositoryWork report-only semantic admission."
                : $"RoadmapTransition:StatusReport did not execute because admission ended as {admission.Outcome}.",
            evidence is null
                ? $"Admission record persisted at `{admission.AdmissionPath}`; no evidence was bound."
                : $"Evidence is bound at `{evidence.EvidencePath}` and equivalence is recorded at `{equivalence?.EquivalencePath}`.",
            "Inspect the authoritative semantic report to follow admission, source snapshot, observation, validation, evidence, decision, equivalence, and report-only state non-mutation.",
            "RoadmapTransition:StatusReport is now a governed child subject of RepositoryWork; legacy status behavior remains available.",
            intent,
            source,
            admission,
            observation,
            evidence,
            decision,
            equivalence,
            reportPath);

        string report = RenderExecutionReport(subject, run);
        await WriteTextAsync(reportPath, report, writtenArtifacts);
        await WriteTextAsync(RoadmapTransitionStatusSemanticArtifactPaths.LatestReport, report, writtenArtifacts);
        await AppendLedgerAsync(ledger, run, writtenArtifacts);

        console?.Info($"RoadmapTransition:StatusReport semantic run completed: {admission.Outcome}");
        console?.Info($"Semantic transition report: {reportPath}");

        bool completed = admission.Outcome == RepositoryWorkAdmissionOutcome.ReportOnly &&
            evidence is not null &&
            decision is not null &&
            equivalence?.Accepted == true;

        return new RoadmapTransitionStatusSemanticExecutionResult(
            admission.Outcome,
            subject.SubjectId,
            runId,
            reportPath,
            completed,
            writtenArtifacts.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static RoadmapTransitionStatusEquivalenceRecord CompareEquivalence(
        string runId,
        RoadmapTransitionStatusSubjectIdentityDocument subject,
        string legacyObservationPath,
        string semanticObservationPath,
        RoadmapStatusExecution legacyExecution,
        RoadmapStatusExecution semanticExecution,
        bool stateMutationDetected)
    {
        RoadmapTransitionStatusEquivalenceFieldComparison[] comparisons =
        [
            Compare("persisted-roadmap-state", StateValue(legacyExecution.Behavior), StateValue(semanticExecution.Behavior)),
            Compare("startup-plan-reason", legacyExecution.Behavior.StartupPlanReason, semanticExecution.Behavior.StartupPlanReason),
            Compare("transition-intent", IntentValue(legacyExecution.Behavior), IntentValue(semanticExecution.Behavior)),
            Compare("blockers", BlockerValue(legacyExecution.Behavior.Blockers), BlockerValue(semanticExecution.Behavior.Blockers)),
            Compare("terminal-outcome-classification", legacyExecution.Behavior.TerminalOutcome, semanticExecution.Behavior.TerminalOutcome),
            Compare("raw-status-output", legacyExecution.RenderConsoleOutput(), semanticExecution.RenderConsoleOutput()),
            new(
                "roadmap-state-non-mutation",
                "unchanged",
                stateMutationDetected ? "changed" : "unchanged",
                !stateMutationDetected),
        ];
        string[] divergences = comparisons
            .Where(comparison => !comparison.Matched)
            .Select(comparison => comparison.Field)
            .ToArray();

        return new RoadmapTransitionStatusEquivalenceRecord(
            $"equivalence-{runId}",
            subject.SubjectId,
            legacyObservationPath,
            semanticObservationPath,
            divergences.Length == 0,
            comparisons,
            divergences,
            RoadmapTransitionStatusSemanticArtifactPaths.Equivalence(runId),
            DateTimeOffset.UtcNow);
    }

    private static RoadmapTransitionStatusEquivalenceFieldComparison Compare(
        string field,
        string legacyValue,
        string semanticValue) =>
        new(field, legacyValue, semanticValue, string.Equals(legacyValue, semanticValue, StringComparison.Ordinal));

    private static bool BehaviorMatches(RoadmapStatusBehaviorFields expected, RoadmapStatusBehaviorFields actual) =>
        string.Equals(StateValue(expected), StateValue(actual), StringComparison.Ordinal) &&
        string.Equals(expected.StartupPlanReason, actual.StartupPlanReason, StringComparison.Ordinal) &&
        string.Equals(IntentValue(expected), IntentValue(actual), StringComparison.Ordinal) &&
        string.Equals(BlockerValue(expected.Blockers), BlockerValue(actual.Blockers), StringComparison.Ordinal) &&
        string.Equals(expected.TerminalOutcome, actual.TerminalOutcome, StringComparison.Ordinal);

    private static string ValidationFailureReason(
        RoadmapTransitionStatusEquivalenceRecord equivalence,
        bool sourceBehaviorMatched,
        bool stateMutationDetected)
    {
        var reasons = new List<string>();
        if (!equivalence.Accepted)
        {
            reasons.Add($"equivalence diverged: {string.Join(", ", equivalence.Divergences)}");
        }

        if (!sourceBehaviorMatched)
        {
            reasons.Add("semantic observation did not match the captured source snapshot");
        }

        if (stateMutationDetected)
        {
            reasons.Add("roadmap state source marker changed during report-only execution");
        }

        return reasons.Count == 0 ? "validation failed" : string.Join("; ", reasons);
    }

    private async Task<RoadmapTransitionStatusSemanticLedgerDocument> LoadLedgerAsync()
    {
        string? content = await artifacts.ReadAsync(RoadmapTransitionStatusSemanticArtifactPaths.Ledger);
        if (string.IsNullOrWhiteSpace(content))
        {
            return RoadmapTransitionStatusSemanticLedgerDocument.Empty;
        }

        RoadmapTransitionStatusSemanticLedgerDocument ledger =
            DeserializeRequired<RoadmapTransitionStatusSemanticLedgerDocument>(
                content,
                RoadmapTransitionStatusSemanticArtifactPaths.Ledger);
        if (!string.Equals(ledger.SchemaVersion, RoadmapTransitionStatusSemanticLedgerDocument.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new RoadmapStepException(
                $"RoadmapTransition:StatusReport semantic ledger has unsupported schema version `{ledger.SchemaVersion}`.");
        }

        return ledger;
    }

    private async Task AppendLedgerAsync(
        RoadmapTransitionStatusSemanticLedgerDocument ledger,
        RoadmapTransitionStatusSemanticRunRecord run,
        List<string> writtenArtifacts)
    {
        RoadmapTransitionStatusSemanticLedgerDocument updated = ledger with
        {
            Runs = [..ledger.Runs, run],
        };
        await WriteJsonAsync(RoadmapTransitionStatusSemanticArtifactPaths.Ledger, updated, writtenArtifacts);
    }

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

    private static string RenderStatusObservation(
        string title,
        RoadmapTransitionStatusSubjectIdentityDocument subject,
        RoadmapTransitionStatusIntentRecord intent,
        RoadmapStatusExecution execution,
        DateTimeOffset capturedAt) =>
        $"""
        # {title}

        | Field | Value |
        |---|---|
        | Subject | {Escape(subject.SubjectId)} |
        | Parent Subject | {Escape(subject.ParentSubjectId)} |
        | Intent | {Escape(intent.IntentId)} |
        | Transition Key | {Escape(intent.TransitionKey)} |
        | Outcome | {execution.Outcome} |
        | State Condition | {Escape(execution.Behavior.StateCondition)} |
        | Current State | {Escape(execution.Behavior.CurrentState ?? "None")} |
        | Startup Plan Reason | {Escape(execution.Behavior.StartupPlanReason)} |
        | Transition Intent | {Escape(IntentValue(execution.Behavior))} |
        | Terminal Outcome Classification | {Escape(execution.Behavior.TerminalOutcome)} |
        | Captured At | {capturedAt:O} |
        | Observation Authority | none; rendered status output is not evidence until validation binds it. |

        ## Raw Status Output

        ```text
        {execution.RenderConsoleOutput()}
        ```
        """;

    private static string RenderEvidence(
        RoadmapTransitionStatusSubjectIdentityDocument subject,
        RoadmapTransitionStatusSourceSnapshotRecord source,
        RoadmapTransitionStatusObservationRecord observation,
        RoadmapTransitionStatusEquivalenceRecord equivalence,
        string validationReason) =>
        $"""
        # RoadmapTransition:StatusReport Evidence

        | Field | Value |
        |---|---|
        | Subject | {Escape(subject.SubjectId)} |
        | Parent Subject | {Escape(subject.ParentSubjectId)} |
        | Source Snapshot | {Escape(source.CaptureId)} |
        | State Source | {Escape(source.StateSourcePath)} |
        | Source Marker | {Escape(source.SnapshotMarker)} |
        | Observation | {Escape(observation.ObservationId)} |
        | Raw Observation | {Escape(observation.RawObservationPath)} |
        | Equivalence | {Escape(equivalence.EquivalenceId)} |
        | Validation Accepted | {equivalence.Accepted} |
        | Validation Reason | {Escape(validationReason)} |
        | Consumer Scope | {ConsumerScope} |

        This evidence binds raw legacy status output to the child transition only after source, equivalence, and non-mutation validation accepted it.
        """;

    private static string RenderExecutionReport(
        RoadmapTransitionStatusSubjectIdentityDocument subject,
        RoadmapTransitionStatusSemanticRunRecord run)
    {
        string evidence = run.Evidence is null ? "None" : run.Evidence.EvidencePath;
        string decision = run.Decision is null ? "None" : run.Decision.DecisionPath;
        string equivalence = run.Equivalence is null ? "None" : run.Equivalence.EquivalencePath;
        string observation = run.Observation is null ? "None" : run.Observation.RawObservationPath;
        string stateMutation = run.Decision is null
            ? "Not evaluated"
            : run.Decision.StateMutationDetected ? "Detected" : "Not detected";
        return $"""
        # RoadmapTransition:StatusReport Semantic Report

        | Field | Value |
        |---|---|
        | Run | {Escape(run.RunId)} |
        | Parent Subject | {Escape(subject.ParentSubjectId)} |
        | Child Subject | {Escape(subject.SubjectId)} |
        | Subject Type | {Escape(subject.SubjectType)} |
        | Transition Key | {Escape(subject.TransitionKey)} |
        | Protocol | {ProtocolId} |
        | Admission | {run.Admission.Outcome} |
        | Admission Reason | {Escape(run.Admission.Reason)} |
        | State Source | {Escape(run.SourceSnapshot.StateSourcePath)} |
        | State Source Condition | {Escape(run.SourceSnapshot.SourceCondition)} |
        | State Mutation | {stateMutation} |
        | Evidence | {Escape(evidence)} |
        | Decision | {Escape(decision)} |
        | Equivalence | {Escape(equivalence)} |
        | Started At | {run.StartedAt:O} |
        | Completed At | {run.CompletedAt:O} |
        | Report Authority | none; this report cannot mutate state or grant execution authority. |

        ## Executable Outcome

        {run.ExecutableOutcome}

        ## Durable Evidence

        {run.DurableEvidence}

        ## Evaluation Gate

        {run.EvaluationGate}

        ## Irreversible Commitment

        {run.IrreversibleCommitment}

        ## Current State View

        | Field | Value |
        |---|---|
        | Persisted State Condition | {Escape(run.SourceSnapshot.ExpectedBehavior.StateCondition)} |
        | Current State | {Escape(run.SourceSnapshot.ExpectedBehavior.CurrentState ?? "None")} |
        | Startup Plan Reason | {Escape(run.SourceSnapshot.ExpectedBehavior.StartupPlanReason)} |
        | Transition Intent | {Escape(IntentValue(run.SourceSnapshot.ExpectedBehavior))} |
        | Blockers | {Escape(BlockerValue(run.SourceSnapshot.ExpectedBehavior.Blockers))} |
        | Terminal Outcome Classification | {Escape(run.SourceSnapshot.ExpectedBehavior.TerminalOutcome)} |

        ## Semantic Path

        - Subject identity: `{RoadmapTransitionStatusSemanticArtifactPaths.SubjectIdentity}`
        - Protocol definition: `{RoadmapTransitionStatusSemanticArtifactPaths.ProtocolDefinition}`
        - Admission: `{run.Admission.AdmissionPath}`
        - Source snapshot: `{run.SourceSnapshot.CapturedArtifactPath}`
        - Raw observation: `{observation}`
        - Evidence: `{evidence}`
        - Decision: `{decision}`
        - Equivalence: `{equivalence}`
        - Ledger: `{RoadmapTransitionStatusSemanticArtifactPaths.Ledger}`
        """;
    }

    private static string StateValue(RoadmapStatusBehaviorFields behavior) =>
        $"{behavior.StateCondition}:{behavior.CurrentState ?? "None"}";

    private static string IntentValue(RoadmapStatusBehaviorFields behavior) =>
        $"{behavior.TransitionIntent} -> {behavior.DispatchState ?? "None"}";

    private static string BlockerValue(IReadOnlyList<RoadmapStatusBlockerField> blockers) =>
        blockers.Count == 0
            ? "None"
            : string.Join("; ", blockers.Select(blocker => $"{blocker.Blocker} Required next step: {blocker.RequiredNextStep}"));

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

    private sealed record SourceCaptureResult(
        RoadmapTransitionStatusSourceSnapshotRecord Record,
        RoadmapStateDocument? State);

    private sealed record EffectiveStateSource(
        string Path,
        string Condition,
        string Marker,
        string? Hash,
        long Bytes)
    {
        public static EffectiveStateSource Present(string path, string content)
        {
            string hash = Sha256(content);
            return new(path, "present", $"{path}:present:{hash}", hash, Encoding.UTF8.GetByteCount(content));
        }

        public static EffectiveStateSource Empty(string path) => new(path, "empty", $"{path}:empty", null, 0);

        public static EffectiveStateSource Missing(string path) => new(path, "missing", $"{path}:missing", null, 0);
    }

    private sealed class CapturingLoopConsole : ILoopConsole
    {
        public void Phase(string phase)
        {
        }

        public void Message(string content)
        {
        }

        public void Delta(string text)
        {
        }

        public void Tool(string summary)
        {
        }

        public void Info(string text)
        {
        }

        public void Warn(string text)
        {
        }

        public void Error(string text)
        {
        }
    }
}
