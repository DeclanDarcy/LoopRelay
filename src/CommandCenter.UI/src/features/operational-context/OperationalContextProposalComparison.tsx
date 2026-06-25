import { EvidenceList } from '../../components/explainability'
import { operationalContextSemanticChangeSupportingEvidenceToEvidence } from '../../lib/explainability'
import { renderMarkdown } from '../../lib/markdown'
import type { OperationalContextSemanticChange } from '../../types'

type OperationalContextProposalComparisonProps = {
  currentContent: string
  proposedContent: string
  semanticChanges?: OperationalContextSemanticChange[]
}

export function OperationalContextProposalComparison({
  currentContent,
  proposedContent,
  semanticChanges = [],
}: OperationalContextProposalComparisonProps) {
  const modifications = semanticChanges.filter(isModificationChange)

  return (
    <div className="proposal-comparison">
      {modifications.length > 0 ? (
        <section className="proposal-modification-summary" aria-label="Proposal modifications">
          <h5>Modification Review</h5>
          <ul>
            {modifications.map((change, index) => (
              <li key={`${change.type}-${change.itemId ?? index}`}>
                <strong>
                  {change.type}: {change.description}
                </strong>
                <dl className="proposal-modification-states">
                  {change.section ? (
                    <div>
                      <dt>Section</dt>
                      <dd>{change.section}</dd>
                    </div>
                  ) : null}
                  {change.previousState ? (
                    <div>
                      <dt>Previous</dt>
                      <dd>{change.previousState}</dd>
                    </div>
                  ) : null}
                  {change.currentState ? (
                    <div>
                      <dt>Current</dt>
                      <dd>{change.currentState}</dd>
                    </div>
                  ) : null}
                  {change.modificationReason ? (
                    <div>
                      <dt>Reason</dt>
                      <dd>{change.modificationReason}</dd>
                    </div>
                  ) : null}
                  {change.identityBasis ? (
                    <div>
                      <dt>Identity basis</dt>
                      <dd>{change.identityBasis}</dd>
                    </div>
                  ) : null}
                </dl>
                {change.supportingEvidence.length > 0 ? (
                  <EvidenceList
                    evidence={operationalContextSemanticChangeSupportingEvidenceToEvidence(change)}
                    title={`Supporting evidence for ${change.type}`}
                  />
                ) : null}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
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
    </div>
  )
}

function isModificationChange(change: OperationalContextSemanticChange) {
  return (
    change.type.includes('Modified') ||
    change.type.includes('Changed') ||
    change.previousState !== null ||
    change.currentState !== null
  )
}
