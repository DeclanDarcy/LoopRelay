import type { BoundaryViolationProjection } from '../types'

type BoundaryNoticeProps = {
  violation: BoundaryViolationProjection
}

export function BoundaryNotice({ violation }: BoundaryNoticeProps) {
  return (
    <article className="boundary-notice" aria-label="Authority boundary notice">
      <div>
        <span>Severity</span>
        <strong>{violation.severity}</strong>
      </div>
      <div>
        <span>Boundary rule</span>
        <p>{violation.boundaryRule}</p>
      </div>
      <div>
        <span>Owning domain</span>
        <strong>{violation.owningDomain}</strong>
      </div>
      <div>
        <span>Rejected assertion</span>
        <p>{violation.rejectedAssertion}</p>
      </div>
      <div>
        <span>Allowed alternative</span>
        <p>{violation.allowedAlternative}</p>
      </div>
      <div>
        <span>Diagnostic detail</span>
        <p>{violation.diagnosticDetail}</p>
      </div>
    </article>
  )
}

