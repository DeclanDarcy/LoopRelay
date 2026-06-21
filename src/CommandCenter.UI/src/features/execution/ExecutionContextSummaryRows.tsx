import type { ExecutionContextPreview } from '../../types'

type ExecutionContextSummaryRowsProps = {
  executionContext: ExecutionContextPreview
  operationalContextStatus: string
  launchStatus: string
  sizeStatus: string
}

export function ExecutionContextSummaryRows({
  executionContext,
  operationalContextStatus,
  launchStatus,
  sizeStatus,
}: ExecutionContextSummaryRowsProps) {
  return (
    <div className="context-summary">
      <span>Generated: {new Date(executionContext.generatedAt).toLocaleString()}</span>
      <span>Total: {executionContext.diagnostics.totalBytes} bytes</span>
      <span>Characters: {executionContext.diagnostics.totalCharacters}</span>
      <span>Warning threshold: {executionContext.diagnostics.warningThresholdBytes} bytes</span>
      <span>Hard threshold: {executionContext.diagnostics.hardLimitBytes} bytes</span>
      <span>Operational context: {operationalContextStatus}</span>
      <span>Launch: {launchStatus}</span>
      <span>Size: {sizeStatus}</span>
    </div>
  )
}
