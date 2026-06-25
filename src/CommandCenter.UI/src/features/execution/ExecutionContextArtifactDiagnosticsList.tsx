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
    </div>
  )
}
