import { renderMarkdown } from '../../lib'

type ArtifactMarkdownPreviewProps = {
  content: string
  isLoading: boolean
}

export function ArtifactMarkdownPreview({ content, isLoading }: ArtifactMarkdownPreviewProps) {
  return (
    <div className="markdown-preview">
      {isLoading ? (
        <p>Loading artifact...</p>
      ) : content.trim() ? (
        renderMarkdown(content)
      ) : (
        <p>Empty artifact.</p>
      )}
    </div>
  )
}
