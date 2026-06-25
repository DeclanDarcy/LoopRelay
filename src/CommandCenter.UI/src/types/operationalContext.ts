export type OperationalContextProposalStatus =
  | 'Pending'
  | 'Edited'
  | 'Superseded'
  | 'Accepted'
  | 'Rejected'
  | 'Promoted'

export type OperationalContextReviewState = 'PendingReview' | 'Edited' | 'Accepted' | 'Rejected' | 'Stale'

export type OperationalContextProposalSummary = {
  pendingProposalExists: boolean
  latestProposalId: string | null
  generatedAt: string | null
  status: OperationalContextProposalStatus | null
  sourceInputCount: number
  contentByteCount: number
  contentCharacterCount: number
  lastPromotedAt: string | null
  lastArchivedRelativePath: string | null
}

export type OperationalContextItem = {
  id: string
  kind: string
  text: string
  rationale: string | null
  sourceRelativePath: string | null
}

export type OperationalContextProjection = {
  exists: boolean
  currentRelativePath: string | null
  revisionCount: number
  currentRevisionNumber: number
  lastUpdatedAt: string | null
  lastPromotionAt: string | null
  currentUnderstandingSummary: string[]
  architecture: OperationalContextItem[]
  authorityBoundaries: OperationalContextItem[]
  constraints: OperationalContextItem[]
  stableDecisions: OperationalContextItem[]
  decisionRationale: OperationalContextItem[]
  openQuestions: OperationalContextItem[]
  activeRisks: OperationalContextItem[]
  recentUnderstandingChanges: OperationalContextItem[]
  pendingProposalSummary: OperationalContextProposalSummary
  latestReviewState: OperationalContextReviewState | null
  continuityWarnings: string[]
}

export type OperationalContextSemanticChange = {
  type: string
  section: string
  description: string
  itemId: string | null
  previousState: string | null
  currentState: string | null
  modificationReason: string | null
  identityBasis: string | null
  supportingEvidence: string[]
}

export type OperationalContextCompressionSummary = {
  preservedItemCount: number
  addedItemCount: number
  modifiedItemCount: number
  removedItemCount: number
  compressedItemCount: number
  permanentUnderstandingItemCount: number
  activeUnderstandingItemCount: number
  historicalUnderstandingItemCount: number
  historicalNoiseItemCount: number
  resolvedQuestionCount: number
  retiredRiskCount: number
  warningCount: number
  warnings: string[]
  revisionSummary: string[]
  noiseRemovedIndicators: string[]
  stableUnderstandingRetentionWarnings: string[]
  itemOutcomes: OperationalContextCompressionOutcome[]
}

export type OperationalContextCompressionOutcome = {
  outcome: string
  itemKind: string
  itemText: string
  rule: string
  threshold: string
  rationale: string
  evidence: string[]
}

export type DecisionTaxonomyBasis = {
  taxonomy: string
  matchedRules: string[]
  matchedEvidence: string[]
  isHeuristicFallback: boolean
  fallbackReason: string | null
  diagnostics: string[]
}

export type DecisionAssimilationRecord = {
  decisionId: string
  sourceRelativePath: string
  statement: string
  taxonomy: string
  taxonomyBasis: DecisionTaxonomyBasis
  status: string
  isDurable: boolean
  qualifiesForAssimilation: boolean
  isAssimilated: boolean
  isOmittedByLimit: boolean
  exclusionReason: string | null
  omissionReason: string | null
  operationalStatement: string | null
  rationale: string | null
  constraintsIntroduced: string[]
  consequencesIntroduced: string[]
  openQuestions: string[]
  sourceEvidence: string[]
}

export type DecisionAssimilationLimit = {
  limit: number
  reason: string
  totalAnalyzedItemCount: number
  totalQualifyingItemCount: number
  assimilatedItemCount: number
  omittedItemCount: number
}

export type ContinuityDecisionReference = {
  decisionId: string
  sourceRelativePath: string
  statement: string
  taxonomy: string
}

export type ContinuityDecisionConsequence = {
  consequenceId: string
  originatingDecision: ContinuityDecisionReference
  operationalStatement: string
  affectedArea: string
  supportingEvidence: string[]
  operationalImpact: string
}

export type ContinuityDecisionContradiction = {
  contradictionId: string
  firstDecision: ContinuityDecisionReference
  secondDecision: ContinuityDecisionReference
  conflictType: string
  severity: string
  evidence: string[]
  resolutionGuidance: string
}

export type DecisionAssimilationProjection = {
  decisions: DecisionAssimilationRecord[]
  consequences: ContinuityDecisionConsequence[]
  contradictions: ContinuityDecisionContradiction[]
  limit: DecisionAssimilationLimit
}

export type OperationalContextProposal = {
  proposalId: string
  repositoryId: string
  generatedAt: string
  status: OperationalContextProposalStatus
  generatedContentRelativePath: string
  generatedContentHash: string
  editedContentRelativePath: string | null
  semanticChanges: OperationalContextSemanticChange[]
  decisionAssimilation: DecisionAssimilationProjection
  compressionSummary: OperationalContextCompressionSummary
  review: {
    proposalId: string
    reviewState: OperationalContextReviewState
    baselineCurrentContextHash: string
    reviewedContentHash: string | null
    reviewedAt: string | null
    reviewNote: string | null
    staleReason: string | null
  }
  promotion: {
    proposalId: string
    promotedAt: string | null
    promotedContentHash: string | null
    promotedContentSourceRelativePath: string | null
    revisionNumber: number | null
    archivedRelativePath: string | null
    archiveFailureReason: string | null
    writeFailureReason: string | null
  }
  generatedContent: string | null
  editedContent: string | null
}
