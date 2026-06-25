import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { OperationalContextAssimilationLimitPanel } from '../../features/operational-context/OperationalContextAssimilationLimitPanel'
import { OperationalContextAssimilationPanel } from '../../features/operational-context/OperationalContextAssimilationPanel'
import { OperationalContextConsequencePanel } from '../../features/operational-context/OperationalContextConsequencePanel'
import { OperationalContextTaxonomyPanel } from '../../features/operational-context/OperationalContextTaxonomyPanel'
import type { DecisionAssimilationProjection } from '../../types'

afterEach(() => {
  cleanup()
})

function createAssimilationProjection(): DecisionAssimilationProjection {
  return {
    decisions: [
      {
        decisionId: 'DEC-0001',
        sourceRelativePath: '.agents/decisions/decisions.md',
        statement: 'Backend continuity services own operational context promotion because hidden memory is not authoritative.',
        taxonomy: 'ArchitecturalDecision',
        taxonomyBasis: {
          taxonomy: 'ArchitecturalDecision',
          matchedRules: ['architectural-continuity-keywords'],
          matchedEvidence: ['Matched architectural keyword: Backend continuity services own'],
          isHeuristicFallback: false,
          fallbackReason: null,
          diagnostics: ['Ambiguous taxonomy match resolved to ArchitecturalDecision by precedence over TacticalDecision.'],
        },
        status: 'Assimilated',
        isDurable: true,
        qualifiesForAssimilation: true,
        isAssimilated: true,
        isOmittedByLimit: false,
        exclusionReason: null,
        omissionReason: null,
        operationalStatement: 'Decision: Backend continuity services own operational context promotion because hidden memory is not authoritative.',
        rationale: 'hidden memory is not authoritative',
        constraintsIntroduced: ['Promotion must write repository artifacts.'],
        consequencesIntroduced: ['Review surfaces must display backend promotion status.'],
        openQuestions: ['Should stale accepted proposals be regenerated automatically?'],
        sourceEvidence: [
          'Source artifact: .agents/decisions/decisions.md',
          'Decision statement: Backend continuity services own operational context promotion because hidden memory is not authoritative.',
        ],
      },
      {
        decisionId: 'DEC-0002',
        sourceRelativePath: '.agents/decisions/decisions.md',
        statement: 'Run the focused UI test before build.',
        taxonomy: 'TacticalDecision',
        taxonomyBasis: {
          taxonomy: 'TacticalDecision',
          matchedRules: ['tactical-execution-keywords'],
          matchedEvidence: ['Matched tactical keyword: Run the focused UI test'],
          isHeuristicFallback: false,
          fallbackReason: null,
          diagnostics: [],
        },
        status: 'Excluded',
        isDurable: false,
        qualifiesForAssimilation: false,
        isAssimilated: false,
        isOmittedByLimit: false,
        exclusionReason: 'Tactical decision signals are execution detail and are not assimilated as durable operational context.',
        omissionReason: null,
        operationalStatement: null,
        rationale: null,
        constraintsIntroduced: [],
        consequencesIntroduced: [],
        openQuestions: [],
        sourceEvidence: ['Decision statement: Run the focused UI test before build.'],
      },
      {
        decisionId: 'DEC-0003',
        sourceRelativePath: '.agents/decisions/decisions.md',
        statement: 'Unclassified implementation note.',
        taxonomy: 'TacticalDecision',
        taxonomyBasis: {
          taxonomy: 'TacticalDecision',
          matchedRules: [],
          matchedEvidence: [],
          isHeuristicFallback: true,
          fallbackReason: 'No taxonomy rules matched; defaulted to tactical so unclassified text does not become durable operational context.',
          diagnostics: ['No taxonomy rules matched.'],
        },
        status: 'OmittedByLimit',
        isDurable: true,
        qualifiesForAssimilation: true,
        isAssimilated: false,
        isOmittedByLimit: true,
        exclusionReason: null,
        omissionReason: 'Operational context proposal generation includes at most eight qualifying durable decision signals to keep the proposed context reviewable.',
        operationalStatement: 'Decision: Unclassified implementation note.',
        rationale: null,
        constraintsIntroduced: [],
        consequencesIntroduced: [],
        openQuestions: [],
        sourceEvidence: ['Decision statement: Unclassified implementation note.'],
      },
    ],
    consequences: [
      {
        consequenceId: 'consequence-1',
        originatingDecision: {
          decisionId: 'DEC-0001',
          sourceRelativePath: '.agents/decisions/decisions.md',
          statement: 'Backend continuity services own operational context promotion because hidden memory is not authoritative.',
          taxonomy: 'ArchitecturalDecision',
        },
        operationalStatement: 'Review surfaces must display backend promotion status.',
        affectedArea: 'Operational context review',
        supportingEvidence: ['Consequence statement: Review surfaces must display backend promotion status.'],
        operationalImpact: 'Review panels need to link consequence text to the originating decision.',
      },
    ],
    contradictions: [],
    limit: {
      limit: 8,
      reason: 'Operational context proposal generation includes at most eight qualifying durable decision signals to keep the proposed context reviewable.',
      totalAnalyzedItemCount: 11,
      totalQualifyingItemCount: 9,
      assimilatedItemCount: 8,
      omittedItemCount: 1,
    },
  }
}

describe('operational context assimilation panel rendering characterization', () => {
  it('renders backend-authored assimilation statuses and reasons without deriving eligibility', () => {
    render(<OperationalContextAssimilationPanel decisionAssimilation={createAssimilationProjection()} />)

    const panel = screen.getByRole('heading', { name: 'Decision Assimilation' }).closest('div')

    expect(panel).not.toBeNull()
    expect(within(panel as HTMLElement).getByText('Assimilated')).toBeInTheDocument()
    expect(within(panel as HTMLElement).getByText('Excluded')).toBeInTheDocument()
    expect(within(panel as HTMLElement).getByText('OmittedByLimit')).toBeInTheDocument()
    expect(within(panel as HTMLElement).getAllByText('Qualifies')).toHaveLength(3)
    expect(within(panel as HTMLElement).getAllByText('Operational statement')).toHaveLength(3)
    expect(
      within(panel as HTMLElement).getByText(
        'Tactical decision signals are execution detail and are not assimilated as durable operational context.',
      ),
    ).toBeInTheDocument()
    expect(
      within(panel as HTMLElement).getByText(
        'Operational context proposal generation includes at most eight qualifying durable decision signals to keep the proposed context reviewable.',
      ),
    ).toBeInTheDocument()
    expect(within(panel as HTMLElement).queryByText('Recommended')).not.toBeInTheDocument()
  })

  it('renders taxonomy basis rules, evidence, fallback, and diagnostics from the payload', () => {
    render(<OperationalContextTaxonomyPanel decisionAssimilation={createAssimilationProjection()} />)

    const panel = screen.getByRole('heading', { name: 'Taxonomy Basis' }).closest('div')

    expect(panel).not.toBeNull()
    expect(within(panel as HTMLElement).getByText('architectural-continuity-keywords')).toBeInTheDocument()
    expect(
      within(panel as HTMLElement).getByText('Matched architectural keyword: Backend continuity services own'),
    ).toBeInTheDocument()
    expect(within(panel as HTMLElement).getByText('Heuristic fallback')).toBeInTheDocument()
    expect(
      within(panel as HTMLElement).getByText(
        'No taxonomy rules matched; defaulted to tactical so unclassified text does not become durable operational context.',
      ),
    ).toBeInTheDocument()
    expect(within(panel as HTMLElement).getByText('No taxonomy rules matched.')).toBeInTheDocument()
    expect(within(panel as HTMLElement).queryByText('Durable by UI rule')).not.toBeInTheDocument()
  })

  it('renders assimilation limit counts and omitted items as visible facts', () => {
    render(<OperationalContextAssimilationLimitPanel decisionAssimilation={createAssimilationProjection()} />)

    expect(screen.getByRole('heading', { name: 'Assimilation Limit' })).toBeInTheDocument()
    expect(screen.getByText('Analyzed: 11')).toBeInTheDocument()
    expect(screen.getByText('Qualifying: 9')).toBeInTheDocument()
    expect(screen.getByText('Assimilated: 8')).toBeInTheDocument()
    expect(screen.getByText('Omitted: 1')).toBeInTheDocument()
    expect(screen.getByText('Limit: 8')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Omitted Items' })).toBeInTheDocument()
    expect(
      screen.getByText((_, element) =>
        element?.tagName.toLowerCase() === 'li' &&
        element.textContent?.includes('OmittedByLimit: Unclassified implementation note.') === true,
      ),
    ).toBeInTheDocument()
  })

  it('renders consequences linked to originating decisions', () => {
    render(<OperationalContextConsequencePanel decisionAssimilation={createAssimilationProjection()} />)

    const panel = screen.getByRole('heading', { name: 'Decision Consequences' }).closest('div')

    expect(panel).not.toBeNull()
    expect(within(panel as HTMLElement).getByText('Operational context review')).toBeInTheDocument()
    expect(within(panel as HTMLElement).getByText('DEC-0001')).toBeInTheDocument()
    expect(
      within(panel as HTMLElement).getByText('Review surfaces must display backend promotion status.'),
    ).toBeInTheDocument()
    expect(
      within(panel as HTMLElement).getByText(
        'Backend continuity services own operational context promotion because hidden memory is not authoritative.',
      ),
    ).toBeInTheDocument()
    expect(
      within(panel as HTMLElement).getByText(
        'Review panels need to link consequence text to the originating decision.',
      ),
    ).toBeInTheDocument()
  })
})
