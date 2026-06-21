import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionRepositorySnapshotPanel } from '../../features/execution/ExecutionRepositorySnapshotPanel'
import type { ExecutionRepositorySnapshot } from '../../types'

afterEach(() => {
  cleanup()
})

const dirtySnapshot: ExecutionRepositorySnapshot = {
  branch: 'main',
  capturedAt: '2026-06-21T17:00:00.000Z',
  dirtyState: {
    stagedPaths: ['.agents/handoffs/handoff.md'],
    modifiedPaths: ['src/CommandCenter.UI/src/App.tsx'],
    addedPaths: ['src/CommandCenter.UI/src/features/execution/ExecutionRepositorySnapshotPanel.tsx'],
    deletedPaths: ['legacy.txt'],
    renamedPaths: ['old.md -> new.md'],
    untrackedPaths: ['scratch.md'],
    isClean: false,
  },
}

describe('execution repository snapshot rendering characterization', () => {
  it('renders nothing when no repository snapshot is available', () => {
    const { container } = render(<ExecutionRepositorySnapshotPanel repositorySnapshot={null} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders backend-provided snapshot labels, timestamp formatting, and buckets', () => {
    render(<ExecutionRepositorySnapshotPanel repositorySnapshot={dirtySnapshot} />)

    expect(
      screen.getByRole('heading', { level: 5, name: 'Repository Snapshot' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Branch: main')).toBeInTheDocument()
    expect(screen.getByText('State: Dirty')).toBeInTheDocument()
    expect(
      screen.getByText(`Captured: ${new Date(dirtySnapshot.capturedAt).toLocaleString()}`),
    ).toBeInTheDocument()

    expect(screen.getByRole('heading', { level: 5, name: 'Staged' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 5, name: 'Modified' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 5, name: 'Added' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 5, name: 'Deleted' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 5, name: 'Renamed' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 5, name: 'Untracked' })).toBeInTheDocument()

    const lists = screen.getAllByRole('list')
    expect(within(lists[0]).getByRole('listitem')).toHaveTextContent(
      '.agents/handoffs/handoff.md',
    )
    expect(within(lists[1]).getByRole('listitem')).toHaveTextContent(
      'src/CommandCenter.UI/src/App.tsx',
    )
    expect(within(lists[2]).getByRole('listitem')).toHaveTextContent(
      'src/CommandCenter.UI/src/features/execution/ExecutionRepositorySnapshotPanel.tsx',
    )
    expect(within(lists[3]).getByRole('listitem')).toHaveTextContent('legacy.txt')
    expect(within(lists[4]).getByRole('listitem')).toHaveTextContent('old.md -> new.md')
    expect(within(lists[5]).getByRole('listitem')).toHaveTextContent('scratch.md')
  })

  it('preserves the detached branch fallback and clean state label', () => {
    render(
      <ExecutionRepositorySnapshotPanel
        repositorySnapshot={{
          ...dirtySnapshot,
          branch: '',
          dirtyState: {
            ...dirtySnapshot.dirtyState,
            stagedPaths: [],
            modifiedPaths: [],
            addedPaths: [],
            deletedPaths: [],
            renamedPaths: [],
            untrackedPaths: [],
            isClean: true,
          },
        }}
      />,
    )

    expect(screen.getByText('Branch: (detached)')).toBeInTheDocument()
    expect(screen.getByText('State: Clean')).toBeInTheDocument()
    expect(screen.getAllByText('None')).toHaveLength(6)
  })
})
