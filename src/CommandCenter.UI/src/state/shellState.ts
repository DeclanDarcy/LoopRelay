import { useCallback, useMemo, useState } from 'react'

export type PrimaryWorkspaceTab =
  | 'workspace'
  | 'execution'
  | 'operational-context'
  | 'decisions'
  | 'continuity'

type RepositoryPathSelections = Record<string, string>
type RepositoryTabSelections = Record<string, PrimaryWorkspaceTab>

function rememberPath(
  currentSelections: RepositoryPathSelections,
  repositoryId: string,
  relativePath: string | null,
) {
  if (!relativePath) {
    const nextSelections = { ...currentSelections }
    delete nextSelections[repositoryId]
    return nextSelections
  }

  return {
    ...currentSelections,
    [repositoryId]: relativePath,
  }
}

function reconcilePath(
  currentSelections: RepositoryPathSelections,
  repositoryId: string,
  availablePaths: string[],
) {
  const rememberedPath = currentSelections[repositoryId]
  if (rememberedPath && availablePaths.includes(rememberedPath)) {
    return currentSelections
  }

  return rememberPath(currentSelections, repositoryId, availablePaths[0] ?? null)
}

export function useShellState() {
  const [selectedRepositoryId, setSelectedRepositoryId] = useState<string | null>(null)
  const [selectedArtifactPathsByRepository, setSelectedArtifactPathsByRepository] =
    useState<RepositoryPathSelections>({})
  const [selectedMilestonePathsByRepository, setSelectedMilestonePathsByRepository] =
    useState<RepositoryPathSelections>({})
  const [activePrimaryTabsByRepository, setActivePrimaryTabsByRepository] =
    useState<RepositoryTabSelections>({})
  const [activePrimaryTab, setActivePrimaryTabState] = useState<PrimaryWorkspaceTab>('workspace')
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false)
  const [sectionTarget, setSectionTarget] = useState<string | null>(null)

  const selectedArtifactPath = selectedRepositoryId
    ? selectedArtifactPathsByRepository[selectedRepositoryId] ?? null
    : null
  const selectedMilestonePath = selectedRepositoryId
    ? selectedMilestonePathsByRepository[selectedRepositoryId] ?? null
    : null

  const selectRepository = useCallback(
    (repositoryId: string) => {
      setSelectedRepositoryId(repositoryId)
      setActivePrimaryTabState(activePrimaryTabsByRepository[repositoryId] ?? 'workspace')
    },
    [activePrimaryTabsByRepository],
  )

  const reconcileRepositorySelection = useCallback(
    (repositoryIds: string[]) => {
      setSelectedRepositoryId((currentId) => {
        if (repositoryIds.length === 0) {
          setActivePrimaryTabState('workspace')
          return null
        }

        if (currentId && repositoryIds.includes(currentId)) {
          return currentId
        }

        const nextRepositoryId = repositoryIds[0]
        setActivePrimaryTabState(activePrimaryTabsByRepository[nextRepositoryId] ?? 'workspace')
        return nextRepositoryId
      })
    },
    [activePrimaryTabsByRepository],
  )

  const selectArtifact = useCallback((repositoryId: string, relativePath: string | null) => {
    setSelectedArtifactPathsByRepository((currentSelections) =>
      rememberPath(currentSelections, repositoryId, relativePath),
    )
  }, [])

  const reconcileSelectedArtifact = useCallback((repositoryId: string, availablePaths: string[]) => {
    setSelectedArtifactPathsByRepository((currentSelections) =>
      reconcilePath(currentSelections, repositoryId, availablePaths),
    )
  }, [])

  const selectMilestone = useCallback((repositoryId: string, relativePath: string | null) => {
    setSelectedMilestonePathsByRepository((currentSelections) =>
      rememberPath(currentSelections, repositoryId, relativePath),
    )
  }, [])

  const reconcileSelectedMilestone = useCallback(
    (repositoryId: string, availablePaths: string[]) => {
      setSelectedMilestonePathsByRepository((currentSelections) =>
        reconcilePath(currentSelections, repositoryId, availablePaths),
      )
    },
    [],
  )

  const clearRepositoryNavigation = useCallback((repositoryId: string) => {
    setSelectedArtifactPathsByRepository((currentSelections) =>
      rememberPath(currentSelections, repositoryId, null),
    )
    setSelectedMilestonePathsByRepository((currentSelections) =>
      rememberPath(currentSelections, repositoryId, null),
    )
    setSelectedRepositoryId((currentId) => (currentId === repositoryId ? null : currentId))
  }, [])

  const setActivePrimaryTab = useCallback(
    (tab: PrimaryWorkspaceTab) => {
      setActivePrimaryTabState(tab)
      if (selectedRepositoryId) {
        setActivePrimaryTabsByRepository((currentSelections) => ({
          ...currentSelections,
          [selectedRepositoryId]: tab,
        }))
      }
    },
    [selectedRepositoryId],
  )

  return useMemo(
    () => ({
      selectedRepositoryId,
      selectedArtifactPath,
      selectedMilestonePath,
      activePrimaryTab,
      isCommandPaletteOpen,
      sectionTarget,
      selectRepository,
      reconcileRepositorySelection,
      selectArtifact,
      reconcileSelectedArtifact,
      selectMilestone,
      reconcileSelectedMilestone,
      clearRepositoryNavigation,
      setActivePrimaryTab,
      setIsCommandPaletteOpen,
      setSectionTarget,
    }),
    [
      activePrimaryTab,
      clearRepositoryNavigation,
      isCommandPaletteOpen,
      reconcileRepositorySelection,
      reconcileSelectedArtifact,
      reconcileSelectedMilestone,
      selectArtifact,
      selectMilestone,
      selectRepository,
      selectedArtifactPath,
      selectedMilestonePath,
      selectedRepositoryId,
      setActivePrimaryTab,
      sectionTarget,
    ],
  )
}
