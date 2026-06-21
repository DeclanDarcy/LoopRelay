import { renderMarkdown } from '../../lib/markdown'

type GeneratedHandoffContentProps = {
  content: string
  isLoading: boolean
}

export function GeneratedHandoffContent({ content, isLoading }: GeneratedHandoffContentProps) {
  return (
    <div className="markdown-preview handoff-review-content">
      {isLoading ? (
        <p>Loading generated handoff...</p>
      ) : content.trim() ? (
        renderMarkdown(content)
      ) : (
        <p>Generated handoff is empty.</p>
      )}
    </div>
  )
}
