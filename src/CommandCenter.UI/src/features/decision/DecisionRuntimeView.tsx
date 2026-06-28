import { Button } from '../../components/design'
import { PhaseTimeline, StreamFailurePanel, StreamOutputPanel } from '../streams'
import type { PhaseStep } from '../streams'
import type { DecisionRunState } from '../../types'
import './DecisionRun.css'

type DecisionRuntimeViewProps = {
  state: DecisionRunState
  onGenerate: () => void
  onEditDecisions: (decisions: string) => void
  onSubmitDecisions: (decisions: string) => void
  // Leave the continuation loop and go to the workspace. The loop otherwise runs indefinitely,
  // so this is the reviewer's explicit exit — the only way navigation happens.
  onFinish?: () => void
  onDismissFailure?: () => void
  // Client-only transport signals (not on the frozen run state): the browser is retrying a dropped
  // stream, or it gave up. Reconnecting shows in the output pill; a give-up surfaces as a
  // recoverable failure instead of an indefinite spinner. A cancelled turn maps to transportFailed.
  isReconnecting?: boolean
  transportFailed?: boolean
}

// The run is a genuine ordered sequence, so the surface is a timeline rather than a card grid:
// prepare the session, propose decisions, open the review gate, then persist the human's edit.
// Each step's state is derived from the streamed run — it encodes progress, it doesn't decorate.
const PHASE_STEPS = [
  // Product term: the session is prepared before proposing. The raw sandbox config is
  // implementation detail and lives in Diagnostics, not the primary step.
  { key: 'Seed', label: 'Prepare decision session' },
  { key: 'Propose', label: 'Propose decisions' },
  { key: 'Review', label: 'Open review gate' },
  { key: 'Submit', label: 'Persist decisions' },
] as const

const PHASE_LABEL: Record<string, string> = {
  DecisionRun: 'Starting run',
  GetNextDecisions: 'Proposing decisions',
}

// Ordinal label for the current decision turn, used only once the loop is past its first turn so
// the reviewer can see which iteration they are reviewing.
function iterationLabel(iteration: number): string | null {
  if (iteration <= 1) {
    return null
  }

  return `Turn ${iteration}`
}

export function DecisionRuntimeView({
  state,
  onGenerate,
  onEditDecisions,
  onSubmitDecisions,
  onFinish,
  onDismissFailure,
  isReconnecting = false,
  transportFailed = false,
}: DecisionRuntimeViewProps) {
  const steps = resolveSteps(state)
  const isRunning = state.status === 'Running'
  // While reconnecting, the output pill reads "Reconnecting" rather than the live phase label.
  const phaseLabel = isReconnecting
    ? 'Reconnecting'
    : state.phase
      ? PHASE_LABEL[state.phase] ?? 'Working'
      : 'Working'
  // The gate is open once the captured decisions become editable, until they are submitted.
  const reviewOpen = state.editableDecisions !== null
  const isIdle = state.status === 'Idle'
  const canSubmit = reviewOpen && (state.editableDecisions ?? '').trim().length > 0
  // After submit the gate is closed while the server runs the continuation turn (streamed on the
  // execution stream) and auto-starts the next decision run; the loop reopens the gate then.
  const isContinuing = state.status === 'Submitting' || state.status === 'Submitted'
  const turnLabel = iterationLabel(state.iteration)
  // The continuation router is handing the session off to a fresh one. Surfaced only while the run
  // is live; review-ready (or a failure) resolves the transfer and the reducer lowers the flag.
  const isTransferring = state.transferring && state.status === 'Running'
  // The raw persisted decisions path is implementation detail (Diagnostics only). Prefer the rotated
  // numbered path when present; guard empty/whitespace so a malformed frame renders nothing.
  const submittedPath = resolveSubmittedPath(state)
  // Diagnostics are implementation/router mechanics: the validated sandbox config, the in-flight
  // session transfer, and the raw persisted path. They live in a secondary disclosure, never the
  // primary reading flow.
  const hasDiagnostics = state.diagnostics !== null || isTransferring || submittedPath !== null
  // A transport give-up takes over the surface as a recoverable failure, but only while the run is
  // still Running: a late drop after the decisions were submitted (Submitting/Submitted) keeps the
  // Continuing banner, and an own-reported Failed surface wins. This is also where a lost/aborted
  // turn surfaces.
  const showTransportFailure = transportFailed && state.status === 'Running'

  return (
    <section className="cc-decision" aria-label="Decision runtime">
      <header className="cc-decision-masthead">
        <span className="cc-stream-eyebrow">
          Decisions{turnLabel ? <span className="cc-decision-turn"> · {turnLabel}</span> : null}
        </span>
        <h2 className="cc-decision-title">Propose and review decisions</h2>
        <p className="cc-decision-lede">
          The agent proposes decisions from the operational context. Review the captured text,
          edit it, then submit. Each submission runs the next turn and proposes again.
        </p>
      </header>

      {isIdle ? (
        <div className="cc-decision-launch">
          <Button type="button" variant="primary" onClick={onGenerate}>
            Generate decisions
          </Button>
        </div>
      ) : null}

      {!isIdle ? <PhaseTimeline ariaLabel="Decision phases" steps={steps} /> : null}

      {hasDiagnostics ? (
        <details className="cc-decision-diagnostics-disclosure" aria-label="Decision diagnostics">
          <summary className="cc-stream-eyebrow cc-decision-diagnostics-summary">
            Diagnostics
          </summary>
          {state.diagnostics ? (
            <p className="cc-decision-diagnostics" aria-label="Sandbox diagnostics">
              Sandbox{' '}
              <code className="cc-decision-diagnostics-value">{state.diagnostics.sandbox}</code> ·
              approvals{' '}
              <code className="cc-decision-diagnostics-value">{state.diagnostics.approvals}</code> ·{' '}
              {state.diagnostics.seeded ? 'session seeded' : 'session not seeded'}
            </p>
          ) : null}
          {isTransferring ? (
            <p
              className="cc-decision-transfer"
              role="status"
              aria-label="Transferring decision session"
            >
              <span className="cc-stream-phase-dot" aria-hidden="true" />
              Transferring decision session…
            </p>
          ) : null}
          {submittedPath ? (
            <p className="cc-decision-diagnostics" aria-label="Persisted decisions path">
              Persisted to <code className="cc-decision-path">{submittedPath}</code>
            </p>
          ) : null}
        </details>
      ) : null}

      {state.status !== 'Failed' &&
      state.status !== 'Idle' &&
      !isContinuing &&
      !showTransportFailure ? (
        <StreamOutputPanel
          ariaLabel="Proposed decisions output"
          streamedText={state.streamedText}
          live={isRunning}
          phaseLabel={phaseLabel}
          tokens={state.completion}
        />
      ) : null}

      {showTransportFailure ? (
        <StreamFailurePanel
          ariaLabel="Decision connection lost"
          eyebrow="Decision connection lost"
          reason="The connection to the decision stream dropped and could not be restored. The run may still be progressing on the backend."
          onDismiss={onDismissFailure}
          dismissLabel="Back to plan"
        />
      ) : null}

      {reviewOpen ? (
        <div className="cc-plan-field cc-decision-review">
          <label className="cc-plan-field-label" htmlFor="cc-decision-decisions">
            Decisions
          </label>
          <p className="cc-plan-field-hint">
            This is the human review gate. Edit the proposed decisions, then submit to persist them.
          </p>
          <textarea
            id="cc-decision-decisions"
            className="cc-plan-textarea"
            aria-label="Decisions"
            rows={12}
            value={state.editableDecisions ?? ''}
            onChange={(event) => onEditDecisions(event.target.value)}
          />
          <div className="cc-decision-review-actions">
            <Button
              type="button"
              variant="primary"
              onClick={() => {
                if (canSubmit) {
                  onSubmitDecisions((state.editableDecisions ?? '').trim())
                }
              }}
              disabled={!canSubmit}
            >
              Submit decisions
            </Button>
            {onFinish ? (
              <Button type="button" variant="secondary" onClick={onFinish}>
                Go to workspace
              </Button>
            ) : null}
          </div>
        </div>
      ) : null}

      {isContinuing ? (
        <section className="cc-decision-submitted" role="status" aria-label="Decisions submitted">
          <span className="cc-stream-eyebrow cc-decision-continuing-eyebrow">
            <span className="cc-stream-phase-dot" aria-hidden="true" />
            Continuing
          </span>
          <p className="cc-decision-submitted-reason">
            Decisions persisted. Running the next turn, then proposing again.
          </p>
        </section>
      ) : null}

      {state.status === 'Failed' && state.failure ? (
        <StreamFailurePanel
          ariaLabel="Decision run failed"
          eyebrow={
            state.failure.phase
              ? `Decision run failed: ${state.failure.phase}`
              : 'Decision run failed'
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

function resolveSteps(state: DecisionRunState): PhaseStep[] {
  const seeded = state.diagnostics?.seeded === true
  const proposed = state.completion !== null || state.proposedDecisions !== null
  const reviewReady = state.editableDecisions !== null
  const submitted = state.status === 'Submitting' || state.status === 'Submitted'

  const stepState = (done: boolean, active: boolean): PhaseStep['state'] => {
    if (done) {
      return 'done'
    }

    return active && state.status === 'Running' ? 'active' : 'pending'
  }

  return [
    {
      ...PHASE_STEPS[0],
      state: stepState(seeded, !seeded),
      // The raw sandbox config is implementation detail (Diagnostics only); the primary note
      // confirms the session is ready in human terms.
      note: seeded ? 'Session ready' : null,
    },
    {
      ...PHASE_STEPS[1],
      state: stepState(proposed, seeded && !proposed),
      note: state.completion
        ? `${state.completion.outputTokens.toLocaleString()} tokens out`
        : null,
    },
    {
      // The review gate is the human checkpoint; mark it done once the buffer is editable.
      ...PHASE_STEPS[2],
      state: reviewReady ? 'done' : proposed && state.status === 'Running' ? 'active' : 'pending',
      note: reviewReady && !submitted ? 'Awaiting your review' : null,
    },
    {
      ...PHASE_STEPS[3],
      state: submitted ? 'done' : reviewReady ? 'active' : 'pending',
      // The raw persisted path is implementation detail (Diagnostics only). The primary note is a
      // human confirmation that the decisions were persisted.
      note: submitted ? 'Decisions persisted' : null,
    },
  ]
}

// The persisted decisions path is implementation detail surfaced in Diagnostics only. Prefer the
// rotated numbered path when the loop reports it, and guard empty/whitespace the same way the
// execution handoff path is guarded, so a malformed submit frame renders nothing rather than an
// empty code block.
function resolveSubmittedPath(state: DecisionRunState): string | null {
  const path = state.submittedNumberedPath ?? state.submittedPath
  if (path === null || path.trim().length === 0) {
    return null
  }

  return path
}
