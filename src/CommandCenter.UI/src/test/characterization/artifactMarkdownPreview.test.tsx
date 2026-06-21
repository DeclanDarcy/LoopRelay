import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ArtifactMarkdownPreview } from '../../features/artifacts/ArtifactMarkdownPreview'

afterEach(() => {
  cleanup()
})

describe('artifact markdown preview rendering characterization', () => {
  it('renders the existing loading fallback', () => {
    render(<ArtifactMarkdownPreview content="# Loaded later" isLoading={true} />)

    expect(screen.getByText('Loading artifact...')).toBeInTheDocument()
    expect(screen.getByText('Loading artifact...').closest('.markdown-preview')).not.toBeNull()
  })

  it('renders markdown content when the artifact draft has text', () => {
    render(<ArtifactMarkdownPreview content={'# Handoff\n\n- Keep authority in backend'} isLoading={false} />)

    expect(screen.getByRole('heading', { level: 2, name: 'Handoff' })).toBeInTheDocument()
    expect(screen.getByText('Keep authority in backend')).toBeInTheDocument()
  })

  it('renders the existing empty fallback for whitespace-only content', () => {
    render(<ArtifactMarkdownPreview content={' \n\t '} isLoading={false} />)

    expect(screen.getByText('Empty artifact.')).toBeInTheDocument()
  })
})
