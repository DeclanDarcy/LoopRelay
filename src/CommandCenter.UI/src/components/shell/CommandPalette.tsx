import { useEffect, useMemo, useRef, useState } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent } from 'react'
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
  const [highlightedIndex, setHighlightedIndex] = useState(0)
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

  useEffect(() => {
    setHighlightedIndex(0)
  }, [filteredTargets])

  if (!isOpen) {
    return null
  }

  function selectTarget(target: NavigationTarget) {
    onSelectTarget(target)
    onClose()
  }

  function handleInputKeyDown(event: ReactKeyboardEvent<HTMLInputElement>) {
    if (filteredTargets.length === 0) {
      return
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setHighlightedIndex((currentIndex) => (currentIndex + 1) % filteredTargets.length)
      return
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault()
      setHighlightedIndex(
        (currentIndex) => (currentIndex - 1 + filteredTargets.length) % filteredTargets.length,
      )
      return
    }

    if (event.key === 'Home') {
      event.preventDefault()
      setHighlightedIndex(0)
      return
    }

    if (event.key === 'End') {
      event.preventDefault()
      setHighlightedIndex(filteredTargets.length - 1)
      return
    }

    if (event.key === 'Enter') {
      event.preventDefault()
      selectTarget(filteredTargets[highlightedIndex] ?? filteredTargets[0])
    }
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
          onKeyDown={handleInputKeyDown}
          placeholder="Search navigation..."
          aria-label="Search navigation"
          aria-activedescendant={
            filteredTargets[highlightedIndex] ? `command-palette-target-${highlightedIndex}` : undefined
          }
        />
        <div className="command-palette-results" aria-label="Navigation targets">
          {filteredTargets.map((target, index) => (
            <button
              type="button"
              key={target.id}
              id={`command-palette-target-${index}`}
              data-highlighted={target.id === filteredTargets[highlightedIndex]?.id}
              className={[
                'command-palette-item',
                target.id === filteredTargets[highlightedIndex]?.id
                  ? 'command-palette-item-active'
                  : '',
              ].filter(Boolean).join(' ')}
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
