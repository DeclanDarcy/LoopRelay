import type { ExecutionContextArtifactDiagnostic } from '../../types'

type ExecutionContextArtifactDiagnosticsListProps = {
  diagnostics: ExecutionContextArtifactDiagnostic[]
}

export function ExecutionContextArtifactDiagnosticsList({
  diagnostics,
}: ExecutionContextArtifactDiagnosticsListProps) {
  return (
    <div className="diagnostic-list">
      {diagnostics.map((diagnostic) => {
        const sizeStatus = diagnostic.hardLimitExceeded
          ? ' / hard limit'
          : diagnostic.warningThresholdExceeded
            ? ' / warning'
            : ''

        return (
          <div className="diagnostic-item" key={diagnostic.relativePath}>
            <span>
              {diagnostic.relativePath}: {diagnostic.byteCount} bytes{sizeStatus}
            </span>
            <small>
              {diagnostic.characterCount} characters / warning{' '}
              {diagnostic.warningThresholdBytes} bytes / hard {diagnostic.hardLimitBytes} bytes
            </small>
          </div>
        )
      })}
    </div>
  )
}
