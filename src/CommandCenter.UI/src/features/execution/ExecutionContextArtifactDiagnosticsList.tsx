import type { ExecutionContextArtifactDiagnostic } from '../../types'

type ExecutionContextArtifactDiagnosticsListProps = {
  diagnostics: ExecutionContextArtifactDiagnostic[]
}

export function ExecutionContextArtifactDiagnosticsList({
  diagnostics,
}: ExecutionContextArtifactDiagnosticsListProps) {
  return (
    <div className="diagnostic-list">
      {diagnostics.map((diagnostic) => (
        <span key={diagnostic.relativePath}>
          {diagnostic.relativePath}: {diagnostic.byteCount} bytes
          {diagnostic.hardLimitExceeded
            ? ' / hard limit'
            : diagnostic.warningThresholdExceeded
              ? ' / warning'
              : ''}
        </span>
      ))}
    </div>
  )
}
