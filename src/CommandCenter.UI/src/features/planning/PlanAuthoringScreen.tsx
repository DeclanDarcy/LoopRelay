import { useEffect, useRef, useState } from 'react'
import { Button } from '../../components/design'
import { usePlanStream } from '../../hooks'
import { PlanFailureNotice } from './PlanFailureNotice'
import { PlanStreamView } from './PlanStreamView'
import { RenderedPlanView } from './RenderedPlanView'
import { RoadmapField } from './RoadmapField'
import { SpecList } from './SpecList'
import './PlanAuthoring.css'

type PlanAuthoringScreenProps = {
  repositoryId: string
  repositoryName: string
  onPlanReady?: () => void
  onExecuted?: () => void
}

export function PlanAuthoringScreen({
  repositoryId,
  repositoryName,
  onPlanReady,
  onExecuted,
}: PlanAuthoringScreenProps) {
  const [roadmap, setRoadmap] = useState('')
  const [specs, setSpecs] = useState<string[]>([])
  const [newCodebase, setNewCodebase] = useState(false)
  const [feedback, setFeedback] = useState('')
  const { state, error, submitWrite, submitRevise, submitExecute, dismissFailure } =
    usePlanStream(repositoryId)
  const lastReadyPlan = useRef<string | null>(null)

  const isTurnRunning = state.status === 'Planning' || state.status === 'Revising'
  const isExecuting = state.status === 'Executing'
  const inputsDisabled = isTurnRunning || isExecuting
  const canWrite = roadmap.trim().length > 0 && !inputsDisabled
  const hasPlan = state.plan !== null
  const canRevise = feedback.trim().length > 0 && hasPlan && !inputsDisabled
  const canExecute = hasPlan && !inputsDisabled

  useEffect(() => {
    if (state.status === 'PlanReady' && state.plan && state.plan !== lastReadyPlan.current) {
      lastReadyPlan.current = state.plan
      onPlanReady?.()
    }
  }, [onPlanReady, state.plan, state.status])

  useEffect(() => {
    if (state.status === 'Executing') {
      onExecuted?.()
    }
  }, [onExecuted, state.status])

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

      {(state.status === 'PlanReady' || isExecuting) && state.plan ? (
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
                {state.status === 'Revising' ? 'Revising plan…' : 'Revise Plan'}
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
                {isExecuting ? 'Executing plan…' : 'Execute Plan'}
              </Button>
            </div>
          </div>
        </>
      ) : null}
    </section>
  )
}
