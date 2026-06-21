import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { GeneratedHandoffContent } from '../../features/execution/GeneratedHandoffContent'

afterEach(() => {
  cleanup()
})

describe('generated handoff content rendering characterization', () => {
  it('preserves the loading fallback', () => {
    render(<GeneratedHandoffContent content="# Ignored while loading" isLoading={true} />)

    expect(screen.getByText('Loading generated handoff...')).toBeInTheDocument()
    expect(screen.getByText('Loading generated handoff...').parentElement).toHaveClass(
      'markdown-preview',
      'handoff-review-content',
    )
    expect(screen.queryByRole('heading', { name: 'Ignored while loading' })).not.toBeInTheDocument()
  })

  it('preserves the empty generated handoff fallback', () => {
    render(<GeneratedHandoffContent content="   " isLoading={false} />)

    expect(screen.getByText('Generated handoff is empty.')).toBeInTheDocument()
    expect(screen.getByText('Generated handoff is empty.').parentElement).toHaveClass(
      'markdown-preview',
      'handoff-review-content',
    )
  })

  it('renders generated handoff markdown with existing markdown behavior', () => {
    render(
      <GeneratedHandoffContent
        content={[
          '# Generated Handoff',
          '',
          '- Preserve workflow authority',
          '- Keep generated content review-only',
          '',
          '```',
          'literal handoff block',
          '```',
        ].join('\n')}
        isLoading={false}
      />,
    )

    expect(screen.getByRole('heading', { name: 'Generated Handoff' })).toBeInTheDocument()
    expect(screen.getByText('Preserve workflow authority')).toBeInTheDocument()
    expect(screen.getByText('Keep generated content review-only')).toBeInTheDocument()
    expect(screen.getByText('literal handoff block')).toBeInTheDocument()
  })
})
