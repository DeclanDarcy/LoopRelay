export type ReasoningEventFamily =
  | 'Hypothesis'
  | 'Alternative'
  | 'Contradiction'
  | 'Direction'
  | 'DecisionEvolution'
  | 'AssumptionEvolution'
  | 'ConstraintEvolution'
  | 'Evidence'
  | 'Thread'

export type ReasoningEventType =
  | 'HypothesisRaised'
  | 'HypothesisSupported'
  | 'HypothesisChallenged'
  | 'HypothesisInvalidated'
  | 'HypothesisRetired'
  | 'AlternativeIntroduced'
  | 'AlternativeCompared'
  | 'AlternativeRejected'
  | 'AlternativeRevisited'
  | 'AlternativeSelected'
  | 'ContradictionIdentified'
  | 'ContradictionInvestigated'
  | 'ContradictionResolved'
  | 'ContradictionAccepted'
  | 'ContradictionRecurred'
  | 'DirectionObserved'
  | 'DirectionReinforced'
  | 'DirectionShifted'
  | 'DirectionAbandoned'
  | 'DecisionSuperseded'
  | 'DecisionReframed'
  | 'DecisionReconsidered'
  | 'AssumptionIntroduced'
  | 'AssumptionChallenged'
  | 'AssumptionInvalidated'
  | 'AssumptionReplaced'
  | 'ConstraintIntroduced'
  | 'ConstraintModified'
  | 'ConstraintRetired'
  | 'EvidenceAdded'
  | 'ThreadStarted'
  | 'ThreadExtended'
  | 'ThreadForked'
  | 'ThreadMerged'

export type ReasoningReferenceKind =
  | 'Decision'
  | 'Proposal'
  | 'ProposalRevision'
  | 'Candidate'
  | 'OperationalContextRevision'
  | 'GovernanceFinding'
  | 'ExecutionProjection'
  | 'ExecutionOutput'
  | 'Handoff'
  | 'Artifact'
  | 'ReasoningEvent'
  | 'ReasoningThread'

export type ReasoningThreadTheme =
  | 'BeliefUnderInvestigation'
  | 'PathConsidered'
  | 'Conflict'
  | 'StrategicMovement'
  | 'DecisionEvolution'
  | 'AssumptionEvolution'
  | 'ConstraintEvolution'
  | 'EvidenceTrail'
  | 'General'

export type ReasoningRelationshipType =
  | 'CausedBy'
  | 'InfluencedBy'
  | 'Supports'
  | 'Challenges'
  | 'Contradicts'
  | 'Supersedes'
  | 'Extends'
  | 'DerivesFrom'
  | 'LeadsTo'
  | 'Replaces'
  | 'Invalidates'
  | 'Resolves'
  | 'Reopens'
  | 'BelongsTo'
  | 'ComparesWith'
  | 'SelectedOver'

export type ReasoningManualCaptureKind =
  | 'DecisionSuperseded'
  | 'DecisionReframed'
  | 'DecisionReconsidered'
  | 'HypothesisRaised'
  | 'HypothesisSupported'
  | 'HypothesisChallenged'
  | 'HypothesisInvalidated'
  | 'HypothesisRetired'
  | 'AlternativeIntroduced'
  | 'AlternativeCompared'
  | 'AlternativeRejected'
  | 'AlternativeRevisited'
  | 'AlternativeSelected'
  | 'ContradictionIdentified'
  | 'ContradictionInvestigated'
  | 'ContradictionResolved'
  | 'ContradictionAccepted'
  | 'ContradictionRecurred'
  | 'DirectionObserved'
  | 'DirectionReinforced'
  | 'DirectionShifted'
  | 'DirectionAbandoned'
  | 'AssumptionIntroduced'
  | 'AssumptionChallenged'
  | 'AssumptionInvalidated'
  | 'AssumptionReplaced'
  | 'ConstraintIntroduced'
  | 'ConstraintModified'
  | 'ConstraintRetired'
  | 'EvidenceAdded'

export type ReasoningCaptureMode = 'Manual' | 'Assisted' | 'Inferred'

export type ReasoningNarrative = {
  summary: string
  details: string
}

export type ReasoningReference = {
  kind: ReasoningReferenceKind
  id: string
  relativePath: string | null
  section: string | null
  excerpt: string | null
  fingerprint?: string | null
}

export type ReasoningProvenance = {
  sourceKind: string
  capturedBy: string
  relativePath: string | null
  section: string | null
  excerpt: string | null
  fingerprint: string | null
}

export type ReasoningCaptureProvenance = {
  mode: ReasoningCaptureMode
  sourceKind: string
  capturedBy: string
  captureReason: string
  sourceTransition: string | null
  sourceArtifact: string | null
  sourceTimestamp: string | null
  skipReason: string | null
  duplicateSignal: string | null
  existingEventReference: ReasoningReference | null
}

export type ReasoningDiagnosticGroup = {
  category: string
  title: string | null
  diagnostics: string[]
}

export type ReasoningEvent = {
  id: string
  repositoryId: string
  createdAt: string
  family: ReasoningEventFamily
  type: ReasoningEventType
  title: string
  narrative: ReasoningNarrative
  references: ReasoningReference[]
  provenance: ReasoningProvenance
  threadIds: string[]
  tags: string[]
  captureProvenance: ReasoningCaptureProvenance
}

export type ReasoningThread = {
  id: string
  repositoryId: string
  title: string
  theme: ReasoningThreadTheme
  createdAt: string
  updatedAt: string
  summary: string
  eventIds: string[]
  tags: string[]
}

export type ReasoningRelationship = {
  id: string
  repositoryId: string
  createdAt: string
  type: ReasoningRelationshipType
  source: ReasoningReference
  target: ReasoningReference
  narrative: ReasoningNarrative
  provenance: ReasoningProvenance
}

export type ReasoningTraceDirection = 'Backward' | 'Forward'

export type ReasoningQueryCategory =
  | 'Decision'
  | 'Hypothesis'
  | 'Alternative'
  | 'Contradiction'
  | 'Direction'
  | 'Thread'
  | 'Assumption'

export type ReasoningMaterializationConcept =
  | 'Hypothesis'
  | 'Alternative'
  | 'Contradiction'
  | 'Direction'
  | 'Thread'

export type ReasoningMaterializationOutcome =
  | 'RemainDerived'
  | 'AddDerivedCache'
  | 'AddReadModelReport'
  | 'PromoteToFirstClassEntity'
  | 'RejectConcept'

export type ReasoningGraphNode = {
  id: string
  kind: ReasoningReferenceKind
  referenceId: string
  label: string
  resolved: boolean
  reference: ReasoningReference | null
}

export type ReasoningGraphRelationship = {
  id: string
  type: ReasoningRelationshipType
  sourceNodeId: string
  targetNodeId: string
  label: string
  provenance: string
  relationshipId: string | null
}

export type ReasoningGraph = {
  repositoryId: string
  generatedAt: string
  nodes: ReasoningGraphNode[]
  relationships: ReasoningGraphRelationship[]
  diagnostics: string[]
  diagnosticGroups?: ReasoningDiagnosticGroup[] | null
}

export type ReasoningTrace = {
  repositoryId: string
  direction: ReasoningTraceDirection
  target: ReasoningReference
  nodes: ReasoningGraphNode[]
  relationships: ReasoningGraphRelationship[]
  diagnostics: string[]
  diagnosticGroups?: ReasoningDiagnosticGroup[] | null
}

export type ReasoningQuery = {
  category: ReasoningQueryCategory
  question: string
  target: ReasoningReference
  direction: ReasoningTraceDirection
  historicalAt?: string | null
}

export type ReasoningReconstructionEvidence = {
  kind: string
  id: string
  title: string
  summary: string
  reference: ReasoningReference | null
  provenance: ReasoningProvenance | null
}

export type ReasoningReconstructionConfidence = {
  level: string
  rationale: string
  eventEvidencePresent: boolean
  relationshipEvidencePresent: boolean
  traceDiagnosticsPresent: boolean
  missingEvidence: string[]
  whyNotHigher: string[]
}

export type ReasoningReconstructionScope = {
  direction: ReasoningTraceDirection
  target: ReasoningReference
  source: ReasoningReference | null
  historicalCutoff: string | null
  reachableEvidence: ReasoningReconstructionEvidence[]
  unreachableEvidence: ReasoningReconstructionEvidence[]
}

export type ReasoningReconstruction = {
  repositoryId: string
  generatedAt: string
  query: ReasoningQuery
  narrative: ReasoningNarrative
  confidence: string
  confidenceRationale: ReasoningReconstructionConfidence
  scope: ReasoningReconstructionScope
  trace: ReasoningTrace
  evidence: ReasoningReconstructionEvidence[]
  diagnostics: string[]
  diagnosticGroups?: ReasoningDiagnosticGroup[] | null
}

export type ReasoningReconstructionReport = {
  id: string
  repositoryId: string
  generatedAt: string
  reconstruction: ReasoningReconstruction
  diagnostics: string[]
}

export type ReasoningQueryResult = {
  repositoryId: string
  generatedAt: string
  query: ReasoningQuery
  reconstruction: ReasoningReconstruction
  diagnostics: string[]
  diagnosticGroups?: ReasoningDiagnosticGroup[] | null
}

export type ReasoningMaterializationScenario = {
  concept: ReasoningMaterializationConcept
  question: string
  reconstructionFailed: boolean
  evidence: string
  repeatedWorkflowCount?: number
}

export type ReasoningMaterializationReviewRequest = {
  scenarios?: ReasoningMaterializationScenario[]
}

export type ReasoningConceptMaterializationReview = {
  concept: ReasoningMaterializationConcept
  recommendation: ReasoningMaterializationOutcome
  summary: string
  failedScenarioCount: number
  repeatedWorkflowCount: number
  failedScenarioThreshold: number
  repeatedWorkflowThreshold: number
  branchReason: string
  elevatedRiskSignals: string[]
  evidence: string[]
  risks: string[]
}

export type ReasoningTaxonomyMaterializationFinding = {
  family: ReasoningEventFamily
  eventTypeCount: number
  eventTypeThreshold: number
  lifecycleRisk: boolean
  terminalEventTypePresent: boolean
  terminalEventTypes: ReasoningEventType[]
  riskReason: string
  summary: string
  evidence: string[]
}

export type ReasoningMaterializationReviewReport = {
  repositoryId: string
  generatedAt: string
  concepts: ReasoningConceptMaterializationReview[]
  taxonomyFindings: ReasoningTaxonomyMaterializationFinding[]
  diagnostics: string[]
  diagnosticGroups?: ReasoningDiagnosticGroup[] | null
}

export type ReasoningCertificationResultKind = 'Passed' | 'Failed'

export type ReasoningCertificationResult = {
  kind: ReasoningCertificationResultKind
  summary: string
}

export type ReasoningCertificationEvidence = {
  id: string
  scenario: string
  passed: boolean
  summary: string
  details: string[]
  references: ReasoningReference[]
}

export type ReasoningCertificationReport = {
  id: string
  repositoryId: string
  generatedAt: string
  result: ReasoningCertificationResult
  evidence: ReasoningCertificationEvidence[]
  diagnostics: string[]
}

export type ManualReasoningCaptureTemplate = {
  kind: ReasoningManualCaptureKind
  family: ReasoningEventFamily
  type: ReasoningEventType
  suggestedThreadTheme: ReasoningThreadTheme
  provenanceSourceKind: string
  suggestedReferenceKinds: ReasoningReferenceKind[]
}

export type ManualReasoningCaptureCommand = {
  kind: ReasoningManualCaptureKind
  title: string
  narrative: ReasoningNarrative
  references?: ReasoningReference[]
  provenance: ReasoningProvenance
  threadIds?: string[]
  tags?: string[]
}

export type CreateReasoningEventCommand = {
  family: ReasoningEventFamily
  type: ReasoningEventType
  title: string
  narrative: ReasoningNarrative
  references?: ReasoningReference[]
  provenance: ReasoningProvenance
  threadIds?: string[]
  tags?: string[]
}

export type CreateReasoningThreadCommand = {
  title: string
  theme: ReasoningThreadTheme
  summary: string
  eventIds?: string[]
  tags?: string[]
}

export type CreateReasoningRelationshipCommand = {
  type: ReasoningRelationshipType
  source: ReasoningReference
  target: ReasoningReference
  narrative: ReasoningNarrative
  provenance: ReasoningProvenance
}
