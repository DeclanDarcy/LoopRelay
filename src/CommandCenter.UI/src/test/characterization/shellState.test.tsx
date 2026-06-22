import { act, renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { useShellState } from '../../state/shellState'

describe('shell navigation state', () => {
  it('selects repositories and preserves artifact and milestone paths per repository', () => {
    const { result } = renderHook(() => useShellState())

    act(() => {
      result.current.reconcileRepositorySelection(['repo-alpha', 'repo-beta'])
    })

    expect(result.current.selectedRepositoryId).toBe('repo-alpha')
    expect(result.current.selectedArtifactPath).toBeNull()
    expect(result.current.selectedMilestonePath).toBeNull()

    act(() => {
      result.current.selectArtifact('repo-alpha', '.agents/plan.md')
      result.current.selectMilestone('repo-alpha', '.agents/milestones/m0.md')
      result.current.selectRepository('repo-beta')
      result.current.selectArtifact('repo-beta', '.agents/decisions/decisions.md')
      result.current.selectMilestone('repo-beta', '.agents/milestones/m1.md')
    })

    expect(result.current.selectedRepositoryId).toBe('repo-beta')
    expect(result.current.selectedArtifactPath).toBe('.agents/decisions/decisions.md')
    expect(result.current.selectedMilestonePath).toBe('.agents/milestones/m1.md')

    act(() => {
      result.current.selectRepository('repo-alpha')
    })

    expect(result.current.selectedArtifactPath).toBe('.agents/plan.md')
    expect(result.current.selectedMilestonePath).toBe('.agents/milestones/m0.md')
  })

  it('reconciles navigation selections by id without owning projection objects', () => {
    const { result } = renderHook(() => useShellState())

    act(() => {
      result.current.reconcileRepositorySelection(['repo-alpha'])
      result.current.selectArtifact('repo-alpha', '.agents/obsolete.md')
      result.current.selectMilestone('repo-alpha', '.agents/milestones/old.md')
      result.current.reconcileSelectedArtifact('repo-alpha', ['.agents/plan.md'])
      result.current.reconcileSelectedMilestone('repo-alpha', ['.agents/milestones/m0.md'])
    })

    expect(result.current.selectedArtifactPath).toBe('.agents/plan.md')
    expect(result.current.selectedMilestonePath).toBe('.agents/milestones/m0.md')

    act(() => {
      result.current.clearRepositoryNavigation('repo-alpha')
    })

    expect(result.current.selectedRepositoryId).toBeNull()
    expect(result.current.selectedArtifactPath).toBeNull()
    expect(result.current.selectedMilestonePath).toBeNull()
  })

  it('keeps primary tab and command palette state as local navigation state', () => {
    const { result } = renderHook(() => useShellState())

    expect(result.current.activePrimaryTab).toBe('workspace')
    expect(result.current.isCommandPaletteOpen).toBe(false)

    act(() => {
      result.current.setActivePrimaryTab('execution')
      result.current.setIsCommandPaletteOpen(true)
    })

    expect(result.current.activePrimaryTab).toBe('execution')
    expect(result.current.isCommandPaletteOpen).toBe(true)
  })

  it('preserves active primary tabs per repository', () => {
    const { result } = renderHook(() => useShellState())

    act(() => {
      result.current.reconcileRepositorySelection(['repo-alpha', 'repo-beta'])
    })

    act(() => {
      result.current.setActivePrimaryTab('continuity')
    })

    act(() => {
      result.current.selectRepository('repo-beta')
    })

    expect(result.current.selectedRepositoryId).toBe('repo-beta')
    expect(result.current.activePrimaryTab).toBe('workspace')

    act(() => {
      result.current.setActivePrimaryTab('execution')
    })

    act(() => {
      result.current.selectRepository('repo-alpha')
    })

    expect(result.current.activePrimaryTab).toBe('continuity')

    act(() => {
      result.current.selectRepository('repo-beta')
    })

    expect(result.current.activePrimaryTab).toBe('execution')
  })
})
