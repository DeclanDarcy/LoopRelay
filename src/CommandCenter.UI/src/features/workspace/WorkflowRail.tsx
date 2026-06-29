import { memo } from 'react'
import type { WorkflowInstance, WorkflowProgressState, WorkflowStage } from '../../types'

type WorkflowRailProps = {
  workflow: WorkflowInstance | null
  isLoading?: boolean
  error?: string | null
  variant?: 'workspace' | 'execution'
}

type WorkflowRailStepState = 'complete' | 'current' | 'pending' | 'blocked'

type WorkflowRailStep = {
  key: string
  label: string
  detail: string
  meta?: string
  state: WorkflowRailStepState
}

const terminalStages = new Set<WorkflowStage>(['Completed'])
const blockedStages = new Set<WorkflowStage>(['Blocked', 'Failed'])
const terminalProgressStates = new Set<WorkflowProgressState>(['Completed'])
const blockedProgressStates = new Set<WorkflowProgressState>(['Blocked', 'Failed'])

function workflowStageState(stage: WorkflowStage): WorkflowRailStepState {
  if (terminalStages.has(stage)) {
    return 'complete'
  }

  if (blockedStages.has(stage)) {
    return 'blocked'
  }

  return stage === 'Unknown' ? 'pending' : 'current'
}

function workflowProgressState(progressState: WorkflowProgressState): WorkflowRailStepState {
  if (terminalProgressStates.has(progressState)) {
    return 'complete'
  }

  if (blockedProgressStates.has(progressState)) {
    return 'blocked'
  }

  return progressState === 'Ready' ? 'pending' : 'current'
}

function firstPresent(values: Array<string | null | undefined>, fallback: string) {
  return values.find((value) => value && value.trim().length > 0) ?? fallback
}

function buildWorkflowRailSteps(workflow: WorkflowInstance): WorkflowRailStep[] {
  const blockingGate = workflow.openGates[0] ?? null
  const currentTransition = workflow.validTransitions[0] ?? null
  const blockedTransition = workflow.blockedTransitions[0] ?? null
  const reasoning = firstPresent(workflow.diagnostics.reasoning, 'No workflow reasoning projected.')
  const satisfyingCommand = firstPresent(
    [
      blockingGate?.satisfyingCommands[0],
      blockingGate?.satisfyingCommand,
      currentTransition?.transition.description,
    ],
    'No command required.',
  )
  const requiredHumanAction = firstPresent(
    [workflow.requiredHumanAction, blockingGate?.requiredAction],
    'No human action required.',
  )

  return [
    {
      key: 'stage',
      label: `Stage: ${workflow.currentStage}`,
      detail: workflow.progressState,
      meta: `Next: ${workflow.nextPossibleStages.join(', ') || 'None'}`,
      state: workflowStageState(workflow.currentStage),
    },
    {
      key: 'progress',
      label: 'Progress',
      detail: reasoning,
      meta: `Valid transitions: ${workflow.validTransitions.length}`,
      state: workflowProgressState(workflow.progressState),
    },
    {
      key: 'gate',
      label: `Gate: ${workflow.blockingGate}`,
      detail: blockingGate?.reason ?? 'No blocking gate is open.',
      meta: satisfyingCommand,
      state: workflow.blockingGate === 'None' ? 'complete' : 'blocked',
    },
    {
      key: 'action',
      label: 'Required Action',
      detail: requiredHumanAction,
      meta: `Open gates: ${workflow.openGates.length}`,
      state:
        workflow.requiredHumanAction || blockingGate?.requiredAction
          ? workflow.progressState === 'Blocked'
            ? 'blocked'
            : 'current'
          : 'complete',
    },
    {
      key: 'transition',
      label: 'Current Transition',
      detail:
        currentTransition?.transition.description ??
        blockedTransition?.reason ??
        'No transition is currently projected.',
      meta: currentTransition
        ? `${currentTransition.transition.fromStage} -> ${currentTransition.transition.toStage}`
        : blockedTransition
          ? `${blockedTransition.transition.fromStage} -> ${blockedTransition.transition.toStage}`
          : undefined,
      state: currentTransition ? 'current' : workflow.progressState === 'Completed' ? 'complete' : 'pending',
    },
  ]
}

function WorkflowRailImpl({
  workflow,
  isLoading = false,
  error = null,
  variant = 'workspace',
}: WorkflowRailProps) {
  const steps = workflow ? buildWorkflowRailSteps(workflow) : []
  const classPrefix = variant === 'execution' ? 'execution-workflow' : 'workspace-workflow'
  const ariaLabel = variant === 'execution' ? 'Execution lifecycle' : 'Workspace workflow state'

  return (
    <div className={`${classPrefix}-rail`} aria-label={ariaLabel}>
      {workflow ? (
        steps.map((step) => (
          <div
            className={`${classPrefix}-step ${classPrefix}-step-${step.state}`}
            key={step.key}
          >
            <span>{step.label}</span>
            <small>{step.detail}</small>
            {step.meta ? <small className="workflow-step-meta">{step.meta}</small> : null}
          </div>
        ))
      ) : (
        <div className={`${classPrefix}-step ${classPrefix}-step-pending`}>
          <span>Workflow</span>
          <small>{error ?? (isLoading ? 'Loading workflow projection...' : 'Workflow projection is not loaded.')}</small>
        </div>
      )}
    </div>
  )
}

export const WorkflowRail = memo(WorkflowRailImpl)
