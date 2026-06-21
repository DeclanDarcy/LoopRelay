import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ExecutionWorkflowRail } from '../../features/execution/ExecutionWorkflowRail'
import type { ExecutionWorkflowStep } from '../../types'

afterEach(() => {
  cleanup()
})

describe('execution workflow rail rendering characterization', () => {
  it('renders caller-provided steps in order with state classes and details', () => {
    const steps: ExecutionWorkflowStep[] = [
      {
        key: 'context',
        label: 'Context',
        detail: 'Prepared',
        state: 'complete',
      },
      {
        key: 'execution',
        label: 'Execution',
        detail: 'Running',
        state: 'current',
      },
      {
        key: 'handoff',
        label: 'Handoff',
        detail: 'Pending execution',
        state: 'pending',
      },
      {
        key: 'commit',
        label: 'Commit',
        detail: 'Pending acceptance',
        state: 'blocked',
      },
    ]

    render(<ExecutionWorkflowRail steps={steps} />)

    const rail = screen.getByLabelText('Execution lifecycle')
    const rows = rail.querySelectorAll('.execution-workflow-step')

    expect(Array.from(rows).map((row) => row.querySelector('span')?.textContent)).toEqual([
      'Context',
      'Execution',
      'Handoff',
      'Commit',
    ])
    expect(rows[0]).toHaveClass('execution-workflow-step-complete')
    expect(rows[1]).toHaveClass('execution-workflow-step-current')
    expect(rows[2]).toHaveClass('execution-workflow-step-pending')
    expect(rows[3]).toHaveClass('execution-workflow-step-blocked')
    expect(screen.getByText('Prepared')).toBeInTheDocument()
    expect(screen.getByText('Running')).toBeInTheDocument()
    expect(screen.getByText('Pending execution')).toBeInTheDocument()
    expect(screen.getByText('Pending acceptance')).toBeInTheDocument()
  })
})
