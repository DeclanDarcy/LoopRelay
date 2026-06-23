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
}

export type ReasoningProvenance = {
  sourceKind: string
  capturedBy: string
  relativePath: string | null
  section: string | null
  excerpt: string | null
  fingerprint: string | null
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
