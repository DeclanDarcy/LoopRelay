import { Button } from '../../components/design'
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
}

type StepState = 'pending' | 'active' | 'done'

// The run is a genuine ordered sequence, so the surface is a timeline rather than a card grid:
// seed the session, propose decisions, open the review gate, then persist the human's edit.
// Each step's state is derived from the streamed run — it encodes progress, it doesn't decorate.
const PHASE_STEPS = [
  { key: 'Seed', label: 'Seed decision session' },
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
}: DecisionRuntimeViewProps) {
  const steps = resolveSteps(state)
  const isRunning = state.status === 'Running'
  const phaseLabel = state.phase ? PHASE_LABEL[state.phase] ?? 'Working' : 'Working'
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

  return (
    <section className="cc-decision" aria-label="Decision runtime">
      <header className="cc-decision-masthead">
        <span className="cc-plan-eyebrow">
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

      {!isIdle ? (
        <ol className="cc-decision-timeline" aria-label="Decision phases">
          {steps.map((step) => (
            <li
              key={step.key}
              className={`cc-decision-step cc-decision-step-${step.state}`}
              aria-current={step.state === 'active' ? 'step' : undefined}
            >
              <span className="cc-decision-step-marker" aria-hidden="true">
                {step.state === 'done' ? <CheckGlyph /> : <span className="cc-decision-step-dot" />}
              </span>
              <span className="cc-decision-step-body">
                <span className="cc-decision-step-label">{step.label}</span>
                {step.note ? <span className="cc-decision-step-note">{step.note}</span> : null}
              </span>
            </li>
          ))}
        </ol>
      ) : null}

      {state.diagnostics ? (
        <p className="cc-decision-diagnostics" aria-label="Sandbox diagnostics">
          Sandbox <code className="cc-decision-diagnostics-value">{state.diagnostics.sandbox}</code>{' '}
          · approvals{' '}
          <code className="cc-decision-diagnostics-value">{state.diagnostics.approvals}</code> ·{' '}
          {state.diagnostics.seeded ? 'session seeded' : 'session not seeded'}
        </p>
      ) : null}

      {isTransferring ? (
        <p className="cc-decision-transfer" role="status" aria-label="Transferring decision session">
          <span className="cc-plan-phase-dot" aria-hidden="true" />
          Transferring decision session…
        </p>
      ) : null}

      {state.status !== 'Failed' && state.status !== 'Idle' && !isContinuing ? (
        <section
          className={`cc-plan-document${isRunning ? ' cc-plan-document-live' : ''}`}
          aria-label="Proposed decisions output"
        >
          <header className="cc-plan-document-head">
            <span className="cc-plan-eyebrow">Output</span>
            {isRunning ? (
              <span className="cc-plan-phase-pill" aria-live="polite">
                <span className="cc-plan-phase-dot" aria-hidden="true" />
                {phaseLabel}…
              </span>
            ) : state.completion ? (
              <span className="cc-plan-tokens">
                {state.completion.promptTokens.toLocaleString()} in ·{' '}
                {state.completion.outputTokens.toLocaleString()} out
              </span>
            ) : null}
          </header>
          <pre className="cc-plan-stream" aria-live="polite" aria-atomic="false">
            {state.streamedText}
            {isRunning ? <span className="cc-plan-caret" aria-hidden="true" /> : null}
          </pre>
        </section>
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
          <span className="cc-plan-eyebrow cc-decision-continuing-eyebrow">
            <span className="cc-plan-phase-dot" aria-hidden="true" />
            Continuing
          </span>
          <p className="cc-decision-submitted-reason">
            Decisions persisted. Running the next turn, then proposing again.
          </p>
          {state.submittedNumberedPath ?? state.submittedPath ? (
            <code className="cc-decision-path">
              {state.submittedNumberedPath ?? state.submittedPath}
            </code>
          ) : null}
        </section>
      ) : null}

      {state.status === 'Failed' && state.failure ? (
        <section className="cc-plan-failure" role="alert" aria-label="Decision run failed">
          <div className="cc-plan-failure-body">
            <span className="cc-plan-eyebrow cc-plan-failure-eyebrow">
              {state.failure.phase
                ? `Decision run failed: ${state.failure.phase}`
                : 'Decision run failed'}
            </span>
            <p className="cc-plan-failure-reason">{state.failure.reason}</p>
            {state.failure.detail ? (
              <pre className="cc-plan-failure-detail">{state.failure.detail}</pre>
            ) : null}
          </div>
          {onDismissFailure ? (
            <Button type="button" variant="secondary" onClick={onDismissFailure}>
              Back to plan
            </Button>
          ) : null}
        </section>
      ) : null}
    </section>
  )
}

function resolveSteps(state: DecisionRunState) {
  const seeded = state.diagnostics?.seeded === true
  const proposed = state.completion !== null || state.proposedDecisions !== null
  const reviewReady = state.editableDecisions !== null
  const submitted = state.status === 'Submitting' || state.status === 'Submitted'

  const stepState = (done: boolean, active: boolean): StepState => {
    if (done) {
      return 'done'
    }

    return active && state.status === 'Running' ? 'active' : 'pending'
  }

  return [
    {
      ...PHASE_STEPS[0],
      state: stepState(seeded, !seeded),
      note: state.diagnostics ? state.diagnostics.sandbox : null,
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
      note: submitted ? state.submittedPath : null,
    },
  ] as Array<(typeof PHASE_STEPS)[number] & { state: StepState; note: string | null }>
}

function CheckGlyph() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden="true">
      <path
        d="M2.5 6.5L5 9L9.5 3.5"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}
