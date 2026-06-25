import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionDecisionInfluencePanel } from '../../features/execution/ExecutionDecisionInfluencePanel'
import type { DecisionInfluenceTrace, DecisionProjectionDecisionDiagnostic } from '../../types'

afterEach(() => {
  cleanup()
})

function trace(overrides: Partial<DecisionInfluenceTrace> = {}): DecisionInfluenceTrace {
  return {
    id: 'execution-sessionalpha000000000000000',
    repositoryId: 'repo-alpha',
    executionSessionId: 'session-alpha',
    recordedAt: '2026-06-23T16:20:00.000Z',
    projectionGeneratedAt: '2026-06-23T16:19:00.000Z',
    projectionFingerprint: 'fingerprint-alpha',
    statements: [
      {
        statementId: 'ECON-0001',
        decisionId: 'DEC-0001',
        title: 'Preserve governance boundary',
        statement: 'Execution consumes only accepted resolved decisions.',
        classification: 'Architectural',
        projectionKind: 'ArchitecturalConstraint',
        statementType: 'Constraint',
        promptSection: 'Decision Constraints',
        priorityRank: null,
        sources: [],
        adherenceObservations: [],
      },
      {
        statementId: 'EDIR-0001',
        decisionId: 'DEC-0002',
        title: 'Use persisted influence',
        statement: 'Load influence from persisted execution traces.',
        classification: 'Tactical',
        projectionKind: 'WorkflowPolicy',
        statementType: 'Directive',
        promptSection: 'Decision Directives',
        priorityRank: null,
        sources: [],
        adherenceObservations: [],
      },
      {
        statementId: 'EPRI-0001',
        decisionId: 'DEC-0002',
        title: 'Use persisted influence',
        statement: 'Prioritize persisted traces before analytics.',
        classification: 'Strategic',
        projectionKind: 'WorkflowPolicy',
        statementType: 'Priority',
        promptSection: 'Decision Priorities',
        priorityRank: 1,
        sources: [],
        adherenceObservations: [],
      },
      {
        statementId: 'EARC-0001',
        decisionId: 'DEC-0001',
        title: 'Preserve governance boundary',
        statement: 'Decision services own lifecycle authority.',
        classification: 'Architectural',
        projectionKind: 'RepositoryConvention',
        statementType: 'ArchitectureRule',
        promptSection: 'Architecture Rules',
        priorityRank: null,
        sources: [],
        adherenceObservations: [],
      },
    ],
    includedDecisions: [],
    excludedDecisions: [],
    supersededDecisions: [],
    conflictingDecisions: [],
    ignoredDecisions: [],
    blockedDecisions: [],
    diagnostics: ['No projection conflicts were recorded.'],
    ...overrides,
  }
}

function diagnostic(
  decisionId: string,
  title: string,
  reason: string,
  overrides: Partial<DecisionProjectionDecisionDiagnostic> = {},
): DecisionProjectionDecisionDiagnostic {
  return {
    decisionId,
    title,
    state: 'Resolved',
    outcome: 'Accepted',
    classification: 'Tactical',
    reason,
    projectedStatementIds: [],
    ...overrides,
  }
}

describe('execution decision influence panel characterization', () => {
  it('renders persisted execution influence grouped by projected statement type', () => {
    render(<ExecutionDecisionInfluencePanel trace={trace()} />)

    expect(screen.getByRole('region', { name: 'Decision influence' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 4, name: '2 influencing decisions' })).toBeInTheDocument()
    expect(screen.getByText('Session: session-alpha')).toBeInTheDocument()
    expect(screen.getByText('Projection fingerprint: fingerprint-alpha')).toBeInTheDocument()

    const decisions = screen.getByText('Influencing Decisions').closest('.execution-influence-section')
    expect(decisions).not.toBeNull()
    expect(within(decisions as HTMLElement).getByText('DEC-0001')).toBeInTheDocument()
    expect(within(decisions as HTMLElement).getByText('DEC-0002')).toBeInTheDocument()

    expect(screen.getByText('Projected Constraints')).toBeInTheDocument()
    expect(screen.getByText('Execution consumes only accepted resolved decisions.')).toBeInTheDocument()
    expect(screen.getByText('Projected Directives')).toBeInTheDocument()
    expect(screen.getByText('Load influence from persisted execution traces.')).toBeInTheDocument()
    expect(screen.getByText('Projected Priorities')).toBeInTheDocument()
    expect(screen.getByText('Rank 1')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 5, name: 'Architecture Rules' })).toBeInTheDocument()
    expect(screen.getByText('Decision services own lifecycle authority.')).toBeInTheDocument()
    expect(screen.getByText('No projection conflicts were recorded.')).toBeInTheDocument()
  })

  it('renders backend-provided influence reason categories without deriving categories from statements', () => {
    render(
      <ExecutionDecisionInfluencePanel
        trace={trace({
          includedDecisions: [
            diagnostic('DEC-0001', 'Preserve governance boundary', 'Included because it is accepted and resolved.'),
          ],
          excludedDecisions: [
            diagnostic('DEC-0003', 'Skip draft automation', 'Excluded because the decision remains under review.', {
              state: 'UnderReview',
              outcome: null,
            }),
          ],
          supersededDecisions: [
            diagnostic('DEC-0004', 'Use legacy projection', 'Superseded by DEC-0002 before prompt projection.', {
              state: 'Superseded',
            }),
          ],
          conflictingDecisions: [
            diagnostic('DEC-0005', 'Contradict persisted trace', 'Conflicts with DEC-0001 projected statement ECON-0001.'),
          ],
          ignoredDecisions: [
            diagnostic('DEC-0006', 'Ignore local draft', 'Ignored because it does not contribute executable statements.', {
              outcome: 'Deferred',
            }),
          ],
          blockedDecisions: [
            diagnostic('DEC-0007', 'Block unsafe launch', 'Blocked by governance finding GOV-0001.', {
              classification: 'Operational',
            }),
          ],
        })}
      />,
    )

    const categories = screen.getByLabelText('Decision influence reason categories')
    expect(within(categories).getByText('Included Decisions')).toBeInTheDocument()
    expect(within(categories).getByText('Included because it is accepted and resolved.')).toBeInTheDocument()
    expect(within(categories).getByText('Excluded Decisions')).toBeInTheDocument()
    expect(within(categories).getByText('Excluded because the decision remains under review.')).toBeInTheDocument()
    expect(within(categories).getByText('Superseded Decisions')).toBeInTheDocument()
    expect(within(categories).getByText('Superseded by DEC-0002 before prompt projection.')).toBeInTheDocument()
    expect(within(categories).getByText('Conflicting Decisions')).toBeInTheDocument()
    expect(within(categories).getByText('Conflicts with DEC-0001 projected statement ECON-0001.')).toBeInTheDocument()
    expect(within(categories).getByText('Ignored Decisions')).toBeInTheDocument()
    expect(within(categories).getByText('Ignored because it does not contribute executable statements.')).toBeInTheDocument()
    expect(within(categories).getByText('Blocked Decisions')).toBeInTheDocument()
    expect(within(categories).getByText('Blocked by governance finding GOV-0001.')).toBeInTheDocument()
  })

  it('keeps backend influence categories visible when only statement-derived decision ids exist', () => {
    render(<ExecutionDecisionInfluencePanel trace={trace()} />)

    const categories = screen.getByLabelText('Decision influence reason categories')
    expect(within(categories).getByText('No included decisions were projected.')).toBeInTheDocument()
    expect(within(categories).getByText('No excluded decisions were projected.')).toBeInTheDocument()
    expect(within(categories).getByText('No superseded decisions were projected.')).toBeInTheDocument()
    expect(within(categories).getByText('No conflicting decisions were projected.')).toBeInTheDocument()
    expect(within(categories).getByText('No ignored decisions were projected.')).toBeInTheDocument()
    expect(within(categories).getByText('No blocked decisions were projected.')).toBeInTheDocument()
  })

  it('renders loading, error, and missing-trace states without stale content', () => {
    const { rerender } = render(<ExecutionDecisionInfluencePanel trace={null} isLoading />)

    expect(screen.getByText('Loading decision influence.')).toBeInTheDocument()
    expect(screen.queryByText('No persisted influence trace is available.')).not.toBeInTheDocument()

    rerender(<ExecutionDecisionInfluencePanel trace={null} error="Trace was not found." />)
    expect(screen.getByText('Trace was not found.')).toBeInTheDocument()
    expect(screen.queryByText('Loading decision influence.')).not.toBeInTheDocument()

    rerender(<ExecutionDecisionInfluencePanel trace={null} />)
    expect(screen.getByText('No persisted influence trace is available.')).toBeInTheDocument()
  })
})
