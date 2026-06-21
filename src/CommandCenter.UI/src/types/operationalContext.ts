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
