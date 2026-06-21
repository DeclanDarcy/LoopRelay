import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { GitPathBucket } from '../../features/execution/GitPathBucket'

afterEach(() => {
  cleanup()
})

describe('git path bucket rendering characterization', () => {
  it('renders an empty bucket with the current none text', () => {
    render(<GitPathBucket label="Staged" paths={[]} />)

    expect(screen.getByRole('heading', { level: 5, name: 'Staged' })).toBeInTheDocument()
    expect(screen.getByText('None').tagName).toBe('P')
    expect(screen.queryByRole('list')).not.toBeInTheDocument()
  })

  it('renders path items in provided order', () => {
    render(<GitPathBucket label="Modified" paths={['src/App.tsx', '.agents/plan.md']} />)

    const list = screen.getByRole('list')
    expect(within(list).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'src/App.tsx',
      '.agents/plan.md',
    ])
  })
})
