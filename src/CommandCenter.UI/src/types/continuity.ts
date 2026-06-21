export type ContinuityTrend = {
  addedCount: number
  removedCount: number
  resolvedCount: number
  lostCount: number
}

export type CompressionTrend = {
  proposalCount: number
  compressedItemCount: number
  removedItemCount: number
  resolvedQuestionCount: number
  retiredRiskCount: number
  warningCount: number
  warnings: string[]
  noiseRemovedIndicators: string[]
}

export type ContinuityDiagnostics = {
  repositoryId: string
  generatedAt: string
  revisionCount: number
  currentContextByteCount: number
  currentContextCharacterCount: number
  contextByteGrowth: number
  averageBytesPerRevision: number
  architectureTrend: ContinuityTrend
  constraintTrend: ContinuityTrend
  decisionTrend: ContinuityTrend
  rationaleTrend: ContinuityTrend
  openQuestionTrend: ContinuityTrend
  activeRiskTrend: ContinuityTrend
  compressionTrend: CompressionTrend
  repeatedInvestigationIndicators: string[]
  repeatedQuestionIndicators: string[]
  decisionReworkIndicators: string[]
  continuityWarnings: string[]
}

export type ContinuityReport = {
  reportId: string
  repositoryId: string
  generatedAt: string
  relativePath: string
  diagnostics: ContinuityDiagnostics
}
