import { describe, expect, it } from 'vitest'
import { isStartableExecutionState } from '../../lib/status'
import type { RepositoryExecutionState } from '../../types'

// A failed (or cancelled) execution must not strand the repository: the only correct next action is
// to start a new run, and the backend's start guard already permits it (it rejects only an
// already-Executing repository). The in-progress lifecycle states must be resolved first.
describe('isStartableExecutionState', () => {
  it('allows starting from the settled states Ready, Failed, and Cancelled', () => {
    const startable: RepositoryExecutionState[] = ['Ready', 'Failed', 'Cancelled']
    for (const state of startable) {
      expect(isStartableExecutionState(state)).toBe(true)
    }
  })

  it('blocks starting while an execution is still in progress', () => {
    const blocked: RepositoryExecutionState[] = [
      'Executing',
      'AwaitingAcceptance',
      'Accepted',
      'AwaitingCommit',
      'AwaitingPush',
    ]
    for (const state of blocked) {
      expect(isStartableExecutionState(state)).toBe(false)
    }
  })
})
