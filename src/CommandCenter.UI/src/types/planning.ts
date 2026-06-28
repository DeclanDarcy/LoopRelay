export type PlanPhase = 'PlanAuthoring' | 'ExecutingPlan'

export type PlanTurnPhase = 'WritePlan' | 'RevisePlan'

export type PlanStatus = {
  planExists: boolean
  state: PlanPhase
}

export type WritePlanRequest = {
  roadmap: string
  specs: string[]
  newCodebase: boolean
}

export type PlanTurnStartedEvent = {
  type: 'turn-started'
  phase: PlanTurnPhase
}

export type PlanDeltaEvent = {
  type: 'delta'
  text: string
}

export type PlanCompletedEvent = {
  type: 'completed'
  plan: string
  promptTokens: number
  outputTokens: number
}

export type PlanFailedEvent = {
  type: 'failed'
  reason: string
  detail?: string
}

export type PlanStreamEvent =
  | PlanTurnStartedEvent
  | PlanDeltaEvent
  | PlanCompletedEvent
  | PlanFailedEvent

export type PlanAuthoringStatus =
  | 'Authoring'
  | 'Planning'
  | 'PlanReady'
  | 'Revising'
  | 'Executing'
  | 'Failed'

export type PlanCompletionTokens = {
  promptTokens: number
  outputTokens: number
}

export type PlanAuthoringState = {
  status: PlanAuthoringStatus
  turnPhase: PlanTurnPhase | null
  streamedText: string
  plan: string | null
  tokens: PlanCompletionTokens | null
  failure: { reason: string; detail: string | null } | null
}
