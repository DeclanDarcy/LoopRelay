export function getOperationalContextSectionItems(content: string, heading: string) {
  const items: string[] = []
  let inSection = false

  for (const rawLine of content.split(/\r?\n/)) {
    const line = rawLine.trim()
    if (line.startsWith('## ')) {
      inSection = line.slice(3).trim().toLowerCase() === heading.toLowerCase()
      continue
    }

    if (!inSection || !line.startsWith('- ')) {
      continue
    }

    const item = line.slice(2).trim()
    if (item) {
      items.push(item)
    }
  }

  return items
}
