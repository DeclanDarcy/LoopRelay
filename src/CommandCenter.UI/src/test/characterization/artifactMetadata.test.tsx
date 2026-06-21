import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ArtifactMetadata } from '../../features/artifacts/ArtifactMetadata'
import type { Artifact } from '../../types'

afterEach(() => {
  cleanup()
})

const currentHandoff: Artifact = {
  relativePath: '.agents/handoffs/handoff.md',
  name: 'handoff.md',
  type: 'Handoff',
  family: 'Handoff',
  versionKind: 'Current',
}

describe('artifact metadata rendering characterization', () => {
  it('renders the existing family, name, and relative path labels', () => {
    render(<ArtifactMetadata artifact={currentHandoff} />)

    expect(screen.getByText('Handoff')).toHaveClass('eyebrow')
    expect(screen.getByRole('heading', { level: 4, name: 'handoff.md' })).toBeInTheDocument()
    expect(screen.getByText('.agents/handoffs/handoff.md')).toBeInTheDocument()
  })
})
