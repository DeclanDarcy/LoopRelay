import { describe, expect, it } from 'vitest'
import { decisionRunReducer, initialDecisionRunState } from './decisionRunMachine'
import type { DecisionRunState } from '../../types'

function drive(state: DecisionRunState, actions: Parameters<typeof decisionRunReducer>[1][]) {
  return actions.reduce(decisionRunReducer, state)
}

describe('decisionRunReducer', () => {
  it('starts Idle with no run output and a closed review gate', () => {
    expect(initialDecisionRunState.status).toBe('Idle')
    expect(initialDecisionRunState.streamedText).toBe('')
    expect(initialDecisionRunState.proposedDecisions).toBeNull()
    expect(initialDecisionRunState.editableDecisions).toBeNull()
    expect(initialDecisionRunState.completion).toBeNull()
    expect(initialDecisionRunState.submittedPath).toBeNull()
    expect(initialDecisionRunState.failure).toBeNull()
  })

  it('moves to Running on run-started', () => {
    const next = decisionRunReducer(initialDecisionRunState, {
      kind: 'event',
      event: { type: 'run-started', phase: 'DecisionRun' },
    })

    expect(next.status).toBe('Running')
    expect(next.phase).toBe('DecisionRun')
  })

  it('records the sandbox diagnostics and keeps running', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      {
        kind: 'event',
        event: { type: 'diagnostics', sandbox: 'read-only', approvals: 'never', seeded: true },
      },
    ])

    expect(next.status).toBe('Running')
    expect(next.diagnostics).toEqual({ sandbox: 'read-only', approvals: 'never', seeded: true })
  })

  it('tracks the reported phase from phase events', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'phase', phase: 'GetNextDecisions' } },
    ])

    expect(next.phase).toBe('GetNextDecisions')
    expect(next.status).toBe('Running')
  })

  it('accumulates delta text across the run', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'delta', text: 'one ' } },
      { kind: 'event', event: { type: 'delta', text: 'two' } },
    ])

    expect(next.streamedText).toBe('one two')
  })

  it('records token totals from completed without ending the run', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'phase', phase: 'GetNextDecisions' } },
      { kind: 'event', event: { type: 'completed', promptTokens: 4200, outputTokens: 1850 } },
    ])

    expect(next.completion).toEqual({ promptTokens: 4200, outputTokens: 1850 })
    // Completed is not terminal: the review gate has not opened yet.
    expect(next.status).toBe('Running')
    expect(next.editableDecisions).toBeNull()
  })

  it('keeps the review gate closed until review-ready arrives', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'delta', text: '- Use SSE for the stream.\n' } },
      { kind: 'event', event: { type: 'completed', promptTokens: 10, outputTokens: 5 } },
    ])

    expect(next.proposedDecisions).toBeNull()
    expect(next.editableDecisions).toBeNull()
  })

  it('opens the review gate and prefills the editable buffer on review-ready', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'completed', promptTokens: 10, outputTokens: 5 } },
      { kind: 'event', event: { type: 'review-ready', decisions: '- Use SSE.\n- Seed the session.' } },
    ])

    expect(next.status).toBe('Completed')
    expect(next.phase).toBeNull()
    expect(next.proposedDecisions).toBe('- Use SSE.\n- Seed the session.')
    expect(next.editableDecisions).toBe('- Use SSE.\n- Seed the session.')
  })

  it('ignores edits before the review gate opens', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'edit', decisions: 'premature edit' },
    ])

    expect(next.editableDecisions).toBeNull()
  })

  it('lets the reviewer edit the captured decisions after review-ready', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'original' } },
      { kind: 'edit', decisions: 'reviewer edited text' },
    ])

    // The original capture is preserved; only the editable buffer changes.
    expect(next.proposedDecisions).toBe('original')
    expect(next.editableDecisions).toBe('reviewer edited text')
  })

  it('reaches the Submitted terminal state with the persisted path', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'decisions' } },
      { kind: 'edit', decisions: 'edited decisions' },
      { kind: 'event', event: { type: 'submitted', path: '.agents/decisions/decisions.md' } },
    ])

    expect(next.status).toBe('Submitted')
    expect(next.submittedPath).toBe('.agents/decisions/decisions.md')
    expect(next.editableDecisions).toBe('edited decisions')
  })

  it('surfaces a failed event with phase, reason, and detail', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'phase', phase: 'GetNextDecisions' } },
      {
        kind: 'event',
        event: { type: 'failed', phase: 'GetNextDecisions', reason: 'Agent crashed', detail: 'trace' },
      },
    ])

    expect(next.status).toBe('Failed')
    expect(next.phase).toBeNull()
    expect(next.failure).toEqual({ phase: 'GetNextDecisions', reason: 'Agent crashed', detail: 'trace' })
  })

  it('surfaces a failure with no phase or detail', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'failed', reason: 'boom' } },
    ])

    expect(next.failure).toEqual({ phase: null, reason: 'boom', detail: null })
  })

  it('is resilient to a delta arriving before run-started (out of order)', () => {
    const next = decisionRunReducer(initialDecisionRunState, {
      kind: 'event',
      event: { type: 'delta', text: 'early chunk' },
    })

    expect(next.status).toBe('Running')
    expect(next.streamedText).toBe('early chunk')
  })

  it('does not reopen the review gate after submission', () => {
    const submitted = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'first' } },
      { kind: 'edit', decisions: 'edited' },
      { kind: 'event', event: { type: 'submitted', path: '.agents/decisions/decisions.md' } },
    ])

    const afterLate = decisionRunReducer(submitted, {
      kind: 'event',
      event: { type: 'review-ready', decisions: 'second' },
    })

    expect(afterLate.status).toBe('Submitted')
    expect(afterLate.editableDecisions).toBe('edited')
    expect(afterLate.proposedDecisions).toBe('first')
  })

  it('does not let a late non-terminal frame clobber a failed run', () => {
    const failed = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'failed', reason: 'boom' } },
      {
        kind: 'event',
        event: { type: 'diagnostics', sandbox: 'read-only', approvals: 'never', seeded: false },
      },
    ])

    expect(failed.status).toBe('Failed')
  })

  it('clears all run state on reset', () => {
    const submitted = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'decisions' } },
      { kind: 'event', event: { type: 'submitted', path: '.agents/decisions/decisions.md' } },
    ])

    const reset = decisionRunReducer(submitted, { kind: 'reset' })

    expect(reset).toEqual(initialDecisionRunState)
  })
})
