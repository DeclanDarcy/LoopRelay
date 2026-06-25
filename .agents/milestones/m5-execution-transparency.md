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
- [ ] Extend `ExecutionSessionSummary` or add a `ExecutionSessionTransparency` endpoint for:
   - [ ] prompt metadata
   - [ ] recovery ran
   - [ ] recovery trigger
   - [ ] reattach attempted
   - [ ] reattach succeeded or failed
   - [ ] orphaned provider state
   - [ ] session marked failed by recovery
   - [ ] recovery event timestamp
   - [ ] provider process state
   - [ ] exit code
   - [ ] last activity
   - [ ] stale activity
   - [ ] event retention trimming
   - [ ] monitoring warnings
- [ ] Adjust push behavior so a failed push returns the updated retry state to the UI:
   - [ ] Keep `ExecutionSessionService.PushAsync` as the execution authority.
   - [ ] In `GitEndpoints.MapPush`, when push fails after state persistence, load the latest session summary and include it in a structured conflict body, or return a typed `PushAttemptResult` that includes failure and session.
   - [ ] Update shell and UI clients to refresh or consume that session.
- [ ] Add a backend git action eligibility read model:
   - [ ] commit preparation loaded
   - [ ] preparation current
   - [ ] selected path count
   - [ ] commit message present
   - [ ] repository state allows commit
   - [ ] session exists
   - [ ] awaiting push
   - [ ] commit SHA exists
   - [ ] remote branch state
   - [ ] previous push failure
   - [ ] disabled reasons
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
- [ ] Add `getExecutionPromptManifest`, `getExecutionTransparency`, and git eligibility client functions.
- [ ] Update `ExecutionSessionPanel` and `ExecutionTab` to show the session-level launched prompt manifest. Clearly distinguish preview context, requested launched context, delivered launched context, and provider adjustments.
- [ ] Add recovery banner and recovery event details to distinguish normal run failure from startup recovery failure.
- [ ] Update `GitWorkflowPanel` to show commit and push precondition checklists, blocked reasons, retry warnings, previous push failure, and push attempt history.
- [ ] Update `GitPathBucket` and `GitWorkflowEvidence` to distinguish execution-generated changes from pre-existing repository changes and provide bulk actions to select only execution-generated changes or deselect pre-existing changes.
- [ ] Update handoff panels to show post-processing state, archive diagnostics, validation diagnostics, and whether provider failure differs from handoff processing failure.
- [ ] Replace placeholder monitoring text with real monitoring health fields.
- [ ] Update execution validation list to render governed decision conflicts as governance blockers with decision id, conflicting excerpt, conflict reason, affected prompt/context, and resolution path.
- [ ] Group execution events by semantic category and display event consequence plus related state change.

### Tests

- [ ] Backend tests proving prompt manifest is persisted, distinguishes requested and delivered context, records provider adjustments, and differs from preview when appropriate.
   - [x] Prompt manifest persistence covers requested vs delivered artifacts and the explicit no-provider-divergence diagnostic.
   - [ ] Preview-vs-launched prompt differences still need coverage when preview and launch surfaces are wired together.
- [ ] Backend tests for push conflict response containing updated retry state.
- [ ] Backend tests for git eligibility branches and structured governed conflicts.
- [ ] UI tests for prompt manifest, provider adjustments, recovery banner, push retry, disabled commit/push reasons, handoff processing diagnostics, pre-existing change warnings, and event grouping.

### Exit Criteria

- [ ] Users can inspect requested context and delivered context for the provider launch.
- [ ] Provider adjustments are represented explicitly, even when no adjustment signal is available.
- [ ] Recovery behavior is distinct from normal execution failure.
- [ ] Push failures leave an understandable retryable state.
- [ ] Commit and push controls explain blocked preconditions.
- [ ] Handoff post-processing is visible and distinguishable from provider failure.
- [ ] Monitoring diagnostics are real.
- [ ] Governed decision conflicts are distinct from generic validation errors.
- [ ] Pre-existing changes are separated from execution-generated changes.
