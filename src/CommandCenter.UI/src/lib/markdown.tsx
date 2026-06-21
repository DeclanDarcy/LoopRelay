import { type ReactNode } from 'react'

export function renderMarkdown(content: string) {
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
