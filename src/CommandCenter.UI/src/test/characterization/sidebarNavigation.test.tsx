import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Sidebar } from '../../components/shell'

describe('sidebar navigation characterization', () => {
  afterEach(() => cleanup())

  it('renders only implemented global navigation entries', () => {
    render(
      <Sidebar
        repositories={[]}
        selectedRepositoryId={null}
        discoveryTargets={[]}
        isLoading={false}
        onOpenPalette={vi.fn()}
        onSelectRepository={vi.fn()}
        onSelectNavigationTarget={vi.fn()}
      />,
    )

    const globalNavigation = screen.getByRole('navigation', { name: 'Global navigation' })
    const globalButtons = within(globalNavigation).getAllByRole('button')

    expect(globalButtons.map((button) => button.textContent)).toEqual(['Repositories'])
    expect(globalButtons.every((button) => !button.hasAttribute('disabled'))).toBe(true)
  })
})
