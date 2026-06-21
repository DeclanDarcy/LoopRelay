import { useEffect, useMemo, useRef, useState } from 'react'
import type { PrimaryWorkspaceTab } from '../../state/shellState'
import type { RepositoryDashboardProjection } from '../../types'

type PaletteTabTarget = {
  kind: 'tab'
  id: PrimaryWorkspaceTab
  label: string
}

type PaletteRepositoryTarget = {
  kind: 'repository'
  id: string
  label: string
  description: string
}

type PaletteSectionTarget = {
  kind: 'section'
  id: string
  label: string
}

type PaletteTarget = PaletteTabTarget | PaletteRepositoryTarget | PaletteSectionTarget

type CommandPaletteProps = {
  isOpen: boolean
  repositories: RepositoryDashboardProjection[]
  onClose: () => void
  onOpen: () => void
  onSelectRepository: (repositoryId: string) => void
  onSelectSection: (sectionId: string) => void
  onSelectTab: (tab: PrimaryWorkspaceTab) => void
}

const tabTargets: PaletteTabTarget[] = [
  { kind: 'tab', id: 'workspace', label: 'Workspace' },
  { kind: 'tab', id: 'execution', label: 'Execution' },
  { kind: 'tab', id: 'operational-context', label: 'Operational Context' },
  { kind: 'tab', id: 'continuity', label: 'Continuity' },
]

const sectionTargets: PaletteSectionTarget[] = [
  { kind: 'section', id: 'artifacts', label: 'Repository Artifacts' },
  { kind: 'section', id: 'execution-context', label: 'Execution Context' },
  { kind: 'section', id: 'proposal-review', label: 'Proposal Review' },
  { kind: 'section', id: 'continuity-diagnostics', label: 'Continuity Diagnostics' },
]

export function CommandPalette({
  isOpen,
  repositories,
  onClose,
  onOpen,
  onSelectRepository,
  onSelectSection,
  onSelectTab,
}: CommandPaletteProps) {
  const [query, setQuery] = useState('')
  const inputRef = useRef<HTMLInputElement | null>(null)

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        onOpen()
      }

      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onClose, onOpen])

  useEffect(() => {
    if (!isOpen) {
      setQuery('')
      return
    }

    inputRef.current?.focus()
  }, [isOpen])

  const targets = useMemo<PaletteTarget[]>(() => {
    const repositoryTargets = repositories.map((entry) => ({
      kind: 'repository' as const,
      id: entry.repository.id,
      label: entry.repository.name,
      description: entry.repository.path,
    }))

    return [...tabTargets, ...sectionTargets, ...repositoryTargets]
  }, [repositories])

  const filteredTargets = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase()
    if (!normalizedQuery) {
      return targets
    }

    return targets.filter((target) => {
      const searchable =
        target.kind === 'repository'
          ? `${target.label} ${target.description}`
          : target.label

      return searchable.toLowerCase().includes(normalizedQuery)
    })
  }, [query, targets])

  if (!isOpen) {
    return null
  }

  function selectTarget(target: PaletteTarget) {
    if (target.kind === 'repository') {
      onSelectRepository(target.id)
    }

    if (target.kind === 'tab') {
      onSelectTab(target.id)
    }

    if (target.kind === 'section') {
      onSelectSection(target.id)
    }

    onClose()
  }

  return (
    <div className="command-palette-backdrop" onMouseDown={onClose}>
      <section
        className="command-palette"
        aria-label="Command palette"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <input
          ref={inputRef}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Search navigation..."
          aria-label="Search navigation"
        />
        <div className="command-palette-results">
          {filteredTargets.map((target) => (
            <button
              type="button"
              key={`${target.kind}-${target.id}`}
              className="command-palette-item"
              onClick={() => selectTarget(target)}
            >
              <span>{target.label}</span>
              <small>{target.kind}</small>
            </button>
          ))}
        </div>
      </section>
    </div>
  )
}
