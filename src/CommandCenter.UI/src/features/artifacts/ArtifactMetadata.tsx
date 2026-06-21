import type { Artifact } from '../../types'

type ArtifactMetadataProps = {
  artifact: Artifact
}

export function ArtifactMetadata({ artifact }: ArtifactMetadataProps) {
  return (
    <div>
      <p className="eyebrow">{artifact.family}</p>
      <h4>{artifact.name}</h4>
      <span>{artifact.relativePath}</span>
    </div>
  )
}
