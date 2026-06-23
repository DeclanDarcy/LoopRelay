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

export type DecisionQualityRating = 'Unknown' | 'Poor' | 'Mixed' | 'Good' | 'Excellent'

export type QualitySignalDirection = 'Neutral' | 'Positive' | 'Negative'

export type QualitySignalSeverity = 'Info' | 'Low' | 'Medium' | 'High' | 'Critical'

export type ExecutionProjectionKind =
  | 'ArchitecturalConstraint'
  | 'ImplementationDirective'
  | 'TechnologyChoice'
  | 'WorkflowPolicy'
  | 'RepositoryConvention'

export type TradeoffImpact = 'Low' | 'Medium' | 'High' | 'Blocking'

export type TradeoffSeverity = 'Info' | 'Low' | 'Medium' | 'High' | 'Blocking'

export type RecommendationMode = 'PreferredOption' | 'PreferredPlusAlternative' | 'NoRecommendation'

export type HumanAuthoringBurden =
  | 'Unknown'
  | 'ReviewOnly'
  | 'MinorEdit'
  | 'MajorRefinement'
  | 'FullRewrite'
  | 'GenerationBypassed'

export type RefinementDirectiveType =
  | 'AddConstraint'
  | 'RemoveConstraint'
  | 'IncreasePriority'
  | 'DecreasePriority'
  | 'ExploreAlternative'
  | 'ReevaluateRisk'
  | 'ReevaluateCost'
  | 'ReevaluateRecommendation'
  | 'ClarifyGoal'

export type RecommendationEvidenceType =
  | 'Benefit'
  | 'Cost'
  | 'Risk'
  | 'Dependency'
  | 'Consequence'
  | 'Constraint'
  | 'PriorDecision'
  | 'RepositoryState'

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

export type DecisionBenefit = {
  statement: string
  impact: TradeoffImpact
  evidence: DecisionEvidence[]
}

export type DecisionCost = {
  statement: string
  impact: TradeoffImpact
  evidence: DecisionEvidence[]
}

export type DecisionRisk = {
  statement: string
  severity: TradeoffSeverity
  isUnknown: boolean
  evidence: DecisionEvidence[]
}

export type DecisionDependency = {
  statement: string
  evidence: DecisionEvidence[]
}

export type DecisionConsequence = {
  statement: string
  impact: TradeoffImpact
  evidence: DecisionEvidence[]
}

export type AnalyzedDecisionOption = {
  optionId: string
  benefits: DecisionBenefit[]
  costs: DecisionCost[]
  risks: DecisionRisk[]
  dependencies: DecisionDependency[]
  consequences: DecisionConsequence[]
  diagnostics: string[]
  evidence: DecisionEvidence[]
}

export type DecisionTradeoffComparison = {
  optionId: string
  relativeStrengths: string[]
  relativeWeaknesses: string[]
  uniqueAdvantages: string[]
  uniqueRisks: string[]
  disqualifyingConstraints: string[]
  evidence: DecisionEvidence[]
}

export type DecisionTradeoffAnalysisDiagnostics = {
  analyzedOptionCount: number
  contextFingerprint: string
  unknowns: string[]
  validationWarnings: string[]
  diagnostics: string[]
}

export type RecommendationEvidence = {
  type: RecommendationEvidenceType
  optionId: string
  summary: string
  evidence: DecisionEvidence[]
}

export type OptionEvaluation = {
  optionId: string
  strengths: string[]
  weaknesses: string[]
  risks: string[]
  constraints: string[]
  summary: string
  score: number
  rank: number
  scoreExplanation: string
  evidence: RecommendationEvidence[]
}

export type DecisionRecommendation = {
  optionId: string
  rationale: string
  evidence: DecisionEvidence[]
  summary?: string
  supportingFactors?: string[]
  concerns?: string[]
  assumptions?: string[]
  alternativeExplanations?: string[]
  mode?: RecommendationMode
  recommendationEvidence?: RecommendationEvidence[]
  optionEvaluations?: OptionEvaluation[]
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

export type DecisionRefinementAnalysisRequest = {
  guidance: string
  requestedBy?: string | null
  baseProposalFingerprint?: string | null
}

export type RefinementDirective = {
  id: string
  type: RefinementDirectiveType
  summary: string
  targetOptionId: string | null
  targetField: string | null
  instruction: string | null
  sources: DecisionSourceReference[] | null
}

export type RefinementPlan = {
  repositoryId: string
  proposalId: string
  analyzedAt: string
  baseProposalFingerprint: string
  directives: RefinementDirective[]
  regenerateOptions: boolean
  reevaluateTradeoffs: boolean
  reevaluateRecommendation: boolean
  fullRegeneration: boolean
  appliedConstraints: string[]
  diagnostics: string[]
}

export type DecisionPackageRegenerationRequest = {
  plan: RefinementPlan
  basePackageId: string
  basePackageFingerprint: string
  requestedBy?: string | null
}

export type DecisionPackageMetadata = {
  contextFingerprint: string
  proposalFingerprint: string
  generatorVersion: string
  schemaVersion: string
  diagnostics: string[]
}

export type DecisionPackage = {
  id: string
  repositoryId: string
  proposalId: string
  candidateId: string
  title: string
  decisionSummary: string
  options: DecisionOption[]
  tradeoffs: DecisionTradeoff[]
  recommendation: DecisionRecommendation | null
  assumptions: DecisionAssumption[]
  openConcerns: string[]
  evidence: DecisionEvidence[]
  metadata: DecisionPackageMetadata
  generatedAt: string
}

export type DecisionPackageVersion = {
  id: string
  repositoryId: string
  proposalId: string
  candidateId: string
  createdAt: string
  packageFingerprint: string
  package: DecisionPackage
}

export type DecisionPackageComparison = {
  proposalId: string
  leftPackageId: string
  rightPackageId: string
  repositoryId: string
  leftPackageFingerprint: string
  rightPackageFingerprint: string
  recommendationChanged: boolean
  optionsChanged: boolean
  evidenceChanged: boolean
  risksChanged: boolean
  contextFingerprintChanged: boolean
  fieldComparisons: DecisionRevisionFieldComparison[]
  addedOptions: DecisionOption[]
  removedOptions: DecisionOption[]
  modifiedOptions: DecisionOption[]
  addedEvidence: string[]
  removedEvidence: string[]
  addedRisks: string[]
  removedRisks: string[]
  diagnostics: string[]
}

export type DecisionRefinementArtifact = {
  id: string
  repositoryId: string
  proposalId: string
  createdAt: string
  request: DecisionPackageRegenerationRequest
  directives: RefinementDirective[]
  plan: RefinementPlan
  basePackageId: string
  basePackageFingerprint: string
  regeneratedPackageId: string
  regeneratedPackageFingerprint: string
  comparison: DecisionPackageComparison
  humanAuthoringBurden: HumanAuthoringBurden
  diagnostics: string[]
}

export type DecisionPackageRegenerationResult = {
  repositoryId: string
  proposalId: string
  plan: RefinementPlan
  basePackageVersion: DecisionPackageVersion
  regeneratedPackageVersion: DecisionPackageVersion
  comparison: DecisionPackageComparison
  humanAuthoringBurden: HumanAuthoringBurden
  diagnostics: string[]
  refinementArtifact: DecisionRefinementArtifact | null
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
  analyzedOptions?: AnalyzedDecisionOption[]
  tradeoffComparisons?: DecisionTradeoffComparison[]
  tradeoffAnalysisDiagnostics?: DecisionTradeoffAnalysisDiagnostics | null
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
  analyzedOptions?: AnalyzedDecisionOption[]
  tradeoffComparisons?: DecisionTradeoffComparison[]
  tradeoffAnalysisDiagnostics?: DecisionTradeoffAnalysisDiagnostics | null
  packageId?: string | null
  packageFingerprint?: string | null
  packageVersionCreatedAt?: string | null
  authorityResolvedAt?: string | null
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
  expectedProposalFingerprint?: string | null
  expectedPackageId?: string | null
  expectedPackageFingerprint?: string | null
  acknowledgeStaleAuthority?: boolean
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
  humanAuthoringBurden: HumanAuthoringBurden
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
  humanAuthoringBurden: HumanAuthoringBurden
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

export type DecisionReviewAuthority = {
  proposalFingerprint: string
  packageId: string | null
  packageFingerprint: string | null
  packageVersionCreatedAt: string | null
  packageSourceProposalFingerprint: string | null
  isPackageCurrentForProposalContent: boolean
}

export type DecisionReviewWorkspace = {
  proposal: DecisionProposal
  review: DecisionReviewStatus
  notes: DecisionReviewNote[]
  revisions: DecisionProposalRevision[]
  diagnostics: DecisionReviewDiagnostics
  authority: DecisionReviewAuthority
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

export type DecisionQualitySignal = {
  id: string
  repositoryId: string
  decisionId: string
  category: string
  direction: QualitySignalDirection
  severity: QualitySignalSeverity
  summary: string
  detail: string
  sources: DecisionSourceReference[]
}

export type HumanAuthoringBurdenSignal = {
  id: string
  repositoryId: string
  decisionId: string
  burden: HumanAuthoringBurden
  sourceKind: string
  summary: string
  sources: DecisionSourceReference[]
}

export type DecisionQualityAssessment = {
  id: string
  repositoryId: string
  decisionId: string
  assessedAt: string
  rating: DecisionQualityRating
  score: number
  signals: DecisionQualitySignal[]
  humanAuthoringBurdenSignals: HumanAuthoringBurdenSignal[]
  diagnostics: string[]
}

export type DecisionQualityReport = {
  id: string
  repositoryId: string
  generatedAt: string
  decisionCount: number
  generatedPackageCount: number
  acceptedCount: number
  acceptedRate: number
  modifiedCount: number
  modifiedRate: number
  rejectedCount: number
  rejectedRate: number
  supersededCount: number
  supersededRate: number
  recommendationDivergenceCount: number
  recommendationDivergenceRate: number
  alternativeUtilizationCount: number
  alternativeUtilizationRate: number
  reviewOnlyCount: number
  reviewOnlyRate: number
  minorEditCount: number
  minorEditRate: number
  majorRefinementCount: number
  majorRefinementRate: number
  fullRewriteCount: number
  fullRewriteRate: number
  generationBypassedCount: number
  generationBypassedRate: number
  rating: DecisionQualityRating
  assessments: DecisionQualityAssessment[]
  diagnostics: string[]
}

export type DecisionQualityTrend = {
  id: string
  repositoryId: string
  generatedAt: string
  assessmentCount: number
  currentRating: DecisionQualityRating
  previousRating: DecisionQualityRating
  currentAverageScore: number
  previousAverageScore: number
  direction: QualitySignalDirection
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

export type ExecutionDecisionPriority = {
  id: string
  decisionId: string
  title: string
  statement: string
  classification: DecisionClassification
  projectionKind: ExecutionProjectionKind
  rank: number
  sources: DecisionSourceReference[]
}

export type ExecutionArchitectureRule = {
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

export type ExecutionDecisionContext = {
  constraints: ExecutionConstraint[]
  directives: ExecutionDirective[]
  priorities: ExecutionDecisionPriority[]
  architectureRules: ExecutionArchitectureRule[]
  conflicts: ExecutionDecisionConflict[]
  diagnostics: string[]
}

export type ExecutionDecisionProjection = {
  repositoryId: string
  generatedAt: string
  constraints: ExecutionConstraint[]
  directives: ExecutionDirective[]
  priorities: ExecutionDecisionPriority[]
  architectureRules: ExecutionArchitectureRule[]
  conflicts: ExecutionDecisionConflict[]
  diagnostics: string[]
  context: ExecutionDecisionContext
}

export type DecisionAdherenceObservation = {
  observedAt: string
  observer: string
  observation: string
}

export type DecisionInfluenceStatement = {
  statementId: string
  decisionId: string
  title: string
  statement: string
  classification: DecisionClassification
  projectionKind: ExecutionProjectionKind
  statementType: string
  promptSection: string
  priorityRank: number | null
  sources: DecisionSourceReference[]
  adherenceObservations: DecisionAdherenceObservation[]
}

export type DecisionInfluenceTrace = {
  id: string
  repositoryId: string
  executionSessionId: string
  recordedAt: string
  projectionGeneratedAt: string
  projectionFingerprint: string
  statements: DecisionInfluenceStatement[]
  diagnostics: string[]
}
