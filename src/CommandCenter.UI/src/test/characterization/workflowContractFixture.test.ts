/// <reference types="node" />

import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import type {
  DecisionSessionWorkflowDiagnostics,
  WorkflowCompletionEvaluation,
  WorkflowDecisionDiagnostics,
  WorkflowDecisionProjection,
  WorkflowExecutionDiagnostics,
  WorkflowExecutionFailure,
  WorkflowExecutionProjection,
  WorkflowGate,
  WorkflowGateDiagnostics,
  WorkflowGateEvidence,
  WorkflowGateResolution,
  WorkflowGitDiagnostics,
  WorkflowGitProjection,
  WorkflowHandoffDiagnostics,
  WorkflowHandoffProjection,
  WorkflowHandoffValidation,
  WorkflowInstance,
  WorkflowOperationalContextDiagnostics,
  WorkflowOperationalContextProjection,
  WorkflowProjectionDiagnostics,
  WorkflowStateMachineDiagnostics,
  WorkflowTimelineEntry,
  WorkflowTransition,
  WorkflowTransitionResult,
} from '../../types/workflow'

type PrimitiveSchema = 'primitive'
type ArraySchema<T> = readonly [] | readonly [Schema<T>]
type ObjectSchema<T extends object> = { readonly [K in keyof Required<T>]: Schema<NonNullable<T[K]>> }
type Schema<T> = T extends readonly (infer U)[]
  ? ArraySchema<U>
  : T extends object
    ? ObjectSchema<T>
    : PrimitiveSchema
type AnySchema = PrimitiveSchema | readonly AnySchema[] | { readonly [key: string]: AnySchema }

const primitive = 'primitive' as const

const executionDiagnosticsSchema = {
  repositoryId: primitive,
  includedEvidence: [primitive],
  missingEvidence: [primitive],
  conflicts: [primitive],
  reasoning: [primitive],
} satisfies ObjectSchema<WorkflowExecutionDiagnostics>

const executionFailureSchema = {
  reason: primitive,
  failedAt: primitive,
  sourceArtifact: primitive,
} satisfies ObjectSchema<WorkflowExecutionFailure>

const handoffValidationSchema = {
  isValid: primitive,
  checks: [primitive],
  failures: [primitive],
} satisfies ObjectSchema<WorkflowHandoffValidation>

const handoffDiagnosticsSchema = {
  repositoryId: primitive,
  includedEvidence: [primitive],
  missingEvidence: [primitive],
  conflicts: [primitive],
  reasoning: [primitive],
} satisfies ObjectSchema<WorkflowHandoffDiagnostics>

const decisionDiagnosticsSchema = {
  repositoryId: primitive,
  projectionInputs: [primitive],
  reasoning: [primitive],
  governanceSignals: [primitive],
  qualitySignals: [primitive],
  certificationSignals: [primitive],
  supersessionSignals: [primitive],
  conflicts: [primitive],
} satisfies ObjectSchema<WorkflowDecisionDiagnostics>

const operationalContextDiagnosticsSchema = {
  repositoryId: primitive,
  projectionInputs: [primitive],
  reasoning: [primitive],
  reviewSignals: [primitive],
  promotionSignals: [primitive],
  linkageSignals: [primitive],
  conflicts: [primitive],
} satisfies ObjectSchema<WorkflowOperationalContextDiagnostics>

const completionEvaluationSchema = {
  repositoryId: primitive,
  isComplete: primitive,
  completionReason: primitive,
  completionArtifact: primitive,
  evidence: [primitive],
  diagnostics: [primitive],
} satisfies ObjectSchema<WorkflowCompletionEvaluation>

const gitDiagnosticsSchema = {
  repositoryId: primitive,
  includedEvidence: [primitive],
  missingEvidence: [primitive],
  commitSignals: [primitive],
  pushSignals: [primitive],
  reasoning: [primitive],
  conflicts: [primitive],
} satisfies ObjectSchema<WorkflowGitDiagnostics>

const gateEvidenceSchema = {
  sourceDomain: primitive,
  sourceArtifact: primitive,
  summary: primitive,
  observedAt: primitive,
  fingerprint: primitive,
} satisfies ObjectSchema<WorkflowGateEvidence>

const gateSchema = {
  gateId: primitive,
  type: primitive,
  repositoryId: primitive,
  stage: primitive,
  status: primitive,
  requiredAction: primitive,
  satisfyingCommand: primitive,
  satisfyingCommands: [primitive],
  sourceDomain: primitive,
  sourceArtifact: primitive,
  createdAt: primitive,
  satisfiedAt: primitive,
  satisfiedActor: primitive,
  reason: primitive,
  evidence: [gateEvidenceSchema],
} satisfies ObjectSchema<WorkflowGate>

const gateResolutionSchema = {
  gateType: primitive,
  blockingCondition: primitive,
  requiredHumanAction: primitive,
  isSatisfied: primitive,
} satisfies ObjectSchema<WorkflowGateResolution>

const transitionSchema = {
  fromStage: primitive,
  toStage: primitive,
  requiredGate: primitive,
  blockingCondition: primitive,
  description: primitive,
} satisfies ObjectSchema<WorkflowTransition>

const transitionResultSchema = {
  transition: transitionSchema,
  isValid: primitive,
  isBlocked: primitive,
  gateResolution: gateResolutionSchema,
  blockingCondition: primitive,
  reason: primitive,
} satisfies ObjectSchema<WorkflowTransitionResult>

const stateMachineDiagnosticsSchema = {
  repositoryId: primitive,
  currentStage: primitive,
  progressState: primitive,
  blockingGate: primitive,
  candidateStages: [primitive],
  validTransitions: [transitionResultSchema],
  blockedTransitions: [transitionResultSchema],
  reasoning: [primitive],
  rejectedTransitions: [primitive],
} satisfies ObjectSchema<WorkflowStateMachineDiagnostics>

const projectionDiagnosticsSchema = {
  repositoryId: primitive,
  projectionInputs: [primitive],
  chosenStage: primitive,
  chosenGate: primitive,
  nextPossibleStages: [primitive],
  validTransitions: [transitionResultSchema],
  blockedTransitions: [transitionResultSchema],
  stateMachine: stateMachineDiagnosticsSchema,
  reasoning: [primitive],
  unknownStates: [primitive],
  conflicts: [primitive],
} satisfies ObjectSchema<WorkflowProjectionDiagnostics>

const gateDiagnosticsSchema = {
  repositoryId: primitive,
  blockingGate: primitive,
  openGates: [gateSchema],
  satisfiedGates: [gateSchema],
  gateCommandMap: [primitive],
  reasoning: [primitive],
  missingEvidence: [primitive],
  conflicts: [primitive],
} satisfies ObjectSchema<WorkflowGateDiagnostics>

const executionProjectionSchema = {
  repositoryId: primitive,
  executionId: primitive,
  status: primitive,
  startedAt: primitive,
  completedAt: primitive,
  failedAt: primitive,
  acceptedAt: primitive,
  rejectedAt: primitive,
  committedAt: primitive,
  pushedAt: primitive,
  commitSha: primitive,
  pushedCommitSha: primitive,
  hasHandoff: primitive,
  hasChanges: primitive,
  failureReason: primitive,
  isExecutionEligible: primitive,
  failure: executionFailureSchema,
  diagnostics: executionDiagnosticsSchema,
} satisfies ObjectSchema<WorkflowExecutionProjection>

const handoffProjectionSchema = {
  repositoryId: primitive,
  executionId: primitive,
  handoffId: primitive,
  handoffPath: primitive,
  status: primitive,
  createdAt: primitive,
  acceptedAt: primitive,
  rejectedAt: primitive,
  hasChanges: primitive,
  summary: primitive,
  validation: handoffValidationSchema,
  diagnostics: handoffDiagnosticsSchema,
} satisfies ObjectSchema<WorkflowHandoffProjection>

const decisionProjectionSchema = {
  repositoryId: primitive,
  decisionId: primitive,
  candidateId: primitive,
  candidateState: primitive,
  proposalId: primitive,
  packageId: primitive,
  status: primitive,
  reviewState: primitive,
  resolutionState: primitive,
  humanAuthoringBurden: primitive,
  createdAt: primitive,
  resolvedAt: primitive,
  isResolutionEligible: primitive,
  isGovernanceBlocked: primitive,
  governanceStatus: primitive,
  qualityStatus: primitive,
  certificationStatus: primitive,
  replacementDecisionId: primitive,
  diagnostics: decisionDiagnosticsSchema,
} satisfies ObjectSchema<WorkflowDecisionProjection>

const operationalContextProjectionSchema = {
  repositoryId: primitive,
  proposalId: primitive,
  status: primitive,
  reviewState: primitive,
  promotionState: primitive,
  createdAt: primitive,
  reviewedAt: primitive,
  promotedAt: primitive,
  reviewer: primitive,
  summary: primitive,
  sourceDecisionId: primitive,
  sourceExecutionId: primitive,
  isReviewEligible: primitive,
  isPromotionEligible: primitive,
  isCommitEligible: primitive,
  diagnostics: operationalContextDiagnosticsSchema,
} satisfies ObjectSchema<WorkflowOperationalContextProjection>

const gitProjectionSchema = {
  repositoryId: primitive,
  commitStatus: primitive,
  pushStatus: primitive,
  commitId: primitive,
  branch: primitive,
  commitTimestamp: primitive,
  pushTimestamp: primitive,
  hasPendingChanges: primitive,
  hasUnpushedChanges: primitive,
  isCommitRequired: primitive,
  isPushRequired: primitive,
  isCommitGateOpen: primitive,
  isPushGateOpen: primitive,
  completion: completionEvaluationSchema,
  diagnostics: gitDiagnosticsSchema,
} satisfies ObjectSchema<WorkflowGitProjection>

const timelineEntrySchema = {
  eventType: primitive,
  stage: primitive,
  occurredAt: primitive,
  summary: primitive,
  sourceDomain: primitive,
  sourceArtifact: primitive,
  fingerprint: primitive,
} satisfies ObjectSchema<WorkflowTimelineEntry>

const decisionSessionDiagnosticsSchema = {
  repositoryId: primitive,
  isValid: primitive,
  evidence: [primitive],
  warnings: [primitive],
  errors: [primitive],
  generatedAt: primitive,
} satisfies ObjectSchema<DecisionSessionWorkflowDiagnostics>

const workflowInstanceSchema = {
  repositoryId: primitive,
  currentStage: primitive,
  progressState: primitive,
  blockingGate: primitive,
  requiredHumanAction: primitive,
  currentExecution: executionProjectionSchema,
  executionStatus: primitive,
  isExecutionEligible: primitive,
  executionFailure: executionFailureSchema,
  executionDiagnostics: executionDiagnosticsSchema,
  currentHandoff: handoffProjectionSchema,
  handoffStatus: primitive,
  handoffValidation: handoffValidationSchema,
  handoffDiagnostics: handoffDiagnosticsSchema,
  currentDecision: decisionProjectionSchema,
  decisionStatus: primitive,
  isDecisionResolutionEligible: primitive,
  isDecisionGovernanceBlocked: primitive,
  decisionDiagnostics: decisionDiagnosticsSchema,
  currentOperationalContext: operationalContextProjectionSchema,
  operationalContextStatus: primitive,
  isOperationalContextReviewEligible: primitive,
  isOperationalContextPromotionEligible: primitive,
  isOperationalContextCommitEligible: primitive,
  operationalContextDiagnostics: operationalContextDiagnosticsSchema,
  currentGit: gitProjectionSchema,
  gitCommitStatus: primitive,
  gitPushStatus: primitive,
  hasPendingGitChanges: primitive,
  hasUnpushedGitChanges: primitive,
  completionEvaluation: completionEvaluationSchema,
  gitDiagnostics: gitDiagnosticsSchema,
  nextPossibleStages: [primitive],
  validTransitions: [transitionResultSchema],
  blockedTransitions: [transitionResultSchema],
  timeline: [timelineEntrySchema],
  openGates: [gateSchema],
  satisfiedGates: [gateSchema],
  gateHistory: [gateSchema],
  gateDiagnostics: gateDiagnosticsSchema,
  diagnostics: projectionDiagnosticsSchema,
  decisionSession: {
    repositoryId: primitive,
    decisionSessionId: primitive,
    decisionSessionState: primitive,
    estimatedTokenCount: primitive,
    estimatedCacheTtl: primitive,
    estimatedCacheMissRisk: primitive,
    reuseScore: primitive,
    transferScore: primitive,
    coherenceScore: primitive,
    transferPressure: primitive,
    currentLifecycleDecision: primitive,
    transferEligibilityStatus: primitive,
    continuityArtifactId: primitive,
    continuityFingerprint: primitive,
    transferLineage: [],
    continuityArtifactLineage: [],
    governanceHealthDimensions: [],
    summary: {
      repositoryId: primitive,
      decisionSessionId: primitive,
      decisionSessionState: primitive,
      lifecycleDecision: primitive,
      transferEligibilityStatus: primitive,
      estimatedTokenCount: primitive,
      estimatedCacheTtl: primitive,
      estimatedCacheMissRisk: primitive,
      coherenceScore: primitive,
      transferPressure: primitive,
      healthStatus: primitive,
      highlights: [primitive],
      generatedAt: primitive,
    },
    readiness: {
      hasActiveSession: primitive,
      hasAnalysis: primitive,
      hasPolicy: primitive,
      hasEligibility: primitive,
      isTransferRecommended: primitive,
      isTransferEligible: primitive,
      hasContinuityArtifact: primitive,
      missingEvidence: [primitive],
      blockingSignals: [primitive],
    },
    diagnostics: decisionSessionDiagnosticsSchema,
    generatedAt: primitive,
  },
} satisfies ObjectSchema<WorkflowInstance>

function readWorkflowInstanceFixture(): unknown {
  const fixturePath = join(
    process.cwd(),
    '..',
    '..',
    'tests',
    'CommandCenter.Backend.Tests',
    'ContractFixtures',
    'workflow-instance.golden.json',
  )

  return JSON.parse(readFileSync(fixturePath, 'utf8')) as unknown
}

function diffSchema(value: unknown, schema: AnySchema, path = '$'): string[] {
  if (schema === primitive) {
    return []
  }

  if (Array.isArray(schema)) {
    if (!Array.isArray(value)) {
      return [`${path} expected array`]
    }

    const itemSchema = schema[0]
    if (itemSchema === undefined) {
      return []
    }

    return value.flatMap((item, index) => diffSchema(item, itemSchema, `${path}[${index}]`))
  }

  if (value === null) {
    return []
  }

  if (typeof value !== 'object') {
    return [`${path} expected object or null`]
  }

  const actual = value as Record<string, unknown>
  const objectSchema = schema as { readonly [key: string]: AnySchema }
  const expectedKeys = Object.keys(objectSchema)
  const actualKeys = Object.keys(actual)
  const missing = expectedKeys
    .filter((key) => !(key in actual))
    .map((key) => `${path}.${key} missing from backend fixture`)
  const unexpected = actualKeys
    .filter((key) => !(key in objectSchema))
    .map((key) => `${path}.${key} not represented by manual TypeScript workflow contract`)
  const nested = expectedKeys.flatMap((key) => diffSchema(actual[key], objectSchema[key], `${path}.${key}`))

  return [...missing, ...unexpected, ...nested]
}

describe('workflow contract fixture consumer verification', () => {
  it('keeps the manual TypeScript WorkflowInstance shape aligned with the backend golden fixture', () => {
    const fixture = readWorkflowInstanceFixture()
    const drift = diffSchema(fixture, workflowInstanceSchema)

    expect(drift).toEqual([])

    const workflow = fixture as WorkflowInstance
    expect(workflow.currentStage).toBe('Commit')
    expect(workflow.decisionSession).toBeNull()
  })
})
