import { useEffect, useMemo, useRef, useState } from 'react'
import type { NavigationTarget } from '../../types'

type CommandPaletteProps = {
  isOpen: boolean
  targets: NavigationTarget[]
  onClose: () => void
  onOpen: () => void
  onSelectTarget: (target: NavigationTarget) => void
}

export function CommandPalette({
  isOpen,
  targets,
  onClose,
  onOpen,
  onSelectTarget,
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
    if (isOpen) {
      inputRef.current?.focus()
    }
  }, [isOpen])

  const filteredTargets = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase()
    if (!normalizedQuery) {
      return targets
    }

    return targets.filter((target) => target.searchText.includes(normalizedQuery))
  }, [query, targets])

  if (!isOpen) {
    return null
  }

  function selectTarget(target: NavigationTarget) {
    onSelectTarget(target)
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
              key={target.id}
              className="command-palette-item"
              onClick={() => selectTarget(target)}
            >
              <span>{target.label}</span>
              <small>{target.group}</small>
              <em>{target.description}</em>
            </button>
          ))}
          {filteredTargets.length === 0 ? (
            <p className="command-palette-empty">No navigation targets found.</p>
          ) : null}
        </div>
      </section>
    </div>
  )
}
