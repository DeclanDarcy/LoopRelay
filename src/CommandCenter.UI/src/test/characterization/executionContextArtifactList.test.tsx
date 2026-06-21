import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionContextArtifactList } from '../../features/execution/ExecutionContextArtifactList'
import type { ExecutionContextArtifact } from '../../types'

afterEach(() => {
  cleanup()
})

function artifact(overrides: Partial<ExecutionContextArtifact>): ExecutionContextArtifact {
  return {
    role: 'Plan',
    relativePath: '.agents/plan.md',
    name: 'plan.md',
    content: '# Plan',
    byteCount: 128,
    characterCount: 64,
    ...overrides,
  }
}

describe('execution context artifact list rendering characterization', () => {
  it('renders artifact role, relative path, and byte count in provided order', () => {
    render(
      <ExecutionContextArtifactList
        artifacts={[
          artifact({
            role: 'Milestone',
            relativePath: '.agents/milestones/m0-frontend-foundations.md',
            byteCount: 2048,
          }),
          artifact({
            role: 'OperationalContext',
            relativePath: '.agents/operational-context/context.md',
            byteCount: 512,
          }),
        ]}
      />,
    )

    const items = screen.getAllByRole('listitem')

    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent(
      'Milestone: .agents/milestones/m0-frontend-foundations.md (2048 bytes)',
    )
    expect(items[1]).toHaveTextContent(
      'OperationalContext: .agents/operational-context/context.md (512 bytes)',
    )
  })

  it('renders an empty list without adding fallback interpretation', () => {
    render(<ExecutionContextArtifactList artifacts={[]} />)

    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
  })
})
