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
})
