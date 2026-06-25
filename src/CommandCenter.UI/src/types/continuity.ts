import type { OperationalContextSemanticChange } from './operationalContext'

export type ContinuityTrend = {
  addedCount: number
  modifiedCount: number
  removedCount: number
  resolvedCount: number
  lostCount: number
}

export type ContinuityDiagnosticGroup = {
  category: string
  title: string
  diagnostics: string[]
}

export type OperationalEvolutionSummary = {
  addedCount: number
  modifiedCount: number
  removedCount: number
  preservedCount: number
  lostCount: number
  resolvedCount: number
  semanticChanges: OperationalContextSemanticChange[]
  diagnosticGroups: ContinuityDiagnosticGroup[]
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
  operationalEvolution: OperationalEvolutionSummary
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
  diagnosticGroups: ContinuityDiagnosticGroup[]
}

export type ContinuityReport = {
  reportId: string
  repositoryId: string
  generatedAt: string
  relativePath: string
  diagnostics: ContinuityDiagnostics
}
