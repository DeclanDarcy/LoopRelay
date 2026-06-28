import { describe, expect, it } from 'vitest'
import { decisionRunReducer, initialDecisionRunState } from './decisionRunMachine'
import type { DecisionRunState } from '../../types'

function drive(state: DecisionRunState, actions: Parameters<typeof decisionRunReducer>[1][]) {
  return actions.reduce(decisionRunReducer, state)
}

// The three transfer phases plus the transferred confirmation, in emission order, as reducer
// actions — the prelude a Transfer-routed run streams before the normal decision flow resumes.
function transferPrelude(): Parameters<typeof decisionRunReducer>[1][] {
  return [
    { kind: 'event', event: { type: 'phase', phase: 'ProduceOperationalDelta' } },
    { kind: 'event', event: { type: 'phase', phase: 'UpdateOperationalContext' } },
    { kind: 'event', event: { type: 'phase', phase: 'StartDecisionSessionFromTransfer' } },
    {
      kind: 'event',
      event: {
        type: 'transferred',
        operationalDelta: '.agents/operational_delta.md',
        operationalContext: '.agents/operational_context.md',
      },
    },
  ]
}

describe('decisionRunReducer', () => {
  it('starts Idle with no run output and a closed review gate', () => {
    expect(initialDecisionRunState.status).toBe('Idle')
    expect(initialDecisionRunState.streamedText).toBe('')
    expect(initialDecisionRunState.proposedDecisions).toBeNull()
    expect(initialDecisionRunState.editableDecisions).toBeNull()
    expect(initialDecisionRunState.completion).toBeNull()
    expect(initialDecisionRunState.submittedPath).toBeNull()
    expect(initialDecisionRunState.submittedNumberedPath).toBeNull()
    expect(initialDecisionRunState.submittedSequence).toBeNull()
    expect(initialDecisionRunState.iteration).toBe(0)
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

  it('records the persisted path and closes the gate on submitted', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'decisions' } },
      { kind: 'edit', decisions: 'edited decisions' },
      { kind: 'event', event: { type: 'submitted', path: '.agents/decisions/decisions.md' } },
    ])

    expect(next.status).toBe('Submitted')
    expect(next.submittedPath).toBe('.agents/decisions/decisions.md')
    // The captured edit is preserved for display; the editable gate is closed.
    expect(next.editableDecisions).toBeNull()
  })

  it('surfaces the rotated numbered submission path and sequence when the loop reports them', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'decisions' } },
      {
        kind: 'event',
        event: {
          type: 'submitted',
          path: '.agents/decisions/decisions.md',
          sequence: 1,
          numberedPath: '.agents/decisions/decisions.0001.md',
        },
      },
    ])

    expect(next.submittedSequence).toBe(1)
    expect(next.submittedNumberedPath).toBe('.agents/decisions/decisions.0001.md')
  })

  it('marks the optimistic Submitting state when the reviewer submits', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'decisions' } },
      { kind: 'edit', decisions: 'edited decisions' },
      { kind: 'submit' },
    ])

    // Submit optimistically closes the gate and waits for the backend confirmation.
    expect(next.status).toBe('Submitting')
    expect(next.editableDecisions).toBeNull()
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

  it('starts the first decision turn at iteration 1', () => {
    const next = decisionRunReducer(initialDecisionRunState, {
      kind: 'event',
      event: { type: 'run-started', phase: 'DecisionRun' },
    })

    expect(next.iteration).toBe(1)
  })

  it('reopens the review gate when the next decision run auto-starts after submit', () => {
    // Submit is no longer terminal: the server runs a continuation turn and auto-starts the next
    // decision run, which arrives as a fresh run-started and reopens the human-review gate.
    const afterSubmit = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'first' } },
      { kind: 'edit', decisions: 'edited first' },
      { kind: 'event', event: { type: 'submitted', path: '.agents/decisions/decisions.md' } },
    ])

    const nextRun = drive(afterSubmit, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
    ])

    // The next run resets the streamed output and reopens streaming, advancing the iteration.
    expect(nextRun.status).toBe('Running')
    expect(nextRun.iteration).toBe(2)
    expect(nextRun.streamedText).toBe('')
    expect(nextRun.editableDecisions).toBeNull()
    expect(nextRun.submittedPath).toBeNull()

    const secondReview = decisionRunReducer(nextRun, {
      kind: 'event',
      event: { type: 'review-ready', decisions: 'second' },
    })

    expect(secondReview.status).toBe('Completed')
    expect(secondReview.editableDecisions).toBe('second')
    expect(secondReview.proposedDecisions).toBe('second')
  })

  it('drives two full review/submit iterations on one run machine', () => {
    const firstSubmit = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'proposal one' } },
      { kind: 'submit' },
      { kind: 'event', event: { type: 'submitted', path: '.agents/decisions/decisions.md' } },
    ])

    expect(firstSubmit.iteration).toBe(1)

    const secondSubmit = drive(firstSubmit, [
      // The auto-started continuation decision run.
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun' } },
      { kind: 'event', event: { type: 'review-ready', decisions: 'proposal two' } },
      { kind: 'submit' },
      { kind: 'event', event: { type: 'submitted', path: '.agents/decisions/decisions.md' } },
    ])

    expect(secondSubmit.status).toBe('Submitted')
    expect(secondSubmit.iteration).toBe(2)
  })

  it('leaves the warm Continue route un-transferring and identical to the routeless path', () => {
    const routeless = decisionRunReducer(initialDecisionRunState, {
      kind: 'event',
      event: { type: 'run-started', phase: 'DecisionRun' },
    })
    const continueRoute = decisionRunReducer(initialDecisionRunState, {
      kind: 'event',
      event: { type: 'run-started', phase: 'DecisionRun', route: 'Continue' },
    })

    expect(continueRoute.transferring).toBe(false)
    // The Continue path must be byte-identical to today's routeless run-started result.
    expect(continueRoute).toEqual(routeless)
  })

  it('raises the transfer flag on a Transfer-routed run-started', () => {
    const next = decisionRunReducer(initialDecisionRunState, {
      kind: 'event',
      event: { type: 'run-started', phase: 'DecisionRun', route: 'Transfer' },
    })

    expect(next.status).toBe('Running')
    expect(next.transferring).toBe(true)
  })

  it('keeps the labelled phase untouched while the transfer phases stream', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun', route: 'Transfer' } },
      { kind: 'event', event: { type: 'phase', phase: 'ProduceOperationalDelta' } },
      { kind: 'event', event: { type: 'phase', phase: 'UpdateOperationalContext' } },
      { kind: 'event', event: { type: 'phase', phase: 'StartDecisionSessionFromTransfer' } },
    ])

    expect(next.transferring).toBe(true)
    expect(next.status).toBe('Running')
    // The proposing-phase label is never overwritten by a transfer phase.
    expect(next.phase).toBe('DecisionRun')
  })

  it('raises the transfer flag on a phase event even without a Transfer-routed run-started', () => {
    // Resilience: a transfer phase alone (e.g. run-started missed on replay) still flags the
    // transfer without throwing or wedging the machine.
    const next = decisionRunReducer(initialDecisionRunState, {
      kind: 'event',
      event: { type: 'phase', phase: 'ProduceOperationalDelta' },
    })

    expect(next.transferring).toBe(true)
    expect(next.status).toBe('Running')
  })

  it('keeps the transfer flag raised through the transferred event', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun', route: 'Transfer' } },
      { kind: 'event', event: { type: 'phase', phase: 'StartDecisionSessionFromTransfer' } },
      {
        kind: 'event',
        event: {
          type: 'transferred',
          operationalDelta: '.agents/operational_delta.md',
          operationalContext: '.agents/operational_context.md',
        },
      },
    ])

    // The proposal still streams next, so the indicator stays raised until review-ready.
    expect(next.transferring).toBe(true)
    expect(next.status).toBe('Running')
  })

  it('clears the transfer flag once review-ready resolves the transfer', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun', route: 'Transfer' } },
      ...transferPrelude(),
      { kind: 'event', event: { type: 'review-ready', decisions: 'transferred decisions' } },
    ])

    expect(next.transferring).toBe(false)
    expect(next.status).toBe('Completed')
    expect(next.editableDecisions).toBe('transferred decisions')
  })

  it('surfaces a transfer-step failure like any other phase failure and clears the flag', () => {
    const next = drive(initialDecisionRunState, [
      { kind: 'event', event: { type: 'run-started', phase: 'DecisionRun', route: 'Transfer' } },
      { kind: 'event', event: { type: 'phase', phase: 'ProduceOperationalDelta' } },
      {
        kind: 'event',
        event: {
          type: 'failed',
          phase: 'ProduceOperationalDelta',
          reason: 'Delta synthesis failed',
          detail: 'no diff',
        },
      },
    ])

    expect(next.status).toBe('Failed')
    expect(next.transferring).toBe(false)
    expect(next.failure).toEqual({
      phase: 'ProduceOperationalDelta',
      reason: 'Delta synthesis failed',
      detail: 'no diff',
    })
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
