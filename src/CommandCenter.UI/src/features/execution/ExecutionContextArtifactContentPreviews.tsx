import { renderMarkdown } from '../../lib'
import type { ExecutionContextArtifact } from '../../types'

type ExecutionContextArtifactContentPreviewsProps = {
  artifacts: ExecutionContextArtifact[]
}

export function ExecutionContextArtifactContentPreviews({
  artifacts,
}: ExecutionContextArtifactContentPreviewsProps) {
  return (
    <div className="context-artifact-previews">
      <h5>Artifact Content</h5>
      {artifacts.map((artifact) => (
        <details key={artifact.relativePath} open={artifact.role === 'OperationalContext'}>
          <summary>
            {artifact.role}: {artifact.relativePath} ({artifact.characterCount} characters)
          </summary>
          <div className="markdown-preview context-artifact-content">
            {artifact.content.trim() ? renderMarkdown(artifact.content) : <p>Empty artifact.</p>}
          </div>
        </details>
      ))}
    </div>
  )
}
