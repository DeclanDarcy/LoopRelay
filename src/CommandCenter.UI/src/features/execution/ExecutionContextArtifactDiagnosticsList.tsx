import type { ExecutionContextArtifactDiagnostic } from '../../types'
import { DiagnosticList } from '../../components/explainability'
import { executionArtifactDiagnosticsToExplanation } from '../../lib/explainability'

type ExecutionContextArtifactDiagnosticsListProps = {
  diagnostics: ExecutionContextArtifactDiagnostic[]
}

export function ExecutionContextArtifactDiagnosticsList({
  diagnostics,
}: ExecutionContextArtifactDiagnosticsListProps) {
  return (
    <div className="diagnostic-list">
      <DiagnosticList
        diagnostics={executionArtifactDiagnosticsToExplanation(diagnostics)}
        title="Artifact Diagnostics"
        emptyLabel="No artifact diagnostics recorded."
      />
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
