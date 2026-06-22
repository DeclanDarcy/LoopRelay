export type DecisionState = 'Open' | 'UnderReview' | 'Resolved' | 'Superseded' | 'Archived'

export type DecisionClassification = 'Architectural' | 'Strategic' | 'Tactical' | 'Operational'

export type DecisionCandidateState =
  | 'Discovered'
  | 'Promoted'
  | 'Dismissed'
  | 'Expired'
  | 'Duplicate'

export type DecisionCandidatePriority = 'Low' | 'Medium' | 'High' | 'Blocking'

export type DecisionProposalState =
  | 'Draft'
  | 'Generated'
  | 'Viewed'
  | 'NeedsRefinement'
  | 'ReadyForResolution'
  | 'Refined'
  | 'Resolved'
  | 'Expired'
  | 'Discarded'

export type DecisionReviewState =
  | 'NotStarted'
  | 'Viewed'
  | 'NeedsRefinement'
  | 'ReadyForResolution'
  | 'Closed'

export type DecisionOutcome = 'Accepted' | 'Rejected' | 'Deferred'

export type DecisionGovernanceCategory =
  | 'Consistency'
  | 'SupersessionLineage'
  | 'DependencyIntegrity'
  | 'AuthorityMetadata'
  | 'DecisionCoverage'
  | 'ProposalQuality'
  | 'ExecutionProjectionReadiness'
  | 'AuthorityBoundary'
  | 'FingerprintIntegrity'
  | 'ProjectionIntegrity'

export type DecisionGovernanceSeverity = 'Info' | 'Warning' | 'Blocking'

export type DecisionHealthAssessment = 'Healthy' | 'AdvisoryFindings' | 'Blocked'

export type DecisionLifecycleCertificationResultKind = 'Passed' | 'Failed'

export type ExecutionProjectionKind =
  | 'ArchitecturalConstraint'
  | 'ImplementationDirective'
  | 'TechnologyChoice'
  | 'WorkflowPolicy'
  | 'RepositoryConvention'

export type DecisionSourceReference = {
  sourceKind: string
  relativePath: string | null
  section: string | null
  itemId: string | null
  decisionId: string | null
  proposalId: string | null
  candidateId: string | null
  excerpt: string | null
}

export type DecisionEvidence = {
  summary: string
  sources: DecisionSourceReference[]
}

export type DecisionSignal = {
  kind: string
  summary: string
  classification: DecisionClassification
  priority: DecisionCandidatePriority
  evidence: DecisionEvidence[]
}

export type DecisionHistoryEntry = {
  at: string
  actor: string
  action: string
  fromState: string | null
  toState: string | null
  reason: string
  sources: DecisionSourceReference[]
}

export type DecisionCandidate = {
  id: string
  repositoryId: string
  state: DecisionCandidateState
  priority: DecisionCandidatePriority
  classification: DecisionClassification
  title: string
  summary: string
  sourceFingerprint: string
  signals: DecisionSignal[]
  evidence: DecisionEvidence[]
  sources: DecisionSourceReference[]
  diagnostics: string[]
  history: DecisionHistoryEntry[]
}

export type DecisionOption = {
  id: string
  title: string
  description: string
  evidence: DecisionEvidence[]
}

export type DecisionTradeoff = {
  optionId: string
  benefit: string
  cost: string
  evidence: DecisionEvidence[]
}

export type DecisionRecommendation = {
  optionId: string
  rationale: string
  evidence: DecisionEvidence[]
}

export type DecisionAssumption = {
  id: string
  statement: string
  evidence: DecisionEvidence[]
}

export type DecisionConstraint = {
  id: string
  summary: string
  evidence: DecisionEvidence[]
}

export type DecisionAssumptionRevision = {
  assumptionId: string
  revisedStatement: string | null
  retire: boolean
  reason: string
}

export type DecisionOptionRevision = {
  optionId: string
  revisedTitle: string | null
  revisedDescription: string | null
  retire: boolean
  reason: string
}

export type DecisionTradeoffRevision = {
  optionId: string
  benefit: string | null
  cost: string | null
  reason: string
}

export type DecisionPriorityAdjustment = {
  itemId: string
  priority: DecisionCandidatePriority
  reason: string
}

export type DecisionRefinementRequest = {
  reason: string
  context?: string | null
  options?: DecisionOption[] | null
  tradeoffs?: DecisionTradeoff[] | null
  recommendation?: DecisionRecommendation | null
  assumptions?: DecisionAssumption[] | null
  requestedBy?: string | null
  baseProposalFingerprint?: string | null
  constraints?: DecisionConstraint[] | null
  assumptionRevisions?: DecisionAssumptionRevision[] | null
  optionRevisions?: DecisionOptionRevision[] | null
  tradeoffRevisions?: DecisionTradeoffRevision[] | null
  priorityAdjustments?: DecisionPriorityAdjustment[] | null
  rejectedChanges?: string[] | null
}

export type DecisionProposal = {
  id: string
  repositoryId: string
  candidateId: string
  state: DecisionProposalState
  title: string
  context: string
  options: DecisionOption[]
  tradeoffs: DecisionTradeoff[]
  recommendation: DecisionRecommendation | null
  assumptions: DecisionAssumption[]
  evidence: DecisionEvidence[]
  history: DecisionHistoryEntry[]
}

export type DecisionMetadata = {
  repositoryId: string
  createdAt: string
  updatedAt: string
  schemaVersion: string
}

export type DecisionResolvedProposalSnapshot = {
  proposalId: string
  candidateId: string
  proposalFingerprint: string
  proposalState: DecisionProposalState
  title: string
  context: string
  options: DecisionOption[]
  tradeoffs: DecisionTradeoff[]
  recommendation: DecisionRecommendation | null
  assumptions: DecisionAssumption[]
  evidence: DecisionEvidence[]
  history: DecisionHistoryEntry[]
  revisions: DecisionProposalRevision[]
}

export type DecisionResolution = {
  outcome: DecisionOutcome
  selectedOptionId: string
  rationale: string
  resolvedBy: string
  recommendationDiverged: boolean
  resolvedAt: string
  sources: DecisionSourceReference[]
  sourceProposalSnapshot: DecisionResolvedProposalSnapshot | null
}

export type DecisionRelationship = {
  type: string
  targetDecisionId: string
  description: string | null
  sources: DecisionSourceReference[]
}

export type Decision = {
  id: string
  state: DecisionState
  classification: DecisionClassification
  title: string
  context: string
  metadata: DecisionMetadata
  resolution: DecisionResolution | null
  relationships: DecisionRelationship[]
  evidence: DecisionEvidence[]
  history: DecisionHistoryEntry[]
}

export type ResolveDecisionCommand = {
  rationale: string
  resolver: string
  selectedOptionId: string | null
  outcome: DecisionOutcome
}

export type CreateDecisionAssimilationRecommendationCommand = {
  requestedBy?: string | null
  notes?: string | null
}

export type DecisionAssimilationRecommendation = {
  decisionId: string
  repositoryId: string
  createdAt: string
  decisionFingerprint: string
  contextSnapshotId: string
  contextFingerprint: string
  sourceDecision: Decision
  contextSnapshot: DecisionContextSnapshot
  projectedStableDecision: string
  rationale: string
  requestedBy: string | null
  notes: string | null
  evidence: DecisionEvidence[]
  sources: DecisionSourceReference[]
  diagnostics: string[]
}

export type DecisionReviewStatus = {
  repositoryId: string
  proposalId: string
  state: DecisionReviewState
  updatedAt: string
  reason: string | null
  sources: DecisionSourceReference[]
}

export type DecisionReviewNote = {
  id: string
  repositoryId: string
  proposalId: string
  createdAt: string
  reviewer: string
  body: string
  sources: DecisionSourceReference[]
}

export type DecisionProposalRevision = {
  id: string
  repositoryId: string
  proposalId: string
  createdAt: string
  reason: string
  changedFields: string[]
  sourceProposalFingerprint: string
  sources: DecisionSourceReference[]
  requestedBy: string | null
  acceptedChanges: string[] | null
  rejectedChanges: string[] | null
  diagnostics: string[] | null
  previousOptions: DecisionOption[] | null
  retiredOptions: DecisionOption[] | null
  previousAssumptions: DecisionAssumption[] | null
  retiredAssumptions: DecisionAssumption[] | null
  previousRecommendationRationale: string | null
  revisedRecommendationRationale: string | null
  previousContext: string | null
  revisedContext: string | null
  revisedOptions: DecisionOption[] | null
  previousTradeoffs: DecisionTradeoff[] | null
  revisedTradeoffs: DecisionTradeoff[] | null
  revisedAssumptions: DecisionAssumption[] | null
}

export type DecisionRevisionFieldComparison = {
  field: string
  changeType: string
  previousValue: string | null
  revisedValue: string | null
}

export type DecisionProposalRevisionComparison = {
  proposalId: string
  revisionId: string
  repositoryId: string
  sourceProposalFingerprint: string
  currentProposalFingerprint: string
  sourceMatchesCurrentProposal: boolean
  changedFields: string[]
  fieldComparisons: DecisionRevisionFieldComparison[]
  acceptedChanges: string[]
  rejectedChanges: string[]
  diagnostics: string[]
  previousOptions: DecisionOption[]
  revisedOptions: DecisionOption[]
  retiredOptions: DecisionOption[]
  previousAssumptions: DecisionAssumption[]
  revisedAssumptions: DecisionAssumption[]
  retiredAssumptions: DecisionAssumption[]
  previousTradeoffs: DecisionTradeoff[]
  revisedTradeoffs: DecisionTradeoff[]
  sources: DecisionSourceReference[]
}

export type DecisionProposalRevisionSnapshot = {
  revision: DecisionProposalRevision
  comparison: DecisionProposalRevisionComparison
  isCurrentProposal: boolean
  authorityBoundary: string
}

export type DecisionProposalLineageEvent = {
  occurredAt: string
  kind: string
  itemId: string | null
  summary: string
  fromState: string | null
  toState: string | null
  sources: DecisionSourceReference[]
}

export type DecisionProposalLineage = {
  repositoryId: string
  proposalId: string
  currentState: DecisionProposalState
  currentProposalFingerprint: string
  currentProposal: DecisionProposal
  review: DecisionReviewStatus
  events: DecisionProposalLineageEvent[]
  revisions: DecisionProposalRevisionSnapshot[]
  reviewNotes: DecisionReviewNote[]
  diagnostics: string[]
}

export type DecisionReviewDiagnostics = {
  hasRecommendation: boolean
  hasEvidence: boolean
  optionCount: number
  tradeoffCount: number
  assumptionCount: number
  noteCount: number
  warnings: string[]
}

export type DecisionReviewWorkspace = {
  proposal: DecisionProposal
  review: DecisionReviewStatus
  notes: DecisionReviewNote[]
  revisions: DecisionProposalRevision[]
  diagnostics: DecisionReviewDiagnostics
}

export type DecisionProposalBrowserItem = {
  proposalId: string
  candidateId: string
  state: DecisionProposalState
  title: string
  classification: DecisionClassification
  priority: DecisionCandidatePriority
  createdAt: string
  updatedAt: string
  reviewState: DecisionReviewState
  reviewUpdatedAt: string
  isResolved: boolean
}

export type DecisionOptionComparisonItem = {
  optionId: string
  title: string
  description: string
  isRecommended: boolean
  benefits: string[]
  costs: string[]
  evidence: DecisionEvidence[]
}

export type DecisionOptionComparison = {
  proposalId: string
  recommendedOptionId: string | null
  options: DecisionOptionComparisonItem[]
}

export type DecisionSourceAttribution = {
  appliesToKind: string
  itemId: string | null
  sourceKind: string
  relativePath: string | null
  section: string | null
  excerpt: string | null
  source: DecisionSourceReference
}

export type DecisionEvidenceInspectionItem = {
  appliesToKind: string
  itemId: string | null
  summary: string
  sources: DecisionSourceAttribution[]
}

export type DecisionEvidenceInspection = {
  proposalId: string
  candidateId: string
  items: DecisionEvidenceInspectionItem[]
  diagnostics: DecisionReviewDiagnostics
}

export type DecisionContextItem = {
  id: string
  kind: string
  title: string
  content: string
  required: boolean
  fingerprint: string
  sources: DecisionSourceReference[]
}

export type DecisionContext = {
  repositoryId: string
  fingerprint: string
  items: DecisionContextItem[]
  diagnostics: DecisionContextDiagnostics
  validation: DecisionContextValidationResult
}

export type DecisionContextSourceStatus = 'Loaded' | 'Missing' | 'Warning'

export type DecisionContextSourceDiagnostic = {
  name: string
  relativePath: string
  required: boolean
  status: DecisionContextSourceStatus
  message: string | null
  byteCount: number
  characterCount: number
  fingerprint: string | null
}

export type DecisionContextDiagnostics = {
  sources: DecisionContextSourceDiagnostic[]
  warnings: string[]
}

export type DecisionContextValidationResult = {
  isValid: boolean
  errors: string[]
  warnings: string[]
}

export type DecisionContextSnapshot = {
  snapshotId: string
  repositoryId: string
  createdAt: string
  fingerprint: string
  context: DecisionContext
  diagnostics: DecisionContextDiagnostics
  validation: DecisionContextValidationResult
}

export type DecisionGovernanceSummary = {
  decisionCount: number
  resolvedDecisionCount: number
  activeCandidateCount: number
  activeProposalCount: number
  assimilationRecommendationCount: number
  findingCount: number
  blockingFindingCount: number
}

export type DecisionGovernanceFinding = {
  id: string
  category: DecisionGovernanceCategory
  severity: DecisionGovernanceSeverity
  blocksExecutionProjection: boolean
  title: string
  detail: string
  sources: DecisionSourceReference[]
  relatedDecisionIds: string[]
  relatedCandidateIds: string[]
  relatedProposalIds: string[]
}

export type DecisionGovernanceReport = {
  id: string
  repositoryId: string
  generatedAt: string
  inputFingerprint: string
  health: DecisionHealthAssessment
  summary: DecisionGovernanceSummary
  findings: DecisionGovernanceFinding[]
  diagnostics: string[]
}

export type DecisionLifecycleCertificationResult = {
  kind: DecisionLifecycleCertificationResultKind
  passedEvidenceCount: number
  failedEvidenceCount: number
}

export type DecisionCertificationEvidence = {
  id: string
  area: string
  passed: boolean
  detail: string
  sources: DecisionSourceReference[]
  relatedDecisionIds: string[]
  relatedCandidateIds: string[]
  relatedProposalIds: string[]
}

export type DecisionCertificationReport = {
  id: string
  repositoryId: string
  generatedAt: string
  inputFingerprint: string
  result: DecisionLifecycleCertificationResult
  health: DecisionHealthAssessment
  evidence: DecisionCertificationEvidence[]
  findings: DecisionGovernanceFinding[]
  diagnostics: string[]
}

export type ExecutionConstraint = {
  id: string
  decisionId: string
  title: string
  statement: string
  classification: DecisionClassification
  projectionKind: ExecutionProjectionKind
  sources: DecisionSourceReference[]
}

export type ExecutionDirective = {
  id: string
  decisionId: string
  title: string
  statement: string
  classification: DecisionClassification
  projectionKind: ExecutionProjectionKind
  sources: DecisionSourceReference[]
}

export type ExecutionDecisionConflict = {
  id: string
  decisionId: string
  title: string
  statement: string
  conflictingExcerpt: string
  sources: DecisionSourceReference[]
}

export type ExecutionDecisionProjection = {
  repositoryId: string
  generatedAt: string
  constraints: ExecutionConstraint[]
  directives: ExecutionDirective[]
  conflicts: ExecutionDecisionConflict[]
  diagnostics: string[]
}
