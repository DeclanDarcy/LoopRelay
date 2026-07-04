import { useEffect, useState } from 'react'
import { Button } from '../../components/design'
import { useDecisionStream, useExecutionStream, usePlanStream } from '../../hooks'
import { StreamFailurePanel } from '../streams'
import { DecisionRuntimeView } from '../decision/DecisionRuntimeView'
import { ExecutionStreamView } from './ExecutionStreamView'
import { PlanFailureNotice } from './PlanFailureNotice'
import { PlanStreamView } from './PlanStreamView'
import { RenderedPlanView } from './RenderedPlanView'
import { EpicField } from './EpicField'
import { SpecList } from './SpecList'
import './PlanAuthoring.css'

type PlanAuthoringScreenProps = {
  repositoryId: string
  repositoryName: string
  onSessionActiveChange?: (isActive: boolean) => void
  onExecuted?: () => void
  // Whether the backend has a verified `.agents/plan.md`. Execute requires this in addition to the
  // in-memory streamed plan, so a turn cannot execute before the plan is durably written. Defaults
  // to true so callers that don't gate (legacy/tests) keep their prior behavior.
  planExists?: boolean
  // Re-fetches the backend plan-status. Called in place when the plan stream reaches PlanReady —
  // the backend writes plan.md before emitting `completed`, so the client's planExists may be stale.
  // The return value is ignored (the refreshed status flows back via the planExists prop).
  refreshPlanStatus?: () => void | Promise<unknown>
}

// The authoring session owns an in-place lifecycle that must outlive the durable
// `planExists` flag: once a plan is written the backend reports planExists=true, but the
// user still needs the rendered plan, Revise, and Execute controls. These reducer states
// mean "keep the authoring screen mounted regardless of planExists".
const ACTIVE_SESSION_STATUSES = ['Planning', 'PlanReady', 'Revising', 'Executing'] as const

export function PlanAuthoringScreen({
  repositoryId,
  repositoryName,
  onSessionActiveChange,
  onExecuted,
  planExists = true,
  refreshPlanStatus,
}: PlanAuthoringScreenProps) {
  const [epic, setEpic] = useState('')
  const [specs, setSpecs] = useState<string[]>([])
  const [newCodebase, setNewCodebase] = useState(false)
  const [feedback, setFeedback] = useState('')
  // Decisions follow execution: once the execution run completes the screen stays mounted and
  // surfaces the decision runtime in place, so the human-review gate runs before navigation.
  const [decisionPhase, setDecisionPhase] = useState(false)
  const {
    state,
    error,
    isReconnecting: isPlanReconnecting,
    transportFailed: isPlanTransportFailed,
    submitWrite,
    submitRevise,
    submitExecute,
    dismissFailure,
  } = usePlanStream(repositoryId)

  const isTurnRunning = state.status === 'Planning' || state.status === 'Revising'
  const isExecuting = state.status === 'Executing'
  // A plan-stream give-up takes over the turn surface as a recoverable failure, mirroring the
  // execution and decision streams — otherwise a dropped plan stream leaves an indefinite spinner.
  const showPlanTransportFailure = isPlanTransportFailed && state.status !== 'Failed'
  // The execution stream stays subscribed for the whole continuation loop, not just the first
  // run: after decisions are submitted the server reuses it for the ContinueExecution turn.
  const {
    state: executionState,
    isReconnecting: isExecutionReconnecting,
    transportFailed: executionTransportFailed,
  } = useExecutionStream(repositoryId, isExecuting || decisionPhase)
  const {
    state: decisionState,
    isReconnecting: isDecisionReconnecting,
    transportFailed: decisionTransportFailed,
    generateDecisions,
    editDecisions,
    submitReviewedDecisions,
  } = useDecisionStream(repositoryId, decisionPhase)
  const inputsDisabled = isTurnRunning || isExecuting || decisionPhase
  // A continuation turn is streaming on the execution stream when, inside the decision phase, the
  // execution run is live again. While it streams the execution surface takes the foreground.
  const continuationRunning = decisionPhase && executionState.status === 'Running'
  const canWrite = epic.trim().length > 0 && !inputsDisabled
  const hasPlan = state.plan !== null
  const canRevise = feedback.trim().length > 0 && hasPlan && !inputsDisabled
  // Execute requires BOTH the in-memory streamed plan AND the backend's verified plan.md, so a turn
  // cannot execute before the plan is durably written. planExists comes from the backend plan-status.
  const canExecute = hasPlan && planExists && !inputsDisabled

  const isSessionActive = (ACTIVE_SESSION_STATUSES as readonly string[]).includes(state.status)

  useEffect(() => {
    onSessionActiveChange?.(isSessionActive)
  }, [isSessionActive, onSessionActiveChange])

  // The backend writes plan.md BEFORE emitting the plan-stream `completed` event, so by PlanReady
  // the backend already has the plan — but the client's planStatus may be stale. Refresh it in
  // place when the stream reaches PlanReady. This is safe: the isAuthoringSessionActive latch keeps
  // the screen mounted regardless of planExists, so refreshing will not unmount it (the m3 invariant).
  //
  // The refresh resolves to the fetched PlanStatus on success and to a falsy value when the status
  // fetch transiently fails. A single fire-and-forget call would leave Execute disabled with no
  // recovery on a flaky fetch, so retry a couple of times with small backoff until it succeeds. The
  // happy path (refresh after `completed`) resolves truthy on the first try, so no retry fires.
  useEffect(() => {
    if (state.status !== 'PlanReady') {
      return
    }

    let isCurrent = true
    const maxAttempts = 3

    const attemptRefresh = async (attempt: number) => {
      const result = await refreshPlanStatus?.()
      if (!isCurrent || result || attempt >= maxAttempts) {
        return
      }

      // The status fetch failed; back off briefly and retry so Execute is not stranded.
      window.setTimeout(() => {
        if (isCurrent) {
          void attemptRefresh(attempt + 1)
        }
      }, 200 * attempt)
    }

    void attemptRefresh(1)

    return () => {
      isCurrent = false
    }
  }, [state.status, refreshPlanStatus])

  // The screen stays mounted through the whole execution run so the user can watch each phase
  // stream in. When the FIRST run completes, the decision phase begins in place rather than
  // navigating. Continuation runs (inside the decision phase) keep the phase open — they are
  // driven by the server and return to the decision stream automatically.
  useEffect(() => {
    if (isExecuting && executionState.status === 'Completed') {
      setDecisionPhase(true)
    }
  }, [executionState.status, isExecuting])

  const writePlanNow = () => {
    if (!canWrite) {
      return
    }

    void submitWrite({
      epic,
      specs: specs.map((spec) => spec.trim()).filter((spec) => spec.length > 0),
      newCodebase,
    })
  }

  const changeSpec = (index: number, value: string) => {
    setSpecs((current) => current.map((spec, specIndex) => (specIndex === index ? value : spec)))
  }

  const addSpec = () => setSpecs((current) => [...current, ''])

  const removeSpec = (index: number) => {
    setSpecs((current) => current.filter((_, specIndex) => specIndex !== index))
  }

  return (
    <section className="cc-plan-screen" aria-label="Plan authoring">
      <header className="cc-plan-masthead">
        <p className="cc-plan-kicker">{repositoryName}</p>
        <h1 className="cc-plan-title">Author the implementation plan</h1>
        <p className="cc-plan-lede">
          This repository has no plan yet. Draft a epic, watch the plan stream in, then revise or
          execute it.
        </p>
      </header>

      {error && state.status !== 'Failed' ? (
        <p className="cc-plan-inline-error" role="alert">
          {error}
        </p>
      ) : null}

      <div className="cc-plan-authoring">
        <EpicField value={epic} disabled={inputsDisabled} onChange={setEpic} />

        <SpecList
          specs={specs}
          disabled={inputsDisabled}
          onChangeSpec={changeSpec}
          onAddSpec={addSpec}
          onRemoveSpec={removeSpec}
        />

        <label className="cc-plan-checkbox">
          <input
            type="checkbox"
            checked={newCodebase}
            disabled={inputsDisabled}
            onChange={(event) => setNewCodebase(event.target.checked)}
          />
          <span>New codebase</span>
        </label>

        <div className="cc-plan-primary-actions">
          <Button type="button" variant="primary" onClick={writePlanNow} disabled={!canWrite}>
            {state.status === 'Planning' ? 'Writing plan…' : 'Write Plan'}
          </Button>
        </div>
      </div>

      {state.status === 'Failed' && state.failure ? (
        <PlanFailureNotice
          reason={state.failure.reason}
          detail={state.failure.detail}
          onDismiss={() => {
            setFeedback('')
            dismissFailure()
          }}
        />
      ) : null}

      {isTurnRunning && showPlanTransportFailure ? (
        <StreamFailurePanel
          ariaLabel="Planning connection lost"
          eyebrow="Planning connection lost"
          reason="The connection to the planning stream dropped and could not be restored. The turn may still be progressing on the backend."
          onDismiss={dismissFailure}
          dismissLabel="Back to plan"
        />
      ) : isTurnRunning ? (
        <PlanStreamView
          text={state.streamedText}
          turnPhase={state.turnPhase}
          isReconnecting={isPlanReconnecting}
        />
      ) : null}

      {isExecuting || continuationRunning ? (
        <ExecutionStreamView
          state={executionState}
          isReconnecting={isExecutionReconnecting}
          transportFailed={executionTransportFailed}
          onDismissFailure={() => {
            // Return to the plan controls. The plan is preserved, so reset lands on PlanReady.
            setDecisionPhase(false)
            dismissFailure()
          }}
        />
      ) : null}

      {decisionPhase ? (
        <DecisionRuntimeView
          state={decisionState}
          isReconnecting={isDecisionReconnecting}
          transportFailed={decisionTransportFailed}
          onGenerate={() => void generateDecisions()}
          onEditDecisions={editDecisions}
          onSubmitDecisions={(decisions) => void submitReviewedDecisions(decisions)}
          onFinish={() => {
            // The reviewer explicitly leaves the continuation loop for the workspace. This is the
            // only navigation away — submitting alone keeps the loop running.
            setDecisionPhase(false)
            onExecuted?.()
          }}
          onDismissFailure={() => {
            // Leave the decision phase and return to the plan controls. The plan is preserved.
            setDecisionPhase(false)
            dismissFailure()
          }}
        />
      ) : null}

      {state.status === 'PlanReady' && state.plan ? (
        <>
          <RenderedPlanView plan={state.plan} tokens={state.tokens} />

          <div className="cc-plan-field cc-plan-revise">
            <label className="cc-plan-field-label" htmlFor="cc-plan-feedback">
              Feedback
            </label>
            <p className="cc-plan-field-hint">
              Tell the agent what to change, then revise the plan in place.
            </p>
            <textarea
              id="cc-plan-feedback"
              className="cc-plan-textarea"
              aria-label="Feedback"
              placeholder="Split the first milestone into setup and migration…"
              rows={4}
              value={feedback}
              disabled={inputsDisabled}
              onChange={(event) => setFeedback(event.target.value)}
            />
            <div className="cc-plan-secondary-actions">
              <Button
                type="button"
                variant="secondary"
                onClick={() => {
                  if (canRevise) {
                    void submitRevise(feedback.trim())
                  }
                }}
                disabled={!canRevise}
              >
                Revise Plan
              </Button>
              <Button
                type="button"
                variant="primary"
                onClick={() => {
                  if (canExecute) {
                    void submitExecute()
                  }
                }}
                disabled={!canExecute}
              >
                Execute Plan
              </Button>
            </div>
          </div>
        </>
      ) : null}
    </section>
  )
}
