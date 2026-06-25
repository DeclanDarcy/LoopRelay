import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const srcRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..')

const auditedFiles = [
  'features/decisions/DecisionRecommendationExplanation.tsx',
  'features/decisions/DecisionOptionEvaluationTable.tsx',
  'features/decisions/DecisionRejectedOptionList.tsx',
  'features/decisions/DecisionQualityPanel.tsx',
  'features/decisions/DecisionQualityExplanation.tsx',
  'features/decisions/DecisionBurdenExplanation.tsx',
  'features/decisions/DecisionGovernanceExplanation.tsx',
  'features/decisions/DecisionInfluenceExplorer.tsx',
  'features/decisions/DecisionProposalViewer.tsx',
  'features/execution/ExecutionDecisionInfluencePanel.tsx',
  'hooks/useDecisionGovernance.ts',
  'hooks/useDecisionLifecycleEligibility.ts',
  'hooks/useDecisionQuality.ts',
]

const allowedPresentationHelpers = new Set([
  'collectPrioritizedSignals',
  'formatDate',
  'formatSignedNumber',
  'formatThreshold',
  'groupFindings',
])

function readAuditedSources() {
  return auditedFiles.map((relativePath) => ({
    relativePath,
    source: readFileSync(resolve(srcRoot, relativePath), 'utf8'),
  }))
}

function semanticHelperNames(source: string) {
  return Array.from(source.matchAll(/\bfunction\s+([A-Za-z0-9_]+)\s*\(/g))
    .map((match) => match[1])
    .filter((name) => !allowedPresentationHelpers.has(name))
    .filter((name) =>
      /(?:calculate|compute|derive|score|rank|assess|evaluate|classify|select|choose|weight).*(?:Quality|Burden|Governance|Influence|Recommendation|Eligibility|Score|Rank|Rating|Finding|Action)/i.test(
        name,
      ),
    )
}

describe('decision transparency authority boundary', () => {
  it('does not introduce frontend semantic calculation helpers for decision transparency', () => {
    const offenders = readAuditedSources().flatMap(({ relativePath, source }) =>
      semanticHelperNames(source).map((name) => `${relativePath}: ${name}`),
    )

    expect(offenders).toEqual([])
  })

  it('does not perform weighted scoring or ranking math in the decision transparency UI', () => {
    const weightedMathPattern =
      /\b(?:score|rank|rating|burden|quality|governance|influence|eligibility|recommendation)[A-Za-z0-9_.]*\s*(?:[+\-*/]=|[+\-*/]\s*\d|\?\?=\s*\d)/i
    const offenders = readAuditedSources().flatMap(({ relativePath, source }) =>
      source
        .split(/\r?\n/)
        .map((line, index) => ({ line: line.trim(), lineNumber: index + 1 }))
        .filter(({ line }) => weightedMathPattern.test(line))
        .map(({ line, lineNumber }) => `${relativePath}:${lineNumber}: ${line}`),
    )

    expect(offenders).toEqual([])
  })
})
