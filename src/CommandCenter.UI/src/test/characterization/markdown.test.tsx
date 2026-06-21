import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { renderMarkdown } from '../../lib'

function renderPreview(content: string) {
  return render(<div>{renderMarkdown(content)}</div>)
}

afterEach(() => {
  cleanup()
})

describe('markdown rendering characterization', () => {
  it('preserves current heading, list, paragraph, and fenced code rendering', () => {
    const { container } = renderPreview(`# Title

## Section

### Detail

Opening paragraph.

- First item
- Second item

\`\`\`tsx
const value = 1
  const indented = true
\`\`\`

Closing paragraph.`)

    expect(screen.getByRole('heading', { level: 2, name: 'Title' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 3, name: 'Section' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 4, name: 'Detail' })).toBeInTheDocument()
    expect(screen.getByText('Opening paragraph.').tagName).toBe('P')
    expect(screen.getByText('Closing paragraph.').tagName).toBe('P')

    const list = screen.getByRole('list')
    expect(within(list).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'First item',
      'Second item',
    ])

    expect(container.querySelector('code')?.textContent).toBe('const value = 1\n  const indented = true')
  })

  it('renders unsupported markdown constructs literally', () => {
    renderPreview(`> Quoted

Inline \`code\` and [link](https://example.test).

| A | B |
| - | - |
| 1 | 2 |`)

    expect(screen.getByText('> Quoted').tagName).toBe('P')
    expect(screen.queryByRole('blockquote')).not.toBeInTheDocument()
    expect(screen.getByText('Inline `code` and [link](https://example.test).').tagName).toBe('P')
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
    expect(screen.getByText('| A | B |').tagName).toBe('P')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('flushes trailing lists and unterminated fenced code blocks', () => {
    const { container } = renderPreview(`- Tail item

\`\`\`
unterminated`)

    expect(screen.getByText('Tail item').tagName).toBe('LI')
    expect(container.querySelector('code')?.textContent).toBe('unterminated')
  })
})
