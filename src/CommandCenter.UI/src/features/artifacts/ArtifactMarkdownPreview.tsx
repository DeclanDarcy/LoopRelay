import { memo, useMemo } from 'react'

import { renderMarkdown } from '../../lib'

type ArtifactMarkdownPreviewProps = {
  content: string
  isLoading: boolean
}

export const ArtifactMarkdownPreview = memo(function ArtifactMarkdownPreview({
  content,
  isLoading,
}: ArtifactMarkdownPreviewProps) {
  const rendered = useMemo(() => renderMarkdown(content), [content])

  return (
    <div className="markdown-preview">
      {isLoading ? (
        <p>Loading artifact...</p>
      ) : content.trim() ? (
        rendered
      ) : (
        <p>Empty artifact.</p>
      )}
    </div>
  )
})
