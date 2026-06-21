import { renderMarkdown } from '../../lib/markdown'

type OperationalContextProposalComparisonProps = {
  currentContent: string
  proposedContent: string
}

export function OperationalContextProposalComparison({
  currentContent,
  proposedContent,
}: OperationalContextProposalComparisonProps) {
  return (
    <div className="proposal-comparison-grid">
      <div>
        <h5>Current Understanding</h5>
        <div className="markdown-preview context-artifact-content">
          {currentContent.trim()
            ? renderMarkdown(currentContent)
            : <p>No current operational context.</p>}
        </div>
      </div>
      <div>
        <h5>Review Candidate</h5>
        <div className="markdown-preview context-artifact-content">
          {proposedContent.trim()
            ? renderMarkdown(proposedContent)
            : <p>Empty proposal.</p>}
        </div>
      </div>
    </div>
  )
}
