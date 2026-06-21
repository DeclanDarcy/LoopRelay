import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionContextArtifactContentPreviews } from '../../features/execution/ExecutionContextArtifactContentPreviews'
import type { ExecutionContextArtifact } from '../../types'

afterEach(() => {
  cleanup()
})

function artifact(overrides: Partial<ExecutionContextArtifact>): ExecutionContextArtifact {
  return {
    role: 'Plan',
    relativePath: '.agents/plan.md',
    name: 'plan.md',
    content: 'Plan content',
    byteCount: 12,
    characterCount: 12,
    ...overrides,
  }
}

describe('execution context artifact content preview rendering characterization', () => {
  it('renders artifacts in backend-provided order with existing summary labels', () => {
    render(
      <ExecutionContextArtifactContentPreviews
        artifacts={[
          artifact({
            role: 'Plan',
            relativePath: '.agents/plan.md',
            characterCount: 32,
          }),
          artifact({
            role: 'Handoff',
            relativePath: '.agents/handoffs/handoff.md',
            characterCount: 48,
          }),
        ]}
      />,
    )

    const summaries = screen.getAllByText(/\.agents\//)

    expect(summaries).toHaveLength(2)
    expect(summaries[0]).toHaveTextContent('Plan: .agents/plan.md (32 characters)')
    expect(summaries[1]).toHaveTextContent('Handoff: .agents/handoffs/handoff.md (48 characters)')
  })

  it('opens operational context artifacts by default and leaves other artifacts collapsed', () => {
    render(
      <ExecutionContextArtifactContentPreviews
        artifacts={[
          artifact({
            role: 'Plan',
            relativePath: '.agents/plan.md',
          }),
          artifact({
            role: 'OperationalContext',
            relativePath: '.agents/operational_context.md',
          }),
        ]}
      />,
    )

    const details = document.querySelectorAll('details')

    expect(details).toHaveLength(2)
    expect(details[0]).not.toHaveAttribute('open')
    expect(details[1]).toHaveAttribute('open')
  })

  it('uses the shared markdown preview rendering for artifact content', () => {
    render(
      <ExecutionContextArtifactContentPreviews
        artifacts={[
          artifact({
            content: '# Heading\n\n- First\n- Second',
          }),
        ]}
      />,
    )

    expect(screen.getByRole('heading', { level: 2, name: 'Heading' })).toBeInTheDocument()
    expect(screen.getByText('First')).toBeInTheDocument()
    expect(screen.getByText('Second')).toBeInTheDocument()
  })

  it('preserves the empty artifact fallback for whitespace-only content', () => {
    render(
      <ExecutionContextArtifactContentPreviews
        artifacts={[
          artifact({
            content: '   \n\t',
          }),
        ]}
      />,
    )

    expect(screen.getByText('Empty artifact.')).toBeInTheDocument()
  })
})
