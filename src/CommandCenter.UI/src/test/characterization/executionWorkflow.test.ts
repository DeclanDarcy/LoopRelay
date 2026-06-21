import { describe, expect, it } from 'vitest'
import { getExecutionWorkflowSteps } from '../../lib'
import type { RepositoryExecutionState } from '../../types'

function summarize(repositoryState: RepositoryExecutionState, hasContext = false, hasSession = false) {
  return getExecutionWorkflowSteps(repositoryState, hasContext, hasSession).map((step) => ({
    key: step.key,
    detail: step.detail,
    state: step.state,
  }))
}

describe('execution workflow display mapping characterization', () => {
  it('preserves the ready lifecycle before and after context preview', () => {
    expect(summarize('Ready')).toEqual([
      { key: 'context', detail: 'Needs preview', state: 'current' },
      { key: 'execution', detail: 'Not started', state: 'pending' },
      { key: 'handoff', detail: 'Pending execution', state: 'pending' },
      { key: 'commit', detail: 'Pending acceptance', state: 'pending' },
      { key: 'push', detail: 'Pending commit', state: 'pending' },
    ])

    expect(summarize('Ready', true)).toEqual([
      { key: 'context', detail: 'Prepared', state: 'complete' },
      { key: 'execution', detail: 'Not started', state: 'current' },
      { key: 'handoff', detail: 'Pending execution', state: 'pending' },
      { key: 'commit', detail: 'Pending acceptance', state: 'pending' },
      { key: 'push', detail: 'Pending commit', state: 'pending' },
    ])
  })

  it('preserves active and review workflow states', () => {
    expect(summarize('Executing', true, true)).toEqual([
      { key: 'context', detail: 'Prepared', state: 'complete' },
      { key: 'execution', detail: 'Running', state: 'current' },
      { key: 'handoff', detail: 'Pending execution', state: 'pending' },
      { key: 'commit', detail: 'Pending acceptance', state: 'pending' },
      { key: 'push', detail: 'Pending commit', state: 'pending' },
    ])

    expect(summarize('AwaitingAcceptance', false, true)).toEqual([
      { key: 'context', detail: 'Prepared', state: 'complete' },
      { key: 'execution', detail: 'Completed', state: 'complete' },
      { key: 'handoff', detail: 'Awaiting review', state: 'current' },
      { key: 'commit', detail: 'Pending acceptance', state: 'pending' },
      { key: 'push', detail: 'Pending commit', state: 'pending' },
    ])
  })

  it('preserves commit, push, completed, and blocked workflow states', () => {
    expect(summarize('AwaitingCommit', false, true)).toEqual([
      { key: 'context', detail: 'Prepared', state: 'complete' },
      { key: 'execution', detail: 'Completed', state: 'complete' },
      { key: 'handoff', detail: 'Accepted or closed', state: 'complete' },
      { key: 'commit', detail: 'Awaiting review', state: 'current' },
      { key: 'push', detail: 'Pending commit', state: 'pending' },
    ])

    expect(summarize('AwaitingPush', false, true)).toEqual([
      { key: 'context', detail: 'Prepared', state: 'complete' },
      { key: 'execution', detail: 'Completed', state: 'complete' },
      { key: 'handoff', detail: 'Accepted or closed', state: 'complete' },
      { key: 'commit', detail: 'Committed', state: 'complete' },
      { key: 'push', detail: 'Awaiting push', state: 'current' },
    ])

    expect(summarize('Ready', false, true)).toEqual([
      { key: 'context', detail: 'Prepared', state: 'complete' },
      { key: 'execution', detail: 'Completed', state: 'complete' },
      { key: 'handoff', detail: 'Accepted or closed', state: 'complete' },
      { key: 'commit', detail: 'Committed', state: 'complete' },
      { key: 'push', detail: 'Published', state: 'complete' },
    ])

    expect(summarize('Failed', false, true)).toEqual([
      { key: 'context', detail: 'Prepared', state: 'complete' },
      { key: 'execution', detail: 'Failed', state: 'blocked' },
      { key: 'handoff', detail: 'Unavailable', state: 'blocked' },
      { key: 'commit', detail: 'Pending acceptance', state: 'blocked' },
      { key: 'push', detail: 'Pending commit', state: 'blocked' },
    ])
  })
})
