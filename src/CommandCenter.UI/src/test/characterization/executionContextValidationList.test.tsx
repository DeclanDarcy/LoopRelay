import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionContextValidationList } from '../../features/execution/ExecutionContextValidationList'

afterEach(() => {
  cleanup()
})

describe('execution context validation list rendering characterization', () => {
  it('renders backend-provided validation errors in provided order', () => {
    render(
      <ExecutionContextValidationList
        validationErrors={[
          'Missing required artifact: .agents/plan.md',
          'Repository planning readiness is Incomplete.',
        ]}
      />,
    )

    const items = screen.getAllByRole('listitem')

    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('Missing required artifact: .agents/plan.md')
    expect(items[1]).toHaveTextContent('Repository planning readiness is Incomplete.')
    expect(screen.queryByText('No validation errors')).not.toBeInTheDocument()
  })

  it('renders validation message text verbatim', () => {
    const validationError = '  Preserve spacing, punctuation: [critical?] -> no derived label.  '

    render(<ExecutionContextValidationList validationErrors={[validationError]} />)

    expect(screen.getByRole('listitem').textContent).toBe(validationError)
  })

  it('preserves the existing empty fallback', () => {
    render(<ExecutionContextValidationList validationErrors={[]} />)

    expect(screen.getByText('No validation errors')).toBeInTheDocument()
    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
  })

  it('renders structured governed decision conflicts as governance blockers', () => {
    render(
      <ExecutionContextValidationList
        validationErrors={['Execution request conflicts with governed decision DEC-0007: React recomputes persistence mutation eligibility.']}
        governedConflicts={[
          {
            id: 'conflict-1',
            decisionId: 'DEC-0007',
            title: 'Persistence authority',
            statement: 'Persistence mutations must remain backend-owned.',
            conflictingExcerpt: 'React recomputes persistence mutation eligibility.',
            conflictReason: 'Governed decision DEC-0007 conflicts with selected execution context.',
            affectedContext: '.agents/milestones/m5-execution-transparency.md',
            affectedPromptSection: 'Governed Decision Projection',
            recommendedResolution: 'Resolve or supersede the governed decision conflict before launching execution.',
            severity: 'Blocking',
            originatingAuthority: 'DecisionProjectionService',
            sources: [],
            evidence: [
              'Decision statement: Persistence mutations must remain backend-owned.',
              'Conflicting excerpt: React recomputes persistence mutation eligibility.',
            ],
            diagnostics: [
              'Conflict was projected by the decisions authority and blocks execution context launch.',
            ],
          },
        ]}
      />,
    )

    const conflicts = screen.getByLabelText('Governed decision conflict diagnostics')

    expect(conflicts).toHaveTextContent('DEC-0007')
    expect(conflicts).toHaveTextContent('Blocking')
    expect(conflicts).toHaveTextContent('Governed decision DEC-0007 conflicts with selected execution context.')
    expect(conflicts).toHaveTextContent('React recomputes persistence mutation eligibility.')
    expect(conflicts).toHaveTextContent('.agents/milestones/m5-execution-transparency.md')
    expect(conflicts).toHaveTextContent('Governed Decision Projection')
    expect(conflicts).toHaveTextContent('Resolve or supersede the governed decision conflict before launching execution.')
    expect(conflicts).toHaveTextContent('DecisionProjectionService')
    expect(screen.getByLabelText('Evidence for DEC-0007')).toHaveTextContent(
      'Decision statement: Persistence mutations must remain backend-owned.',
    )
  })
})
