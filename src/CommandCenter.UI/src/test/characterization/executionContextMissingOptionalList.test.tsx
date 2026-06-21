import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionContextMissingOptionalList } from '../../features/execution/ExecutionContextMissingOptionalList'

afterEach(() => {
  cleanup()
})

describe('execution context missing optional list rendering characterization', () => {
  it('renders backend-provided paths in provided order', () => {
    render(
      <ExecutionContextMissingOptionalList
        paths={['.agents/operational_context.md', '.agents/continuity/report.md']}
      />,
    )

    const items = screen.getAllByRole('listitem')

    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('.agents/operational_context.md')
    expect(items[1]).toHaveTextContent('.agents/continuity/report.md')
    expect(screen.queryByText('None')).not.toBeInTheDocument()
  })

  it('preserves the existing empty fallback', () => {
    render(<ExecutionContextMissingOptionalList paths={[]} />)

    expect(screen.getByText('None')).toBeInTheDocument()
    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
  })
})
