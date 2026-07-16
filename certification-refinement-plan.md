# Certification session-log diagnosis refinement plan

## Objective

Refine the certification fixture program so that, when a live certification run fails and does not satisfy a narrowly defined high-confidence bypass policy, the failed case is retained and the relevant agent session log is inspected before anyone reruns the fixture or starts guessing at fixes.

The motivating failure defines the required vertical slice:

- certification reported that `.agents/handoffs/handoff.md` was missing;
- the relevant Codex session showed a successful write to `agents/handoffs/handoff.md`;
- the retained case contained the wrong path and did not contain the required path; and
- the diagnosis identified the omitted leading dot as the first observed contract divergence.

The refinement is intentionally narrow. It is not a general-purpose observability system and it does not inspect every provider session.

## Required workflow

```text
live certification command fails
  -> retain the failed case and attempt result
  -> apply the structured diagnosis-bypass policy
     -> bypass matches: report the existing explanation; do not read the rollout
     -> bypass does not match: correlate the failed invocation with its provider telemetry
        -> resolve the relevant thread, turn, and rollout
        -> extract a bounded private session segment
        -> inspect the failure, segment, retained case, and relevant source
        -> attach a cited diagnosis, or an explicit inconclusive/unavailable result
  -> do not automatically repeat an unexplained failure until the diagnostic attempt reaches a terminal disposition
```

This replaces guess → wait for another live run → guess again with inspect → diagnose → fix → verify once.

## Scope

This work must provide:

1. automatic retention of a failed live certification case, without requiring `--retain-case`;
2. reliable correlation from the failed certification invocation to the relevant provider thread, turn, and rollout;
3. on-demand private reading of only the session segment relevant to the failure;
4. a read-only diagnostic pass over the failure result, bounded session evidence, retained case, and relevant source;
5. a diagnosis attached to the failed attempt, with facts, inferences, and evidence citations kept distinct; and
6. a guard against blind automatic repeats of unexplained failures until diagnosis reaches a terminal disposition.

Apply the shared behavior to every live certification runner. Implement and prove it first through the full-chain runner and the motivating wrong-path fixture before generalizing it.

## Explicit non-goals

- Do not parse rollout logs for successful transitions.
- Do not parse a rollout merely because a provider session occurred.
- Do not invoke diagnosis for a failure that already has a confident, actionable classification.
- Do not introduce full-tree pre/post hashing or a general `CertificationStateObserver`.
- Do not build a broad deterministic-rule taxonomy or general forensic rule engine.
- Do not make diagnostic-agent prose the certification pass/fail oracle.
- Do not expose hidden or encrypted reasoning.
- Do not broaden the public runtime-recovery rollout projection to expose raw tool data.
- Do not let the diagnostic pass repair the product, modify the retained fixture case, commit, publish, or rerun certification.
- Do not make a public incident-bundle program, retention service, or governance system a prerequisite for this feature.
- Do not replace the existing failure-classification system in the first implementation. `ProductRegression` remains a routing classification, not a root-cause diagnosis.

## Existing capabilities to reuse

- [`FullChainLiveRunner.cs`](src/LoopRelay.Certification/FullChainLiveRunner.cs) owns the full-chain transition loop, result construction, and current conditional case retention.
- [`LiveProviderFailureClassifier.cs`](src/LoopRelay.Certification/LiveProviderFailureClassifier.cs) already recognizes the narrow quota-exhaustion condition. Its fallback `ProductRegression` result is a useful signal that diagnosis may be required, but it is not itself an explanation.
- [`SessionTelemetryRecord.cs`](src/LoopRelay.Cli/Models/SessionTelemetryRecord.cs) already records `CodexLogPath`, session identity, provider thread ID, provider turn ID, timestamps, model, and transport.
- [`SessionTelemetryRecorder.cs`](src/LoopRelay.Cli/Services/Telemetry/SessionTelemetryRecorder.cs) already resolves and records the rollout path when an exact provider thread ID is available.
- [`CodexRolloutRepository.cs`](src/LoopRelay.Agents/Services/Codex/CodexRolloutRepository.cs) already resolves an exact rollout by provider thread ID and distinguishes complete, partial, absent, corrupt, permission-denied, and ambiguous results.
- [`FileSystemCodexRolloutLocator.cs`](src/LoopRelay.Cli/Services/Agents/FileSystemCodexRolloutLocator.cs) provides an existing time-and-workspace fallback that may be considered during later hardening, but is not part of the first vertical slice.
- [`EvidencePolicy.cs`](src/LoopRelay.Certification/EvidencePolicy.cs) provides existing normalization and privacy scanning for anything deliberately promoted from the private failure record.
- [`docs/certification.md`](docs/certification.md) already establishes certification as post-epic hardening rather than part of ordinary run-all-tests verification.

The public projection emitted by `CodexRolloutRepository` deliberately reduces tool calls to safe summaries. Preserve that boundary. Certification diagnosis needs a separate private, on-demand reader rather than more detail in the public recovery record.

## When session inspection runs

Introduce one shared decision point before any rollout is opened:

```csharp
Task<CertificationDiagnosisOutcome> DiagnoseIfNeededAsync(
    CertificationFailureContext context,
    CancellationToken cancellationToken);
```

The gate must use structured result metadata and an explicit bypass allowlist, not attempt to judge the semantic quality of natural-language explanations.

Diagnosis is skipped only when:

- the command did not invoke a live provider and therefore has no relevant provider session; or
- the failure classification is on the bypass allowlist, has high confidence, is supported by deterministic evidence already in the result, and provides an actionable next step that does not require session inspection.

For the first vertical slice, the bypass allowlist contains only confirmed quota exhaustion. The mechanical rule is therefore:

```text
if no live provider was invoked:
    NotNeeded
else if quota exhaustion is deterministically confirmed:
    NotNeeded
else:
    diagnose
```

Successful transitions never enter failure diagnosis. A live failure classified generically as `ProductRegression`, a non-allowlisted classification, or an allowlisted classification missing the required confidence, evidence, or next action must be diagnosed. Diagnosis is also required when the release gate requests adjudication of an unexplained failure or the operator explicitly requests it for an attempt record.

An explicit request may repeat diagnosis over already retained evidence, but must not rerun the certification fixture.

`NotNeeded` records the structured bypass reason and existing explanation, but does not read telemetry or a rollout and does not invoke a diagnostic agent. Expanding the bypass allowlist requires a focused policy change and tests; runners must not add ad hoc exceptions.

## Minimal identity and correlation

Add a certification invocation ID at the boundary where the harness starts the CLI process. Carry it into the attempt result and telemetry selection. Reuse the identities already emitted by CLI telemetry rather than inventing a larger identity hierarchy:

- certification invocation ID;
- CLI session ID and turn index;
- provider thread ID and provider turn ID; and
- recorded rollout path.

Correlation order for the first vertical slice:

1. select telemetry belonging to the failed CLI invocation;
2. read its provider thread ID, provider turn ID, and recorded rollout path;
3. resolve the rollout independently by exact provider thread ID;
4. if exact thread resolution and the recorded path both exist, require them to identify the same canonical file;
5. if they disagree, return `Ambiguous` and open neither as authoritative evidence;
6. if only exact thread resolution exists, use it with `ExactThread` as the resolution method;
7. if only the telemetry-recorded path exists and it is within the allowed Codex log root, use it with `RecordedPath` as the resolution method; and
8. select the recorded provider turn within the resolved rollout.

If the rollout resolves but the recorded provider turn is absent, return a distinct `TurnAbsent` outcome. Do not substitute an adjacent turn.

The first vertical slice does not use workspace/time inference. When exact telemetry is unavailable it reports `Absent`, `TurnAbsent`, `Partial`, `Corrupt`, `PermissionDenied`, or `Ambiguous` rather than guessing. Add the existing workspace/time locator during hardening only if real retained failures demonstrate a need; any such result must be labeled heuristic, include its candidates and confidence, and never be elevated silently to exact correlation.

If the CLI process can launch more than one provider session, retain all correlated telemetry records but open only the turn associated with the failed transition. Rollout resolution, turn selection, and adjacent-context selection are separate recorded stages. A separately launched diagnostic agent receives a distinct invocation role so its own session can never be mistaken for the failed product session.

## Bounded private session reader

Add a certification-private rollout reader with a deliberately small responsibility:

- open an exact correlated rollout;
- select the failed provider turn and a small configurable amount of adjacent context;
- pair tool calls with tool outputs by call ID;
- retain the event order needed to understand what the provider attempted and what the tools reported;
- include visible assistant/user messages needed to interpret those events;
- exclude hidden/encrypted reasoning and unrelated turns; and
- stop at configurable event and byte limits, recording any truncation.

The reader should return structured events and stable source locations such as rollout path plus event ordinal or line number. It is not a second general session model. It exists only to give failure diagnosis the smallest useful slice of the offending session.

Raw tool arguments and outputs remain private local evidence. Before persistence, redact credential-shaped values, auth headers, tokens, secret environment values, and user-profile path prefixes. Redaction must preserve operationally meaningful relative paths such as `.agents/handoffs/handoff.md`.

Do not change the public recovery projection in [`CodexRolloutRepository.cs`](src/LoopRelay.Agents/Services/Codex/CodexRolloutRepository.cs).

## Retained failure record

Use these terms consistently:

- **attempt record**: the immutable certification result and any diagnostic artifacts for one execution;
- **retained fixture case**: the disposable generated repository/workspace in which the failure occurred; and
- **session evidence**: the bounded copied segment selected from the provider rollout.

Every failed live execution receives its own non-overwriting attempt record and retained fixture case. Artifact creation within the attempt record is conditional:

```text
attempt-<invocation-id>/
  failure.json
  diagnosis-status.json
  retained-case/                       # or an immutable reference to its retained location
  telemetry-reference.json             # only when correlation was attempted
  session-segment.private.jsonl        # only when inspection occurred and evidence resolved
  diagnosis.json                       # only when diagnosis was attempted
  diagnosis.md                         # only when diagnosis was attempted
```

`failure.json` contains the authoritative certification result and failed transition. `diagnosis-status.json` is always present and records `NotNeeded`, `Completed`, `Inconclusive`, or `Unavailable`, plus a bypass or failure reason. For `NotNeeded`, it is the only diagnosis artifact and includes the existing explanation. `telemetry-reference.json` records exact identities and stage-specific resolution status. The private segment contains the bounded redacted events. When diagnosis is attempted, `diagnosis.json` is the validated machine-readable result and `diagnosis.md` is its concise operator-facing rendering.

A later passing attempt must not overwrite or erase the failed attempt. No comprehensive repository snapshot is required. The retained generated case is the authoritative fixture state; diagnosis may inspect specific paths or files relevant to the failed contract.

Automatic retention must happen before correlation or diagnosis so evidence survives a diagnostic timeout, cancellation, missing rollout, or diagnostic-agent failure. The diagnostic agent never writes into the attempt record directly: it writes only to isolated scratch, after which the harness validates and copies accepted output into the attempt record.

## Read-only diagnostic pass

The diagnostic pass receives only:

- the failed certification result and CLI explanation;
- the bounded private session segment;
- read access to the retained fixture case;
- explicitly selected relevant product/harness source; and
- a writable isolated scratch directory for its own response.

Source selection is bounded and owned by the runner's failure-context construction. For the first slice it includes:

1. the certification source that owns the failed contract;
2. the workflow or prompt source involved in the failed transition; and
3. files directly named by the failure result or selected rollout segment.

The motivating test explicitly supplies its validator and prompt/workflow source. If those files are insufficient, the diagnoser may use a bounded read-only follow-up interface with path allowlisting and request limits. It must not receive repository-wide exploratory access by default.

Do not give the diagnostic agent provider credentials, Git write/publish credentials, or a writable product checkout. Prefer copied source excerpts and an analysis workspace populated from the retained fixture case. The retained fixture case, attempt inputs, and product source are protected read-only inputs; only isolated scratch is writable. The harness accepts output only from the designated scratch output, validates its schema and citations, copies the accepted result into the attempt record, and verifies that protected roots are unchanged. Any attempted write outside scratch rejects the diagnosis.

The diagnostic prompt requires:

- observed facts with citations to the failure result, rollout event, or retained-case path;
- inferences labeled separately from facts;
- the first observed contract divergence, without claiming it is the ultimate causal defect unless the evidence supports that conclusion;
- a concise likely cause and directly implicated source or prompt area;
- missing evidence and alternative explanations; and
- no repair, mutation, certification rerun, or uncited assertion.

For the motivating case, a sufficient diagnosis is:

```text
Fact: certification required .agents/handoffs/handoff.md and reported it absent.
Fact: rollout event <citation> records a successful write to agents/handoffs/handoff.md.
Fact: the retained case contains agents/handoffs/handoff.md and lacks the required dotted path.
Inference: the first observed contract divergence is omission of the leading dot in the write path.
```

The certification result remains authoritative. The diagnostic agent explains a failure; it cannot convert failure to pass.

## Diagnosis result

Keep status and detailed diagnosis distinct:

```text
CertificationDiagnosisStatus
  disposition: NotNeeded | Completed | Inconclusive | Unavailable
  invocationId
  bypassOrFailureReason?
  createdAt

CertificationFailureDiagnosis       # present only when diagnosis was attempted
  disposition: Completed | Inconclusive | Unavailable
  invocationId
  telemetryResolution
  summary
  facts[]          # each has one or more evidence citations
  inferences[]     # explicitly labeled and citation-backed
  missingEvidence[]
  firstObservedContractDivergence?
  createdAt

CertificationDiagnosisOutcome
  status: CertificationDiagnosisStatus
  diagnosis?: CertificationFailureDiagnosis
```

`NotNeeded` appears only in the small status record and names the bypass rule. `Unavailable` means the diagnostic mechanism could not run, such as a missing diagnostic provider or denied rollout access. `Inconclusive` means the available evidence was inspected but did not support a reliable explanation. A terminal disposition permits the surrounding workflow to stop waiting; it does not itself authorize an automatic repeat.

Keep classification and diagnosis separate in the first slice. A generic `ProductRegression` classification may route the release result while the attached diagnosis supplies the actual evidence-backed explanation.

## Failure, timeout, and cancellation behavior

- If failed-case retention itself fails, surface that as a certification infrastructure error immediately.
- If exact correlation fails, write the explicit rollout-resolution or turn-selection status and candidate metadata; do not choose arbitrarily.
- If the rollout resolves but the requested turn is absent, record `TurnAbsent`; do not inspect a neighboring turn instead.
- If the rollout is partial, inspect only the complete bounded events and mark the diagnosis evidence as partial.
- If redaction or private-segment persistence fails, do not pass unredacted events to the diagnostic agent.
- Give post-failure collection and diagnosis separate bounded time budgets so they cannot hang the certification command indefinitely.
- On operator cancellation, retain whatever authoritative failure and correlation metadata already exists, stop launching new diagnostic work, and record cancellation.
- If the diagnostic agent fails, timeouts, or returns malformed output, preserve the failed case and emit `Unavailable` rather than hiding the original failure.

## Repeat rule

The certification harness currently stops on failure; preserve that behavior. Any campaign wrapper or future aggregate runner must not automatically repeat an unexplained live failure until its diagnostic attempt has reached a terminal disposition: `Completed`, `Inconclusive`, or `Unavailable`.

This is a sequencing guard, not a claim that diagnosis must always succeed and not automatic permission to repeat. The operator may decide to rerun after reviewing an inconclusive or unavailable diagnosis, but that decision is explicit rather than a hidden guess loop.

Document the same rule in [`docs/certification.md`](docs/certification.md): inspect the attempt record and diagnosis before manually repeating a failed live fixture.

## Implementation sequence

### 1. Deliver the motivating vertical slice

- Make failed-case retention unconditional in `FullChainLiveRunner`.
- Introduce the shared `ICertificationFailureDiagnoser` boundary and the simple explained/unexplained gate.
- Propagate a certification invocation ID and correlate it exactly to existing CLI/provider telemetry; do not add workspace/time inference in this slice.
- Add the bounded private reader.
- Add the bounded runner-owned source selection and optional allowlisted follow-up reader.
- Run the diagnostic pass with protected read-only inputs and isolated writable scratch; validate and copy accepted output through the harness.
- Attach the diagnosis to the failed attempt.
- Add the wrong-path fixture proving the leading-dot diagnosis end to end.

This slice is the first useful release. Do not defer the actual session-assisted diagnosis until after building generalized infrastructure.

### 2. Apply the shared hook to all live runners

Call the same diagnoser from the semantic live runners that can invoke Codex:

- `ProviderProfileRunner`;
- `TransitionRecoveryRunner`;
- `PlanWorkflowRunner`;
- `ExecuteWorkflowRunner`;
- `RoadmapLiveRunner`;
- `CompletionClosureRunner`; and
- `FullChainLiveRunner`.

Keep runner-specific code limited to constructing `CertificationFailureContext`. Correlation, bounded reading, redaction, agent isolation, artifact writing, and disposition handling belong to the shared diagnoser.

### 3. Harden failure paths and documentation

- Cover absent, turn-absent, partial, corrupt, permission-denied, and ambiguous rollout resolution.
- Evaluate workspace/time fallback only from real retained failures; if added, keep it heuristic and outside exact correlation.
- Enforce event/byte/time limits and cancellation semantics.
- Verify protected product and retained-case inputs remain unchanged.
- Add private-evidence privacy tests.
- Update [`docs/certification.md`](docs/certification.md) and the README entry to describe conditional session inspection and diagnose-before-rerun behavior.

Defer any broader deterministic classifier, state observer, public incident bundle, or long-term evidence-governance program until repeated real failures demonstrate a separate need.

## Test plan

### Decision-gate tests

- A successful live transition does not read telemetry or a rollout.
- A confidently classified quota failure reports the existing explanation and does not read a rollout.
- A nominally known classification that lacks the bypass allowlist's required confidence, deterministic evidence, or actionable next step invokes diagnosis.
- A generic unexplained live failure invokes diagnosis exactly once.
- A non-provider deterministic fixture failure does not look for a provider session.
- An explicit operator diagnosis request reads retained evidence without rerunning certification.

### Correlation and reader tests

- Exact invocation, provider thread, and provider turn identities select the correct rollout when unrelated sessions exist nearby.
- Exact thread lookup and the telemetry-recorded path identifying different rollouts returns `Ambiguous`; neither source silently wins.
- A resolved rollout that lacks the requested provider turn returns `TurnAbsent`; no adjacent turn is substituted.
- If a workspace/time fallback is added during hardening, it is labeled non-exact and returns ambiguity when multiple candidates qualify.
- Tool calls and outputs are paired by call ID and retain event order.
- Adjacent unrelated turns are excluded.
- Partial and truncated rollouts produce usable bounded evidence with explicit warnings.
- Hidden/encrypted reasoning never enters the private segment or diagnosis prompt.
- Credential-shaped values and profile path prefixes are redacted while relative fixture paths remain useful.

### Diagnostic tests

- The motivating fixture cites the missing dotted path, the successful wrong-path write, and retained-case confirmation, then identifies omission of the leading dot.
- Facts and inferences have distinct schema fields and facts require citations.
- A diagnostic agent cannot modify the product checkout or retained case.
- A diagnostic agent that attempts to write outside its scratch output is rejected, and protected inputs are confirmed unchanged.
- Provider unavailability, timeout, malformed output, and cancellation preserve the original failure and yield the expected non-completed disposition.
- A later passing attempt does not overwrite the earlier failure or its diagnosis.
- No automatic second certification invocation occurs after an unexplained failure.

### Integration tests

- Every live runner uses the shared diagnosis boundary.
- Every failed live attempt is retained without `--retain-case`.
- Known explained failures remain fast and do not pay rollout-reading or diagnostic-agent costs.
- The full-chain motivating failure is diagnosed from a single failed run, with no guess-and-rerun cycle.

## Definition of done

This refinement is complete when:

1. every failed live case is retained automatically and cannot be overwritten by a later attempt;
2. successful and confidently explained runs never trigger rollout inspection;
3. an unexplained live failure is correlated to an exact thread and turn when those identities exist, with uncertainty made explicit otherwise;
4. only a bounded, redacted, private segment of the relevant session is read, with hidden reasoning excluded;
5. a read-only diagnostic pass can inspect the failure, retained case, relevant source, and session segment without mutating protected inputs;
6. every attempt has a small status record, while detailed diagnosis artifacts exist only when diagnosis was attempted and distinguish cited facts from inferences;
7. the motivating wrong-path case is diagnosed correctly from its first failed attempt;
8. no automated blind repeat occurs before a required diagnostic attempt reaches a terminal disposition; and
9. the workflow and its post-epic-hardening role are documented for operators.

The result should let an agent learn what actually happened in the offending session and make one evidence-driven fix, while leaving ordinary successful certification runs and routine test verification untouched.
