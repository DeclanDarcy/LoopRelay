import { Button } from '../../components/design'

type PlanFailureNoticeProps = {
  reason: string
  detail: string | null
  onDismiss: () => void
}

export function PlanFailureNotice({ reason, detail, onDismiss }: PlanFailureNoticeProps) {
  return (
    <section className="cc-plan-failure" role="alert" aria-label="Planning failed">
      <div className="cc-plan-failure-body">
        <span className="cc-plan-eyebrow cc-plan-failure-eyebrow">Planning failed</span>
        <p className="cc-plan-failure-reason">{reason}</p>
        {detail ? <pre className="cc-plan-failure-detail">{detail}</pre> : null}
      </div>
      <Button type="button" variant="secondary" onClick={onDismiss}>
        Back to authoring
      </Button>
    </section>
  )
}
