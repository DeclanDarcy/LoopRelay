import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CommandPalette } from '../../components/shell'
import type { NavigationTarget } from '../../types'

const targets: NavigationTarget[] = [
  {
    id: 'workspace-alpha',
    kind: 'workspace',
    group: 'Alpha',
    label: 'Workspace',
    description: 'Open the repository workspace',
    classification: 'primary',
    repositoryId: 'repo-alpha',
    tab: 'workspace',
    sectionId: null,
    artifactPath: null,
    milestonePath: null,
    searchText: 'workspace alpha open the repository workspace',
  },
  {
    id: 'execution-alpha',
    kind: 'workspace',
    group: 'Alpha',
    label: 'Execution',
    description: 'Open the execution workspace',
    classification: 'primary',
    repositoryId: 'repo-alpha',
    tab: 'execution',
    sectionId: null,
    artifactPath: null,
    milestonePath: null,
    searchText: 'execution alpha open the execution workspace',
  },
  {
    id: 'continuity-alpha',
    kind: 'workspace',
    group: 'Alpha',
    label: 'Continuity',
    description: 'Open the continuity workspace',
    classification: 'primary',
    repositoryId: 'repo-alpha',
    tab: 'continuity',
    sectionId: null,
    artifactPath: null,
    milestonePath: null,
    searchText: 'continuity alpha open the continuity workspace',
  },
]

function renderPalette(overrides: Partial<Parameters<typeof CommandPalette>[0]> = {}) {
  const props = {
    isOpen: true,
    targets,
    onClose: vi.fn(),
    onOpen: vi.fn(),
    onSelectTarget: vi.fn(),
    ...overrides,
  }

  render(<CommandPalette {...props} />)

  return props
}

describe('command palette characterization', () => {
  afterEach(() => cleanup())

  it('selects highlighted navigation targets with keyboard controls', () => {
    const props = renderPalette()
    const input = screen.getByRole('textbox', { name: 'Search navigation' })

    expect(input).toHaveFocus()
    expect(screen.getByRole('button', { name: /Workspace/ })).toHaveAttribute(
      'data-highlighted',
      'true',
    )

    fireEvent.keyDown(input, { key: 'ArrowDown' })
    expect(screen.getByRole('button', { name: /Execution/ })).toHaveAttribute(
      'data-highlighted',
      'true',
    )

    fireEvent.keyDown(input, { key: 'Enter' })

    expect(props.onSelectTarget).toHaveBeenCalledWith(targets[1])
    expect(props.onClose).toHaveBeenCalledTimes(1)
  })

  it('wraps keyboard highlight and resets highlight when filtering', () => {
    renderPalette()
    const input = screen.getByRole('textbox', { name: 'Search navigation' })

    fireEvent.keyDown(input, { key: 'ArrowUp' })
    expect(screen.getByRole('button', { name: /Continuity/ })).toHaveAttribute(
      'data-highlighted',
      'true',
    )

    fireEvent.change(input, { target: { value: 'execution' } })

    expect(screen.getByRole('button', { name: /Execution/ })).toHaveAttribute(
      'data-highlighted',
      'true',
    )
    expect(screen.queryByRole('button', { name: /Workspace/ })).toBeNull()
  })
})
