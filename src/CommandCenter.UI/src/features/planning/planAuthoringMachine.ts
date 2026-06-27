import type { PlanAuthoringState, PlanStreamEvent } from '../../types'

export const initialPlanAuthoringState: PlanAuthoringState = {
  status: 'Authoring',
  turnPhase: null,
  streamedText: '',
  plan: null,
  tokens: null,
  failure: null,
}

export type PlanAuthoringAction =
  | { kind: 'write-submitted' }
  | { kind: 'revise-submitted' }
  | { kind: 'execute-submitted' }
  | { kind: 'command-failed'; reason: string; detail?: string }
  | { kind: 'event'; event: PlanStreamEvent }
  | { kind: 'reset' }

export function planAuthoringReducer(
  state: PlanAuthoringState,
  action: PlanAuthoringAction,
): PlanAuthoringState {
  switch (action.kind) {
    case 'write-submitted':
      return {
        ...state,
        status: 'Planning',
        turnPhase: 'WritePlan',
        streamedText: '',
        failure: null,
      }
    case 'revise-submitted':
      return {
        ...state,
        status: 'Revising',
        turnPhase: 'RevisePlan',
        streamedText: '',
        failure: null,
      }
    case 'execute-submitted':
      return {
        ...state,
        status: 'Executing',
        turnPhase: null,
        failure: null,
      }
    case 'command-failed':
      return {
        ...state,
        status: 'Failed',
        turnPhase: null,
        failure: { reason: action.reason, detail: action.detail ?? null },
      }
    case 'reset':
      return {
        ...initialPlanAuthoringState,
        plan: state.plan,
        tokens: state.tokens,
        status: state.plan ? 'PlanReady' : 'Authoring',
      }
    case 'event':
      return reduceEvent(state, action.event)
    default:
      return state
  }
}

function reduceEvent(state: PlanAuthoringState, event: PlanStreamEvent): PlanAuthoringState {
  switch (event.type) {
    case 'turn-started':
      return {
        ...state,
        status: event.phase === 'RevisePlan' ? 'Revising' : 'Planning',
        turnPhase: event.phase,
        streamedText: '',
        failure: null,
      }
    case 'delta':
      return {
        ...state,
        streamedText: state.streamedText + event.text,
      }
    case 'completed':
      return {
        ...state,
        status: 'PlanReady',
        turnPhase: null,
        plan: event.plan,
        tokens: { promptTokens: event.promptTokens, outputTokens: event.outputTokens },
      }
    case 'failed':
      return {
        ...state,
        status: 'Failed',
        turnPhase: null,
        failure: { reason: event.reason, detail: event.detail ?? null },
      }
    default:
      return state
  }
}
