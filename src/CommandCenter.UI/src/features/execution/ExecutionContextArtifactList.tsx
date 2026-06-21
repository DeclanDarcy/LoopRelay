import type { ExecutionContextArtifact } from '../../types'

type ExecutionContextArtifactListProps = {
  artifacts: ExecutionContextArtifact[]
}

export function ExecutionContextArtifactList({ artifacts }: ExecutionContextArtifactListProps) {
  return (
    <ul>
      {artifacts.map((artifact) => (
        <li key={artifact.relativePath}>
          {artifact.role}: {artifact.relativePath} ({artifact.byteCount} bytes)
        </li>
      ))}
    </ul>
  )
}
