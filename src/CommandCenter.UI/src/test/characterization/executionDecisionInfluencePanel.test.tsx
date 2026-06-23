import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionDecisionInfluencePanel } from '../../features/execution/ExecutionDecisionInfluencePanel'
import type { DecisionInfluenceTrace } from '../../types'

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
    diagnostics: ['No projection conflicts were recorded.'],
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
