import { Button } from '../../components/design'
import type { ExecutionRunState } from '../../types'
import './ExecutionRun.css'

type ExecutionStreamViewProps = {
  state: ExecutionRunState
  onDismissFailure?: () => void
}

type StepState = 'pending' | 'active' | 'done'

// The run is a genuine ordered sequence, so the surface is a timeline rather than a card
// grid: extract the milestones, record the commit, hand off to execution. Each step's
// state is derived from the streamed run — it encodes progress, it doesn't decorate.
const PHASE_STEPS = [
  { key: 'ExtractMilestones', label: 'Extract milestones' },
  { key: 'Commit', label: 'Commit & push' },
  { key: 'StartExecution', label: 'Start execution' },
  { key: 'Handoff', label: 'Rotate handoff' },
] as const

const PHASE_LABEL: Record<string, string> = {
  ExecutePlan: 'Starting run',
  ExtractMilestones: 'Extracting milestones',
  StartExecution: 'Starting execution',
  ContinueExecution: 'Continuing execution',
}

export function ExecutionStreamView({ state, onDismissFailure }: ExecutionStreamViewProps) {
  const steps = resolveSteps(state)
  const isRunning = state.status === 'Running'
  const phaseLabel = state.phase ? PHASE_LABEL[state.phase] ?? 'Working' : 'Working'

  return (
    <section className="cc-execution" aria-label="Plan execution">
      <header className="cc-execution-masthead">
        <span className="cc-plan-eyebrow">Execution</span>
        <h2 className="cc-execution-title">Executing the plan</h2>
        <p className="cc-execution-lede">
          The agent is running the plan. Watch each phase complete, then land in the workspace.
        </p>
      </header>

      <ol className="cc-execution-timeline" aria-label="Execution phases">
        {steps.map((step) => (
          <li
            key={step.key}
            className={`cc-execution-step cc-execution-step-${step.state}`}
            aria-current={step.state === 'active' ? 'step' : undefined}
          >
            <span className="cc-execution-step-marker" aria-hidden="true">
              {step.state === 'done' ? <CheckGlyph /> : <span className="cc-execution-step-dot" />}
            </span>
            <span className="cc-execution-step-body">
              <span className="cc-execution-step-label">{step.label}</span>
              {step.note ? <span className="cc-execution-step-note">{step.note}</span> : null}
            </span>
          </li>
        ))}
      </ol>

      {state.status !== 'Failed' ? (
        <section
          className={`cc-plan-document${isRunning ? ' cc-plan-document-live' : ''}`}
          aria-label="Execution output"
        >
          <header className="cc-plan-document-head">
            <span className="cc-plan-eyebrow">Output</span>
            {isRunning ? (
              <span className="cc-plan-phase-pill" aria-live="polite">
                <span className="cc-plan-phase-dot" aria-hidden="true" />
                {phaseLabel}…
              </span>
            ) : state.status === 'Completed' && state.completion ? (
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
              <code className="cc-execution-path">{state.completion.handoffPath}</code>
            </dd>
          </div>
        </dl>
      ) : null}

      {state.status === 'Failed' && state.failure ? (
        <section className="cc-plan-failure" role="alert" aria-label="Execution failed">
          <div className="cc-plan-failure-body">
            <span className="cc-plan-eyebrow cc-plan-failure-eyebrow">
              {state.failure.phase ? `Execution failed: ${state.failure.phase}` : 'Execution failed'}
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

function resolveSteps(state: ExecutionRunState) {
  const milestonesDone = state.milestoneCount !== null
  const commitDone = state.commit !== null
  const handoffDone = state.handoff !== null || state.status === 'Completed'
  // StartExecution is reached once the backend reports the lifecycle/phase transition; the
  // handoff arriving (or completion) is the strongest proxy the UI has for it being underway.
  const executionStarted = state.phase === 'StartExecution' || handoffDone

  const stepState = (done: boolean, active: boolean): StepState => {
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
      note: state.handoff ? state.handoff.path : null,
    },
  ] as Array<(typeof PHASE_STEPS)[number] & { state: StepState; note: string | null }>
}

function shortSha(sha: string) {
  return sha.length > 10 ? sha.slice(0, 10) : sha
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
