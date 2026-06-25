import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionContextArtifactDiagnosticsList } from '../../features/execution/ExecutionContextArtifactDiagnosticsList'
import type { ExecutionContextArtifactDiagnostic } from '../../types'

afterEach(() => {
  cleanup()
})

function diagnostic(
  overrides: Partial<ExecutionContextArtifactDiagnostic>,
): ExecutionContextArtifactDiagnostic {
  return {
    role: 'Plan',
    relativePath: '.agents/plan.md',
    byteCount: 128,
    characterCount: 64,
    warningThresholdBytes: 98304,
    hardLimitBytes: 262144,
    warningThresholdExceeded: false,
    hardLimitExceeded: false,
    ...overrides,
  }
}

describe('execution context artifact diagnostics rendering characterization', () => {
  it('renders backend-provided paths and byte counts through shared diagnostics in provided order', () => {
    render(
      <ExecutionContextArtifactDiagnosticsList
        diagnostics={[
          diagnostic({
            relativePath: '.agents/milestones/m0-frontend-foundations.md',
            byteCount: 2048,
          }),
          diagnostic({
            relativePath: '.agents/operational_context.md',
            byteCount: 512,
          }),
        ]}
      />,
    )

    const diagnostics = document.querySelectorAll('.explainability-diagnostic-list > ul > li')

    expect(diagnostics).toHaveLength(2)
    expect(diagnostics[0]).toHaveTextContent(
      'Plan: .agents/milestones/m0-frontend-foundations.md',
    )
    expect(diagnostics[0]).toHaveTextContent('2048 bytes')
    expect(diagnostics[1]).toHaveTextContent('Plan: .agents/operational_context.md')
    expect(diagnostics[1]).toHaveTextContent('512 bytes')
    expect(screen.getByText('Artifact Diagnostics')).toBeInTheDocument()
    expect(screen.getByText('Plan: .agents/milestones/m0-frontend-foundations.md')).toBeInTheDocument()
  })

  it('preserves warning and hard-limit tones from the shared diagnostic adapter', () => {
    render(
      <ExecutionContextArtifactDiagnosticsList
        diagnostics={[
          diagnostic({
            relativePath: '.agents/warning.md',
            warningThresholdExceeded: true,
          }),
          diagnostic({
            relativePath: '.agents/hard-limit.md',
            warningThresholdExceeded: true,
            hardLimitExceeded: true,
          }),
        ]}
      />,
    )

    expect(screen.getByText('Plan: .agents/warning.md')).toBeInTheDocument()
    expect(screen.getByText('warning')).toBeInTheDocument()
    expect(screen.getByText('Plan: .agents/hard-limit.md')).toBeInTheDocument()
    expect(screen.getByText('danger')).toBeInTheDocument()
  })

  it('renders an empty diagnostic list without adding fallback interpretation', () => {
    const { container } = render(<ExecutionContextArtifactDiagnosticsList diagnostics={[]} />)

    expect(container.querySelector('.diagnostic-list')).toBeInTheDocument()
    expect(screen.getByText('No artifact diagnostics recorded.')).toBeInTheDocument()
    expect(screen.queryByText(/bytes/)).not.toBeInTheDocument()
  })
})
