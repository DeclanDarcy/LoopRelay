import { useEffect, useState } from 'react'
import { Button } from '../../components/design'
import { useDecisionStream, useExecutionStream, usePlanStream } from '../../hooks'
import { DecisionRuntimeView } from '../decision/DecisionRuntimeView'
import { ExecutionStreamView } from './ExecutionStreamView'
import { PlanFailureNotice } from './PlanFailureNotice'
import { PlanStreamView } from './PlanStreamView'
import { RenderedPlanView } from './RenderedPlanView'
import { RoadmapField } from './RoadmapField'
import { SpecList } from './SpecList'
import './PlanAuthoring.css'

type PlanAuthoringScreenProps = {
  repositoryId: string
  repositoryName: string
  onSessionActiveChange?: (isActive: boolean) => void
  onExecuted?: () => void
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
}: PlanAuthoringScreenProps) {
  const [roadmap, setRoadmap] = useState('')
  const [specs, setSpecs] = useState<string[]>([])
  const [newCodebase, setNewCodebase] = useState(false)
  const [feedback, setFeedback] = useState('')
  // Decisions follow execution: once the execution run completes the screen stays mounted and
  // surfaces the decision runtime in place, so the human-review gate runs before navigation.
  const [decisionPhase, setDecisionPhase] = useState(false)
  const { state, error, submitWrite, submitRevise, submitExecute, dismissFailure } =
    usePlanStream(repositoryId)

  const isTurnRunning = state.status === 'Planning' || state.status === 'Revising'
  const isExecuting = state.status === 'Executing'
  const { state: executionState } = useExecutionStream(repositoryId, isExecuting)
  const {
    state: decisionState,
    generateDecisions,
    editDecisions,
    submitReviewedDecisions,
  } = useDecisionStream(repositoryId, decisionPhase)
  const inputsDisabled = isTurnRunning || isExecuting || decisionPhase
  const canWrite = roadmap.trim().length > 0 && !inputsDisabled
  const hasPlan = state.plan !== null
  const canRevise = feedback.trim().length > 0 && hasPlan && !inputsDisabled
  const canExecute = hasPlan && !inputsDisabled

  const isSessionActive = (ACTIVE_SESSION_STATUSES as readonly string[]).includes(state.status)

  useEffect(() => {
    onSessionActiveChange?.(isSessionActive)
  }, [isSessionActive, onSessionActiveChange])

  // The screen stays mounted through the whole execution run so the user can watch each phase
  // stream in. When the run completes, the decision phase begins in place rather than navigating.
  useEffect(() => {
    if (isExecuting && executionState.status === 'Completed') {
      setDecisionPhase(true)
    }
  }, [executionState.status, isExecuting])

  // Navigation to the workspace only happens once the human-review gate closes — that is, after
  // the reviewer submits the decisions and the backend confirms they were persisted.
  useEffect(() => {
    if (decisionPhase && decisionState.status === 'Submitted') {
      onExecuted?.()
    }
  }, [decisionPhase, decisionState.status, onExecuted])

  const writePlanNow = () => {
    if (!canWrite) {
      return
    }

    void submitWrite({
      roadmap,
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
          This repository has no plan yet. Draft a roadmap, watch the plan stream in, then revise or
          execute it.
        </p>
      </header>

      {error && state.status !== 'Failed' ? (
        <p className="cc-plan-inline-error" role="alert">
          {error}
        </p>
      ) : null}

      <div className="cc-plan-authoring">
        <RoadmapField value={roadmap} disabled={inputsDisabled} onChange={setRoadmap} />

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

      {isTurnRunning ? (
        <PlanStreamView text={state.streamedText} turnPhase={state.turnPhase} />
      ) : null}

      {isExecuting ? (
        <ExecutionStreamView
          state={executionState}
          onDismissFailure={() => {
            // Return to the plan controls. The plan is preserved, so reset lands on PlanReady.
            dismissFailure()
          }}
        />
      ) : null}

      {decisionPhase ? (
        <DecisionRuntimeView
          state={decisionState}
          onGenerate={() => void generateDecisions()}
          onEditDecisions={editDecisions}
          onSubmitDecisions={(decisions) => void submitReviewedDecisions(decisions)}
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
