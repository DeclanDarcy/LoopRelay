import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionContextSummaryRows } from '../../features/execution/ExecutionContextSummaryRows'
import type { ExecutionContextPreview } from '../../types'

afterEach(() => {
  cleanup()
})

function executionContextPreview(
  overrides: Partial<ExecutionContextPreview> = {},
): ExecutionContextPreview {
  return {
    repositoryId: 'repo-alpha',
    repositoryName: 'CommandCenter',
    repositoryPath: 'C:\\kernritsu\\CommandCenter',
    milestonePath: '.agents/milestones/m0-frontend-foundations.md',
    generatedAt: '2026-06-21T17:00:00.000Z',
    artifacts: [],
    repositorySnapshot: null,
    diagnostics: {
      totalBytes: 4096,
      totalCharacters: 2048,
      warningThresholdBytes: 100000,
      hardLimitBytes: 120000,
      warningThresholdExceeded: false,
      hardLimitExceeded: false,
      artifactDiagnostics: [],
      validationErrors: [],
      missingOptionalArtifacts: [],
      launchBlocked: false,
    },
    ...overrides,
  }
}

describe('execution context summary row rendering characterization', () => {
  it('renders existing summary labels and caller-provided status text', () => {
    render(
      <ExecutionContextSummaryRows
        executionContext={executionContextPreview()}
        operationalContextStatus="Included (128 bytes)"
        launchStatus="Ready"
        sizeStatus="Within limits"
      />,
    )

    expect(document.querySelector('.context-summary')).toBeInTheDocument()
    expect(screen.getByText(/^Generated: /)).toBeInTheDocument()
    expect(screen.getByText('Total: 4096 bytes')).toBeInTheDocument()
    expect(screen.getByText('Operational context: Included (128 bytes)')).toBeInTheDocument()
    expect(screen.getByText('Launch: Ready')).toBeInTheDocument()
    expect(screen.getByText('Size: Within limits')).toBeInTheDocument()
  })

  it('renders stale or blocked statuses without interpreting them', () => {
    render(
      <ExecutionContextSummaryRows
        executionContext={executionContextPreview({
          diagnostics: {
            ...executionContextPreview().diagnostics,
            totalBytes: 131072,
          },
        })}
        operationalContextStatus="Preview stale"
        launchStatus="Build an execution context for the selected milestone."
        sizeStatus="Hard limit"
      />,
    )

    expect(screen.getByText('Total: 131072 bytes')).toBeInTheDocument()
    expect(screen.getByText('Operational context: Preview stale')).toBeInTheDocument()
    expect(
      screen.getByText('Launch: Build an execution context for the selected milestone.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Size: Hard limit')).toBeInTheDocument()
  })
})
