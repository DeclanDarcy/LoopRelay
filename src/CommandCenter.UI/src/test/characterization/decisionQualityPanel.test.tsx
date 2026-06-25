import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DecisionQualityPanel } from '../../features/decisions/DecisionQualityPanel'
import type { DecisionQualityAssessment, DecisionQualityReport, DecisionQualityTrend } from '../../types'

afterEach(() => {
  cleanup()
})

describe('DecisionQualityPanel', () => {
  it('prioritizes burden and signal categories over the overall score', () => {
    render(
      <DecisionQualityPanel
        assessments={[createAssessment()]}
        currentReport={createReport()}
        reports={[createReport('quality.202606231200000000001')]}
        currentTrend={createTrend()}
        trends={[createTrend('trend.202606231200000000001')]}
        selectedProposalId="PROP-0001"
        isLoading={false}
        isAssessing={false}
        isGeneratingReport={false}
        isGeneratingTrend={false}
        error={null}
        onAssessProposal={vi.fn()}
        onGenerateReport={vi.fn()}
        onGenerateTrend={vi.fn()}
      />,
    )

    expect(screen.getByText('Review only: 1')).toBeInTheDocument()
    expect(screen.getAllByText('RecommendationStability / Positive / Info').length).toBeGreaterThan(0)
    expect(screen.getByText('TradeoffQuality / Positive / Info')).toBeInTheDocument()
    expect(screen.getAllByText('ConstraintQuality / Neutral / Info').length).toBeGreaterThan(0)
    expect(screen.getByText('Base score: 50')).toBeInTheDocument()
    expect(screen.getByText('Raw score: 82')).toBeInTheDocument()
    expect(screen.getByText('Threshold: Good (65-84)')).toBeInTheDocument()
    expect(screen.getByText('+12 score | QS-RecommendationStability')).toBeInTheDocument()
    expect(screen.getByText('Effective burden: ReviewOnly')).toBeInTheDocument()
    expect(screen.getByText('Select the highest-weight human-authoring burden signal.')).toBeInTheDocument()
    expect(screen.getByText('Winning signal: HAB-0001')).toBeInTheDocument()
    expect(screen.getAllByText('.agents/decisions/records/DEC-0001/decision.json').length).toBeGreaterThan(0)
    expect(screen.getByText('quality.202606231200000000001')).toBeInTheDocument()
    expect(screen.getByText('trend.202606231200000000001')).toBeInTheDocument()
  })

  it('keeps assessment, report, and trend persistence behind explicit actions', () => {
    const onAssessProposal = vi.fn()
    const onGenerateReport = vi.fn()
    const onGenerateTrend = vi.fn()

    render(
      <DecisionQualityPanel
        assessments={[createAssessment()]}
        currentReport={createReport()}
        reports={[]}
        currentTrend={createTrend()}
        trends={[]}
        selectedProposalId="PROP-0001"
        isLoading={false}
        isAssessing={false}
        isGeneratingReport={false}
        isGeneratingTrend={false}
        error={null}
        onAssessProposal={onAssessProposal}
        onGenerateReport={onGenerateReport}
        onGenerateTrend={onGenerateTrend}
      />,
    )

    const quality = screen.getByLabelText('Decision quality')
    fireEvent.click(within(quality).getByRole('button', { name: 'Assess Proposal' }))
    fireEvent.click(within(quality).getByRole('button', { name: 'Save Report' }))
    fireEvent.click(within(quality).getByRole('button', { name: 'Save Trend' }))

    expect(onAssessProposal).toHaveBeenCalledTimes(1)
    expect(onGenerateReport).toHaveBeenCalledTimes(1)
    expect(onGenerateTrend).toHaveBeenCalledTimes(1)
  })
})

function createAssessment(): DecisionQualityAssessment {
  return {
    id: 'assessment.202606231200000000000',
    repositoryId: 'repo-alpha',
    decisionId: 'DEC-0001',
    assessedAt: '2026-06-23T19:00:00.000Z',
    rating: 'Good',
    score: 82,
    diagnostics: ['Assessment is advisory.'],
    humanAuthoringBurdenSignals: [
      {
        id: 'HAB-0001',
        repositoryId: 'repo-alpha',
        decisionId: 'DEC-0001',
        burden: 'ReviewOnly',
        sourceKind: 'ResolutionSnapshot',
        summary: 'Human reviewed generated content only.',
        sources: [source()],
      },
    ],
    qualityExplanation: {
      baseScore: 50,
      rawScore: 82,
      clampedScore: 82,
      threshold: {
        rating: 'Good',
        minimumScore: 65,
        maximumScore: 84,
        reason: 'Good threshold crossed by backend score contribution.',
      },
      overrideReason: null,
      signalContributions: [
        {
          signalId: 'QS-RecommendationStability',
          category: 'RecommendationStability',
          direction: 'Positive',
          severity: 'Info',
          scoreContribution: 12,
          summary: 'Recommendation remained stable.',
        },
        {
          signalId: 'QS-ConstraintQuality',
          category: 'ConstraintQuality',
          direction: 'Neutral',
          severity: 'Info',
          scoreContribution: 0,
          summary: 'Constraint evidence is present.',
        },
      ],
      diagnostics: ['Quality explanation is backend-owned.'],
    },
    humanAuthoringBurdenExplanation: {
      decisionId: 'DEC-0001',
      selectionRule: 'Select the highest-weight human-authoring burden signal.',
      effectiveBurden: 'ReviewOnly',
      winningSignal: {
        id: 'HAB-0001',
        repositoryId: 'repo-alpha',
        decisionId: 'DEC-0001',
        burden: 'ReviewOnly',
        sourceKind: 'ResolutionSnapshot',
        summary: 'Human reviewed generated content only.',
        sources: [source()],
      },
      isUnknown: false,
      isInferred: false,
      diagnostics: ['Signal HAB-0001 selected effective burden ReviewOnly.'],
    },
    signals: [
      signal('HumanAuthoringBurden', 'Positive', 'Info'),
      signal('RecommendationStability', 'Positive', 'Info'),
      signal('TradeoffQuality', 'Positive', 'Info'),
      signal('ContextQuality', 'Positive', 'Info'),
      signal('ConstraintQuality', 'Neutral', 'Info'),
    ],
  }
}

function createReport(id = 'quality.current'): DecisionQualityReport {
  const assessment = createAssessment()
  return {
    id,
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-23T19:00:00.000Z',
    decisionCount: 1,
    generatedPackageCount: 1,
    acceptedCount: 1,
    acceptedRate: 1,
    modifiedCount: 0,
    modifiedRate: 0,
    rejectedCount: 0,
    rejectedRate: 0,
    supersededCount: 0,
    supersededRate: 0,
    recommendationDivergenceCount: 0,
    recommendationDivergenceRate: 0,
    alternativeUtilizationCount: 0,
    alternativeUtilizationRate: 0,
    reviewOnlyCount: 1,
    reviewOnlyRate: 1,
    minorEditCount: 0,
    minorEditRate: 0,
    majorRefinementCount: 0,
    majorRefinementRate: 0,
    fullRewriteCount: 0,
    fullRewriteRate: 0,
    generationBypassedCount: 0,
    generationBypassedRate: 0,
    rating: 'Good',
    assessments: [assessment],
    diagnostics: ['Report is advisory.'],
    humanAuthoringBurdenExplanations: [assessment.humanAuthoringBurdenExplanation!],
  }
}

function createTrend(id = 'trend.current'): DecisionQualityTrend {
  return {
    id,
    repositoryId: 'repo-alpha',
    generatedAt: '2026-06-23T19:00:00.000Z',
    assessmentCount: 1,
    currentRating: 'Good',
    previousRating: 'Unknown',
    currentAverageScore: 82,
    previousAverageScore: 0,
    direction: 'Positive',
    diagnostics: ['Trend is advisory.'],
  }
}

function signal(
  category: DecisionQualityAssessment['signals'][number]['category'],
  direction: DecisionQualityAssessment['signals'][number]['direction'],
  severity: DecisionQualityAssessment['signals'][number]['severity'],
) {
  return {
    id: `QS-${category}`,
    repositoryId: 'repo-alpha',
    decisionId: 'DEC-0001',
    category,
    direction,
    severity,
    summary: `${category} summary`,
    detail: `${category} detail`,
    sources: [source()],
  }
}

function source() {
  return {
    sourceKind: 'DecisionRecord',
    relativePath: '.agents/decisions/records/DEC-0001/decision.json',
    section: null,
    itemId: null,
    decisionId: 'DEC-0001',
    proposalId: 'PROP-0001',
    candidateId: 'CAND-0001',
    excerpt: 'Human resolution remains the assessment boundary.',
  }
}
