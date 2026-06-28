import { PhaseTimeline, StreamFailurePanel, StreamOutputPanel } from '../streams'
import type { PhaseStep } from '../streams'
import type { ExecutionRunState } from '../../types'
import './ExecutionRun.css'

type ExecutionStreamViewProps = {
  state: ExecutionRunState
  onDismissFailure?: () => void
  // Client-only transport signals (not on the frozen run state): the browser is retrying a dropped
  // stream, or it gave up. Reconnecting shows in the output pill; a give-up surfaces as a
  // recoverable failure instead of an indefinite spinner. A cancelled turn maps to transportFailed.
  isReconnecting?: boolean
  transportFailed?: boolean
}

// The run is a genuine ordered sequence, so the surface is a timeline rather than a card
// grid: extract the milestones, record the commit, hand off to execution. Each step's
// state is derived from the streamed run — it encodes progress, it doesn't decorate.
const PHASE_STEPS = [
  { key: 'ExtractMilestones', label: 'Extract milestones' },
  { key: 'Commit', label: 'Commit & push' },
  { key: 'StartExecution', label: 'Start execution' },
  // Product term: the agent records the handoff for the next turn. The raw filesystem path is
  // implementation detail and lives in Diagnostics, not the primary step note.
  { key: 'Handoff', label: 'Record handoff' },
] as const

const PHASE_LABEL: Record<string, string> = {
  ExecutePlan: 'Starting run',
  ExtractMilestones: 'Extracting milestones',
  StartExecution: 'Starting execution',
  ContinueExecution: 'Continuing execution',
}

export function ExecutionStreamView({
  state,
  onDismissFailure,
  isReconnecting = false,
  transportFailed = false,
}: ExecutionStreamViewProps) {
  const steps = resolveSteps(state)
  const isRunning = state.status === 'Running'
  // While reconnecting, the output pill reads "Reconnecting" rather than the live phase label.
  const phaseLabel = isReconnecting
    ? 'Reconnecting'
    : state.phase
      ? PHASE_LABEL[state.phase] ?? 'Working'
      : 'Working'
  const handoffPath = resolveHandoffPath(state)
  // A transport give-up takes over the surface as a recoverable failure, but only while the run is
  // still Running: a late drop after a Completed run keeps its success summary, and an own-reported
  // Failed surface wins. This is also where a lost/aborted turn surfaces.
  const showTransportFailure = transportFailed && state.status === 'Running'

  return (
    <section className="cc-execution" aria-label="Plan execution">
      <header className="cc-execution-masthead">
        <span className="cc-stream-eyebrow">Execution</span>
        <h2 className="cc-execution-title">Executing the plan</h2>
        <p className="cc-execution-lede">
          The agent is running the plan. Watch each phase complete, then land in the workspace.
        </p>
      </header>

      <PhaseTimeline ariaLabel="Execution phases" steps={steps} />

      {state.status !== 'Failed' && !showTransportFailure ? (
        <StreamOutputPanel
          ariaLabel="Execution output"
          streamedText={state.streamedText}
          live={isRunning}
          phaseLabel={phaseLabel}
          tokens={state.status === 'Completed' ? state.completion : null}
        />
      ) : null}

      {showTransportFailure ? (
        <StreamFailurePanel
          ariaLabel="Execution connection lost"
          eyebrow="Execution connection lost"
          reason="The connection to the execution stream dropped and could not be restored. The run may still be progressing on the backend."
          onDismiss={onDismissFailure}
          dismissLabel="Back to plan"
        />
      ) : null}

      {state.status === 'Completed' && state.completion ? (
        <dl className="cc-execution-summary" role="group" aria-label="Execution result">
          <div className="cc-execution-summary-item">
            <dt>Milestones</dt>
            <dd>{state.completion.milestoneCount}</dd>
          </div>
          <div className="cc-execution-summary-item">
            <dt>Commit</dt>
            <dd>
              {state.completion.commitSha ? (
                <code className="cc-execution-sha">{shortSha(state.completion.commitSha)}</code>
              ) : (
                <span className="cc-execution-empty">No commit</span>
              )}
            </dd>
          </div>
          <div className="cc-execution-summary-item">
            <dt>Handoff</dt>
            <dd>
              {/* Product result: a human confirmation, not the raw path. The path is in Diagnostics. */}
              <span className="cc-execution-handoff-confirm">Handoff recorded</span>
            </dd>
          </div>
        </dl>
      ) : null}

      {state.status === 'Completed' ? (
        <details className="cc-execution-diagnostics" aria-label="Execution diagnostics">
          <summary className="cc-stream-eyebrow cc-execution-diagnostics-summary">
            Diagnostics
          </summary>
          <dl className="cc-execution-diagnostics-list">
            <div className="cc-execution-diagnostics-item">
              <dt>Handoff path</dt>
              <dd>
                {handoffPath ? (
                  <code className="cc-execution-path">{handoffPath}</code>
                ) : (
                  <span className="cc-execution-empty">Unknown handoff</span>
                )}
              </dd>
            </div>
          </dl>
        </details>
      ) : null}

      {state.status === 'Failed' && state.failure ? (
        <StreamFailurePanel
          ariaLabel="Execution failed"
          eyebrow={
            state.failure.phase ? `Execution failed: ${state.failure.phase}` : 'Execution failed'
          }
          reason={state.failure.reason}
          detail={state.failure.detail}
          onDismiss={onDismissFailure}
          dismissLabel="Back to plan"
        />
      ) : null}
    </section>
  )
}

// The handoff path is implementation detail surfaced in Diagnostics only. Guard against an
// empty/whitespace path the same way the commit summary guards a missing sha, so a malformed
// completion frame renders "Unknown handoff" instead of an empty code block.
function resolveHandoffPath(state: ExecutionRunState): string | null {
  const path = state.completion?.handoffPath ?? state.handoff?.path ?? null
  if (path === null || path.trim().length === 0) {
    return null
  }

  return path
}

function resolveSteps(state: ExecutionRunState): PhaseStep[] {
  const milestonesDone = state.milestoneCount !== null
  const commitDone = state.commit !== null
  const handoffDone = state.handoff !== null || state.status === 'Completed'
  // StartExecution is reached once the backend reports the lifecycle/phase transition; the
  // handoff arriving (or completion) is the strongest proxy the UI has for it being underway.
  const executionStarted = state.phase === 'StartExecution' || handoffDone

  const stepState = (done: boolean, active: boolean): PhaseStep['state'] => {
    if (done) {
      return 'done'
    }

    return active && state.status === 'Running' ? 'active' : 'pending'
  }

  return [
    {
      ...PHASE_STEPS[0],
      state: stepState(milestonesDone, !milestonesDone),
      note: milestonesDone ? `${state.milestoneCount} extracted` : null,
    },
    {
      ...PHASE_STEPS[1],
      state: stepState(commitDone, milestonesDone && !commitDone),
      note: state.commit
        ? state.commit.commitSha
          ? `${shortSha(state.commit.commitSha)}${state.commit.pushed ? ' · pushed' : ''}`
          : 'No changes to commit'
        : null,
    },
    {
      ...PHASE_STEPS[2],
      state: stepState(executionStarted, commitDone && !executionStarted),
      note: null,
    },
    {
      ...PHASE_STEPS[3],
      state: stepState(handoffDone, executionStarted && !handoffDone),
      // The raw handoff path is implementation detail (Diagnostics only). The primary note is a
      // human confirmation that the handoff was recorded.
      note: handoffDone ? 'Recorded' : null,
    },
  ]
}

function shortSha(sha: string) {
  return sha.length > 10 ? sha.slice(0, 10) : sha
}
