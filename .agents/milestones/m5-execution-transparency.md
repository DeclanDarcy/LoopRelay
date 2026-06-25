## Milestone 5: Execution Transparency

### Objective

Make execution explainable: what was launched, what context was included, what recovery happened, what failed, what is retryable, what changed, what is safe to commit, and what is safe to push.

### Backend

- [x] Add an `ExecutionPromptManifest` model that captures requested context and delivered context for each launched session:
   - [x] session id
   - [x] generated at
   - [x] full prompt text or persisted prompt artifact reference
   - [x] requested artifact paths
   - [x] requested artifact roles
   - [x] requested context bytes
   - [x] requested context characters
   - [x] delivered artifact paths
   - [x] delivered artifact roles
   - [x] delivered context bytes
   - [x] delivered context characters
   - [x] dirty repository flag at request time
   - [x] dirty repository flag at delivery time when known
   - [x] governed decision count requested
   - [x] governed decision count delivered
   - [x] operational context source requested and delivered
   - [x] handoff source requested and delivered
   - [x] milestone source requested and delivered
   - [x] provider delivery status
   - [x] provider adjustments, including truncation, refusal, provider-added wrapper, or provider cache reference when present
   - [x] divergence reason when delivered context differs from requested context
   - [x] diagnostics
- [x] Persist the launched prompt manifest with the execution session. The manifest is app execution metadata, not repository authority.
- [x] If the provider abstraction cannot yet report delivered-context divergence or adjustments, record delivered context as equal to requested context, provider adjustments as empty, and include an explicit `NoProviderDivergenceSignal` diagnostic. Keep the model ready for future provider limits, refusals, wrappers, cache references, or delivery failures.
- [x] Add `GET /api/execution-sessions/{sessionId}/prompt` to return the launched manifest.
- [x] Extend `ExecutionSessionSummary` or add a `ExecutionSessionTransparency` endpoint for:
   - [x] prompt metadata
   - [x] recovery ran
   - [x] recovery trigger
   - [x] reattach attempted
   - [x] reattach succeeded or failed
   - [x] orphaned provider state
   - [x] session marked failed by recovery
   - [x] recovery event timestamp
   - [x] provider process state
   - [x] exit code
   - [x] last activity
   - [x] stale activity
   - [x] event retention trimming
   - [x] monitoring warnings
- [x] Adjust push behavior so a failed push returns the updated retry state to the UI:
   - [x] Keep `ExecutionSessionService.PushAsync` as the execution authority.
   - [x] In `GitEndpoints.MapPush`, when push fails after state persistence, load the latest session summary and include it in a structured conflict body, or return a typed `PushAttemptResult` that includes failure and session.
   - [x] Update shell and UI clients to refresh or consume that session.
- [x] Add a backend git action eligibility read model:
   - [x] commit preparation loaded
   - [x] preparation current
   - [x] selected path count
   - [x] commit message present
   - [x] repository state allows commit
   - [x] session exists
   - [x] awaiting push
   - [x] commit SHA exists
   - [x] remote branch state
   - [x] previous push failure
   - [x] disabled reasons
- [ ] Add structured governed decision conflict details to execution context diagnostics instead of only flattened validation strings.
- [ ] Add handoff processing transparency fields:
   - [ ] handoff produced
   - [ ] handoff missing
   - [ ] handoff archived
   - [ ] archive path or sequence
   - [ ] archive failed
   - [ ] handoff validated
   - [ ] validation failure
   - [ ] resulting session state
- [ ] Add semantic categories and consequence text to execution events:
   - [ ] launch
   - [ ] provider
   - [ ] monitoring
   - [ ] recovery
   - [ ] handoff
   - [ ] git
   - [ ] failure

### UI

- [ ] Extend execution TypeScript types for prompt manifest, recovery diagnostics, git eligibility, handoff processing, monitoring health, structured governed conflicts, and semantic event grouping.
   - [x] Added prompt manifest TypeScript types.
   - [x] Added execution transparency TypeScript types for prompt metadata, recovery, and monitoring health.
- [x] Add `getExecutionPromptManifest`, `getExecutionTransparency`, and git eligibility client functions.
   - [x] Added `getExecutionPromptManifest`.
   - [x] Added `getExecutionTransparency`.
   - [x] Added `getExecutionGitEligibility`.
- [x] Update `ExecutionSessionPanel` and `ExecutionTab` to show the session-level launched prompt manifest. Clearly distinguish preview context, requested launched context, delivered launched context, and provider adjustments.
- [x] Add recovery banner and recovery event details to distinguish normal run failure from startup recovery failure.
- [x] Update `GitWorkflowPanel` to show commit and push precondition checklists, blocked reasons, retry warnings, previous push failure, and push attempt history.
   - [x] Previous push failure and last push attempt timestamp now render from the execution summary.
   - [x] Commit/push eligibility, disabled reasons, stale preparation, selected path state, remote branch state, and previous push failure now render from the backend eligibility projection.
- [ ] Update `GitPathBucket` and `GitWorkflowEvidence` to distinguish execution-generated changes from pre-existing repository changes and provide bulk actions to select only execution-generated changes or deselect pre-existing changes.
- [ ] Update handoff panels to show post-processing state, archive diagnostics, validation diagnostics, and whether provider failure differs from handoff processing failure.
- [x] Replace placeholder monitoring text with real monitoring health fields.
- [ ] Update execution validation list to render governed decision conflicts as governance blockers with decision id, conflicting excerpt, conflict reason, affected prompt/context, and resolution path.
- [ ] Group execution events by semantic category and display event consequence plus related state change.

### Tests

- [ ] Backend tests proving prompt manifest is persisted, distinguishes requested and delivered context, records provider adjustments, and differs from preview when appropriate.
   - [x] Prompt manifest persistence covers requested vs delivered artifacts and the explicit no-provider-divergence diagnostic.
   - [ ] Preview-vs-launched prompt differences still need coverage when preview and launch surfaces are wired together.
- [x] Backend tests for push conflict response containing updated retry state.
- [ ] Backend tests for git eligibility branches and structured governed conflicts.
   - [x] Git eligibility branches cover stale commit preparation, selected path state, missing message, previous push failure, remote branch state, and endpoint projection.
- [ ] UI tests for prompt manifest, provider adjustments, recovery banner, push retry, disabled commit/push reasons, handoff processing diagnostics, pre-existing change warnings, and event grouping.
   - [x] Prompt manifest rendering covers requested vs delivered context, provider adjustments empty state, and `NoProviderDivergenceSignal`.
   - [x] Prompt manifest hook coverage proves shell detail-command loading.
   - [x] Recovery and monitoring transparency rendering covers recovery events, orphaned-provider state, provider process state, exit code, retained events, and monitoring warnings.
   - [x] Transparency hook coverage proves shell detail-command loading.
   - [x] Push retry rendering covers previous push failure and last push attempt evidence.
   - [x] Git eligibility rendering and hook coverage prove backend-owned disabled reasons and shell loading.

### Exit Criteria

- [x] Users can inspect requested context and delivered context for the provider launch.
- [x] Provider adjustments are represented explicitly, even when no adjustment signal is available.
- [x] Recovery behavior is distinct from normal execution failure.
- [x] Push failures leave an understandable retryable state.
- [x] Commit and push controls explain blocked preconditions.
- [ ] Handoff post-processing is visible and distinguishable from provider failure.
- [x] Monitoring diagnostics are real.
- [ ] Governed decision conflicts are distinct from generic validation errors.
- [ ] Pre-existing changes are separated from execution-generated changes.
