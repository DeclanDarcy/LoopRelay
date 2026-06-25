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
  it('renders backend-provided paths and byte counts in provided order', () => {
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

    const diagnostics = document.querySelectorAll('.diagnostic-item span')

    expect(diagnostics).toHaveLength(2)
    expect(diagnostics[0]).toHaveTextContent(
      '.agents/milestones/m0-frontend-foundations.md: 2048 bytes',
    )
    expect(diagnostics[1]).toHaveTextContent('.agents/operational_context.md: 512 bytes')
    expect(screen.getByText('Artifact Diagnostics')).toBeInTheDocument()
    expect(screen.getByText('Plan: .agents/milestones/m0-frontend-foundations.md')).toBeInTheDocument()
  })

  it('preserves the warning and hard-limit suffix labels', () => {
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

    expect(screen.getByText('.agents/warning.md: 128 bytes / warning')).toBeInTheDocument()
    expect(screen.getByText('.agents/hard-limit.md: 128 bytes / hard limit')).toBeInTheDocument()
  })

  it('renders an empty diagnostic list without adding fallback interpretation', () => {
    const { container } = render(<ExecutionContextArtifactDiagnosticsList diagnostics={[]} />)

    expect(container.querySelector('.diagnostic-list')).toBeInTheDocument()
    expect(screen.getByText('No artifact diagnostics recorded.')).toBeInTheDocument()
    expect(screen.queryByText(/bytes/)).not.toBeInTheDocument()
  })
})
