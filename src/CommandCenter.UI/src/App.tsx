import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { invoke } from '@tauri-apps/api/core'
import './App.css'

type RepositoryAvailability = 'Available' | 'Missing' | 'AccessDenied'
type ExecutionReadiness = 'MissingPlan' | 'MissingMilestones' | 'Ready'
type ArtifactType = 'Plan' | 'OperationalContext' | 'Milestone' | 'Handoff' | 'Decision'
type ArtifactFamily = ArtifactType
type ArtifactVersionKind = 'Current' | 'Historical'

type Repository = {
  id: string
  name: string
  path: string
}

type Artifact = {
  relativePath: string
  name: string
  type: ArtifactType
  family: ArtifactFamily
  versionKind: ArtifactVersionKind
}

type ArtifactInventory = {
  plan: Artifact | null
  operationalContext: Artifact | null
  milestones: Artifact[]
  currentHandoff: Artifact | null
  historicalHandoffs: Artifact[]
  currentDecisions: Artifact | null
  historicalDecisions: Artifact[]
}

type RepositoryDashboardProjection = {
  repository: Repository
  availability: RepositoryAvailability
  readiness: ExecutionReadiness
  milestoneCount: number
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
}

type RepositoryWorkspaceProjection = {
  repository: Repository
  availability: RepositoryAvailability
  readiness: ExecutionReadiness
  artifactInventory: ArtifactInventory
  milestoneCount: number
  hasPlan: boolean
  hasOperationalContext: boolean
  hasCurrentHandoff: boolean
  hasCurrentDecisions: boolean
}

type ArtifactCategory = {
  label: string
  missingLabel: string
  artifacts: Artifact[]
}

const availabilityLabels: Record<RepositoryAvailability, string> = {
  Available: 'Available',
  Missing: 'Missing',
  AccessDenied: 'Access denied',
}

const readinessLabels: Record<ExecutionReadiness, string> = {
  MissingPlan: 'Missing plan',
  MissingMilestones: 'Missing milestones',
  Ready: 'Ready',
}

function formatError(error: unknown) {
  return error instanceof Error ? error.message : String(error)
}

function getArtifactCategories(inventory: ArtifactInventory): ArtifactCategory[] {
  return [
    {
      label: 'Plan',
      missingLabel: 'plan.md is missing.',
      artifacts: inventory.plan ? [inventory.plan] : [],
    },
    {
      label: 'Operational Context',
      missingLabel: 'operational_context.md is missing.',
      artifacts: inventory.operationalContext ? [inventory.operationalContext] : [],
    },
    {
      label: 'Milestones',
      missingLabel: 'No milestone files found.',
      artifacts: inventory.milestones,
    },
    {
      label: 'Current Handoff',
      missingLabel: 'handoff.md is missing.',
      artifacts: inventory.currentHandoff ? [inventory.currentHandoff] : [],
    },
    {
      label: 'Historical Handoffs',
      missingLabel: 'No historical handoffs found.',
      artifacts: inventory.historicalHandoffs,
    },
    {
      label: 'Current Decisions',
      missingLabel: 'decisions.md is missing.',
      artifacts: inventory.currentDecisions ? [inventory.currentDecisions] : [],
    },
    {
      label: 'Historical Decisions',
      missingLabel: 'No historical decisions found.',
      artifacts: inventory.historicalDecisions,
    },
  ]
}

function renderMarkdown(content: string) {
  const nodes: ReactNode[] = []
  const lines = content.split(/\r?\n/)
  let codeLines: string[] = []
  let listItems: string[] = []
  let inCode = false

  function flushList(keyPrefix: string) {
    if (listItems.length === 0) {
      return
    }

    nodes.push(
      <ul key={`${keyPrefix}-list-${nodes.length}`}>
        {listItems.map((item, index) => (
          <li key={`${keyPrefix}-item-${index}`}>{item}</li>
        ))}
      </ul>,
    )
    listItems = []
  }

  lines.forEach((line, index) => {
    if (line.trim().startsWith('```')) {
      if (inCode) {
        nodes.push(
          <pre key={`code-${index}`}>
            <code>{codeLines.join('\n')}</code>
          </pre>,
        )
        codeLines = []
        inCode = false
      } else {
        flushList(`before-code-${index}`)
        inCode = true
      }
      return
    }

    if (inCode) {
      codeLines.push(line)
      return
    }

    const trimmed = line.trim()

    if (!trimmed) {
      flushList(`blank-${index}`)
      return
    }

    if (trimmed.startsWith('- ')) {
      listItems.push(trimmed.slice(2))
      return
    }

    flushList(`line-${index}`)

    if (trimmed.startsWith('### ')) {
      nodes.push(<h4 key={`h4-${index}`}>{trimmed.slice(4)}</h4>)
    } else if (trimmed.startsWith('## ')) {
      nodes.push(<h3 key={`h3-${index}`}>{trimmed.slice(3)}</h3>)
    } else if (trimmed.startsWith('# ')) {
      nodes.push(<h2 key={`h2-${index}`}>{trimmed.slice(2)}</h2>)
    } else {
      nodes.push(<p key={`p-${index}`}>{trimmed}</p>)
    }
  })

  if (inCode) {
    nodes.push(
      <pre key="code-tail">
        <code>{codeLines.join('\n')}</code>
      </pre>,
    )
  }

  flushList('tail')
  return nodes
}

function getAvailableArtifactPaths(inventory: ArtifactInventory) {
  return getArtifactCategories(inventory)
    .flatMap((category) => category.artifacts)
    .map((artifact) => artifact.relativePath)
}

function App() {
  const [repositories, setRepositories] = useState<RepositoryDashboardProjection[]>([])
  const [selectedRepositoryId, setSelectedRepositoryId] = useState<string | null>(null)
  const [workspace, setWorkspace] = useState<RepositoryWorkspaceProjection | null>(null)
  const [selectedArtifactPath, setSelectedArtifactPath] = useState<string | null>(null)
  const selectedArtifactPathsByRepository = useRef<Record<string, string>>({})
  const [artifactContent, setArtifactContent] = useState('')
  const [draftContent, setDraftContent] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isWorkspaceLoading, setIsWorkspaceLoading] = useState(false)
  const [isArtifactLoading, setIsArtifactLoading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [isRotating, setIsRotating] = useState(false)
  const [isAdding, setIsAdding] = useState(false)
  const [removingRepositoryId, setRemovingRepositoryId] = useState<string | null>(null)

  const selectedRepository = useMemo(
    () =>
      repositories.find((entry) => entry.repository.id === selectedRepositoryId) ??
      repositories[0] ??
      null,
    [repositories, selectedRepositoryId],
  )

  const selectedArtifact = useMemo(() => {
    if (!workspace || !selectedArtifactPath) {
      return null
    }

    return (
      getArtifactCategories(workspace.artifactInventory)
        .flatMap((category) => category.artifacts)
        .find((artifact) => artifact.relativePath === selectedArtifactPath) ?? null
    )
  }, [selectedArtifactPath, workspace])

  const hasDraftChanges = artifactContent !== draftContent
  const canRotateSelectedArtifact =
    selectedArtifact?.versionKind === 'Current' &&
    (selectedArtifact.family === 'Handoff' || selectedArtifact.family === 'Decision')

  const selectRepository = useCallback((repositoryId: string) => {
    setSelectedRepositoryId(repositoryId)
    setSelectedArtifactPath(selectedArtifactPathsByRepository.current[repositoryId] ?? null)
  }, [])

  const selectArtifact = useCallback((repositoryId: string, relativePath: string) => {
    selectedArtifactPathsByRepository.current[repositoryId] = relativePath
    setSelectedArtifactPath(relativePath)
  }, [])

  const reconcileSelectedArtifact = useCallback(
    (repositoryId: string, nextWorkspace: RepositoryWorkspaceProjection) => {
      const artifactPaths = getAvailableArtifactPaths(nextWorkspace.artifactInventory)
      const rememberedPath = selectedArtifactPathsByRepository.current[repositoryId]

      if (rememberedPath && artifactPaths.includes(rememberedPath)) {
        setSelectedArtifactPath(rememberedPath)
        return
      }

      const nextPath = artifactPaths[0] ?? null
      setSelectedArtifactPath(nextPath)
      if (nextPath) {
        selectedArtifactPathsByRepository.current[repositoryId] = nextPath
      } else {
        delete selectedArtifactPathsByRepository.current[repositoryId]
      }
    },
    [],
  )

  const loadWorkspace = useCallback(async (repositoryId: string) => {
    setIsWorkspaceLoading(true)
    setError(null)
    try {
      const nextWorkspace = await invoke<RepositoryWorkspaceProjection>(
        'get_repository_workspace',
        { repositoryId },
      )
      setWorkspace(nextWorkspace)
      reconcileSelectedArtifact(repositoryId, nextWorkspace)
    } catch (loadError) {
      setWorkspace(null)
      setSelectedArtifactPath(null)
      setError(formatError(loadError))
    } finally {
      setIsWorkspaceLoading(false)
    }
  }, [reconcileSelectedArtifact])

  const loadRepositories = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const nextRepositories = await invoke<RepositoryDashboardProjection[]>(
        'list_repositories',
      )
      setRepositories(nextRepositories)
      setSelectedRepositoryId((currentId) => {
        if (nextRepositories.length === 0) {
          return null
        }

        if (
          currentId &&
          nextRepositories.some((entry) => entry.repository.id === currentId)
        ) {
          return currentId
        }

        return nextRepositories[0].repository.id
      })
    } catch (loadError) {
      setError(formatError(loadError))
    } finally {
      setIsLoading(false)
    }
  }, [])

  async function addRepository() {
    setIsAdding(true)
    setError(null)
    setMessage(null)
    try {
      const selectedPath = await invoke<string | null>('select_repository_directory')

      if (!selectedPath) {
        return
      }

      await invoke('register_repository', { path: selectedPath })
      setMessage('Repository registered.')
      await loadRepositories()
    } catch (addError) {
      setError(formatError(addError))
    } finally {
      setIsAdding(false)
    }
  }

  async function removeRepository(repository: Repository) {
    const confirmed = window.confirm(
      `Remove ${repository.name} from Command Center?\n\nRepository files will not be deleted.`,
    )

    if (!confirmed) {
      return
    }

    setRemovingRepositoryId(repository.id)
    setError(null)
    setMessage(null)
    try {
      await invoke('remove_repository', { repositoryId: repository.id })
      setMessage('Repository registration removed.')
      delete selectedArtifactPathsByRepository.current[repository.id]
      setWorkspace(null)
      setSelectedArtifactPath(null)
      await loadRepositories()
    } catch (removeError) {
      setError(formatError(removeError))
    } finally {
      setRemovingRepositoryId(null)
    }
  }

  async function refreshWorkspace() {
    if (!selectedRepository) {
      return
    }

    setIsWorkspaceLoading(true)
    setError(null)
    setMessage(null)
    try {
      const nextWorkspace = await invoke<RepositoryWorkspaceProjection>(
        'refresh_repository_workspace',
        { repositoryId: selectedRepository.repository.id },
      )
      setWorkspace(nextWorkspace)
      reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
      setMessage('Workspace refreshed.')
      await loadRepositories()
    } catch (refreshError) {
      setError(formatError(refreshError))
    } finally {
      setIsWorkspaceLoading(false)
    }
  }

  async function saveArtifact() {
    if (!selectedRepository || !selectedArtifact) {
      return
    }

    setIsSaving(true)
    setError(null)
    setMessage(null)
    try {
      await invoke('save_artifact_content', {
        repositoryId: selectedRepository.repository.id,
        relativePath: selectedArtifact.relativePath,
        content: draftContent,
      })
      setArtifactContent(draftContent)
      setMessage('Artifact saved.')
      await loadWorkspace(selectedRepository.repository.id)
      await loadRepositories()
    } catch (saveError) {
      setError(formatError(saveError))
    } finally {
      setIsSaving(false)
    }
  }

  async function rotateSelectedArtifact() {
    if (!selectedRepository || !selectedArtifact || !canRotateSelectedArtifact) {
      return
    }

    const artifactLabel = selectedArtifact.family === 'Handoff' ? 'current handoff' : 'current decisions'
    const confirmed = window.confirm(`Rotate ${artifactLabel}?`)

    if (!confirmed) {
      return
    }

    setIsRotating(true)
    setError(null)
    setMessage(null)
    try {
      const command =
        selectedArtifact.family === 'Handoff'
          ? 'rotate_current_handoff'
          : 'rotate_current_decisions'
      const nextWorkspace = await invoke<RepositoryWorkspaceProjection>(command, {
        repositoryId: selectedRepository.repository.id,
      })
      setWorkspace(nextWorkspace)
      reconcileSelectedArtifact(selectedRepository.repository.id, nextWorkspace)
      setMessage('Artifact rotated.')
      await loadRepositories()
    } catch (rotateError) {
      setError(formatError(rotateError))
    } finally {
      setIsRotating(false)
    }
  }

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadRepositories()
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadRepositories])

  useEffect(() => {
    if (!selectedRepository) {
      return
    }

    const timeoutId = window.setTimeout(() => {
      void loadWorkspace(selectedRepository.repository.id)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadWorkspace, selectedRepository])

  useEffect(() => {
    if (!selectedRepository || !selectedArtifactPath) {
      const timeoutId = window.setTimeout(() => {
        setArtifactContent('')
        setDraftContent('')
      }, 0)

      return () => window.clearTimeout(timeoutId)
    }

    let isCurrent = true
    const timeoutId = window.setTimeout(() => {
      setIsArtifactLoading(true)
      setError(null)

      invoke<string>('load_artifact_content', {
        repositoryId: selectedRepository.repository.id,
        relativePath: selectedArtifactPath,
      })
        .then((content) => {
          if (!isCurrent) {
            return
          }

          setArtifactContent(content)
          setDraftContent(content)
        })
        .catch((loadError) => {
          if (isCurrent) {
            setError(formatError(loadError))
          }
        })
        .finally(() => {
          if (isCurrent) {
            setIsArtifactLoading(false)
          }
        })
    }, 0)

    return () => {
      isCurrent = false
      window.clearTimeout(timeoutId)
    }
  }, [selectedArtifactPath, selectedRepository])

  return (
    <main className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Command Center</p>
          <h1>Repositories</h1>
        </div>
        <div className="header-actions">
          <button type="button" className="secondary-action" onClick={loadRepositories}>
            Refresh
          </button>
          <button
            type="button"
            className="primary-action"
            onClick={addRepository}
            disabled={isAdding}
          >
            {isAdding ? 'Adding...' : 'Add Repository'}
          </button>
        </div>
      </header>

      {error ? <div className="notice error">{error}</div> : null}
      {message ? <div className="notice success">{message}</div> : null}

      <section className="workspace-grid" aria-label="Repository workspace">
        <section className="repository-list" aria-label="Registered repositories">
          <div className="section-heading">
            <h2>Dashboard</h2>
            <span>{repositories.length} registered</span>
          </div>

          {isLoading ? (
            <p className="empty-state">Loading repositories...</p>
          ) : repositories.length === 0 ? (
            <p className="empty-state">No repositories registered.</p>
          ) : (
            <div className="repository-items">
              {repositories.map((entry) => {
                const isSelected = entry.repository.id === selectedRepository?.repository.id

                return (
                  <button
                    type="button"
                    key={entry.repository.id}
                    className={`repository-item${isSelected ? ' selected' : ''}`}
                    onClick={() => selectRepository(entry.repository.id)}
                  >
                    <span className="repository-name">{entry.repository.name}</span>
                    <span className="repository-path">{entry.repository.path}</span>
                    <span
                      className={`availability availability-${entry.availability.toLowerCase()}`}
                    >
                      {availabilityLabels[entry.availability]}
                    </span>
                    <span className={`readiness readiness-${entry.readiness.toLowerCase()}`}>
                      {readinessLabels[entry.readiness]}
                    </span>
                    <span className="repository-metadata">
                      {entry.milestoneCount} milestones
                    </span>
                    <span className="repository-metadata">
                      Handoff {entry.hasCurrentHandoff ? 'present' : 'missing'}
                    </span>
                    <span className="repository-metadata">
                      Decisions {entry.hasCurrentDecisions ? 'present' : 'missing'}
                    </span>
                  </button>
                )
              })}
            </div>
          )}
        </section>

        <section className="repository-details" aria-label="Repository details">
          <div className="section-heading">
            <h2>Workspace</h2>
            <div className="section-actions">
              <button
                type="button"
                className="secondary-action"
                onClick={() => void refreshWorkspace()}
                disabled={!selectedRepository || isWorkspaceLoading}
              >
                {isWorkspaceLoading ? 'Refreshing...' : 'Refresh Workspace'}
              </button>
            </div>
          </div>

          {selectedRepository ? (
            <div className="details-body">
              <div className="details-title-row">
                <div>
                  <p className="eyebrow">Selected repository</p>
                  <h3>{selectedRepository.repository.name}</h3>
                </div>
                <span
                  className={`availability availability-${selectedRepository.availability.toLowerCase()}`}
                >
                  {availabilityLabels[selectedRepository.availability]}
                </span>
              </div>

              <dl className="details-list">
                <div>
                  <dt>Path</dt>
                  <dd>{selectedRepository.repository.path}</dd>
                </div>
                <div>
                  <dt>Readiness</dt>
                  <dd>{readinessLabels[workspace?.readiness ?? selectedRepository.readiness]}</dd>
                </div>
                <div>
                  <dt>Milestones</dt>
                  <dd>{workspace?.milestoneCount ?? selectedRepository.milestoneCount}</dd>
                </div>
              </dl>

              <div className="summary-grid">
                <span>Plan: {workspace?.hasPlan ? 'Present' : 'Missing'}</span>
                <span>
                  Operational context:{' '}
                  {workspace?.hasOperationalContext ? 'Present' : 'Missing'}
                </span>
                <span>Handoff: {workspace?.hasCurrentHandoff ? 'Present' : 'Missing'}</span>
                <span>Decisions: {workspace?.hasCurrentDecisions ? 'Present' : 'Missing'}</span>
              </div>

              {workspace ? (
                <div className="artifact-workspace">
                  <section className="artifact-explorer" aria-label="Artifact explorer">
                    {getArtifactCategories(workspace.artifactInventory).map((category) => (
                      <div className="artifact-category" key={category.label}>
                        <h4>{category.label}</h4>
                        {category.artifacts.length === 0 ? (
                          <p className="missing-artifact">{category.missingLabel}</p>
                        ) : (
                          <div className="artifact-list">
                            {category.artifacts.map((artifact) => (
                              <button
                                type="button"
                                key={artifact.relativePath}
                                className={`artifact-item${
                                  artifact.relativePath === selectedArtifactPath ? ' selected' : ''
                                }`}
                                onClick={() =>
                                  selectArtifact(
                                    selectedRepository.repository.id,
                                    artifact.relativePath,
                                  )
                                }
                              >
                                <span>{artifact.name}</span>
                                <span>{artifact.versionKind}</span>
                              </button>
                            ))}
                          </div>
                        )}
                      </div>
                    ))}
                  </section>

                  <section className="artifact-panel" aria-label="Artifact content">
                    {selectedArtifact ? (
                      <>
                        <div className="artifact-panel-header">
                          <div>
                            <p className="eyebrow">{selectedArtifact.family}</p>
                            <h4>{selectedArtifact.name}</h4>
                            <span>{selectedArtifact.relativePath}</span>
                          </div>
                          <div className="artifact-panel-actions">
                            {canRotateSelectedArtifact ? (
                              <button
                                type="button"
                                className="secondary-action"
                                onClick={() => void rotateSelectedArtifact()}
                                disabled={
                                  isRotating ||
                                  isArtifactLoading ||
                                  isSaving ||
                                  hasDraftChanges
                                }
                                title={
                                  hasDraftChanges
                                    ? 'Save changes before rotating.'
                                    : 'Archive the current artifact to the next historical file.'
                                }
                              >
                                {isRotating ? 'Rotating...' : 'Rotate'}
                              </button>
                            ) : null}
                            <button
                              type="button"
                              className="primary-action"
                              onClick={() => void saveArtifact()}
                              disabled={isSaving || isArtifactLoading || !hasDraftChanges}
                            >
                              {isSaving ? 'Saving...' : 'Save'}
                            </button>
                          </div>
                        </div>
                        <textarea
                          className="artifact-editor"
                          value={draftContent}
                          onChange={(event) => setDraftContent(event.target.value)}
                          spellCheck={false}
                          disabled={isArtifactLoading}
                        />
                        <div className="markdown-preview">
                          {isArtifactLoading ? (
                            <p>Loading artifact...</p>
                          ) : draftContent.trim() ? (
                            renderMarkdown(draftContent)
                          ) : (
                            <p>Empty artifact.</p>
                          )}
                        </div>
                      </>
                    ) : (
                      <p className="empty-state">No artifact selected.</p>
                    )}
                  </section>
                </div>
              ) : (
                <p className="empty-state">Loading workspace...</p>
              )}

              <div className="details-actions">
                <button
                  type="button"
                  className="danger-action"
                  onClick={() => void removeRepository(selectedRepository.repository)}
                  disabled={removingRepositoryId === selectedRepository.repository.id}
                >
                  {removingRepositoryId === selectedRepository.repository.id
                    ? 'Removing...'
                    : 'Remove Registration'}
                </button>
              </div>
            </div>
          ) : (
            <p className="empty-state">Select or add a repository.</p>
          )}
        </section>
      </section>
    </main>
  )
}

export default App
