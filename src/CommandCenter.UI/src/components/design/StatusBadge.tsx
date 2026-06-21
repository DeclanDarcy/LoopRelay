import type { HTMLAttributes } from 'react'

import { Badge } from './Badge'
import type { StatusPresentation } from '../../lib/status'

type StatusBadgeProps = HTMLAttributes<HTMLSpanElement> & {
  status: StatusPresentation
}

export function StatusBadge({ status, ...props }: StatusBadgeProps) {
  return (
    <Badge tone={status.tone} {...props}>
      {status.label}
    </Badge>
  )
}
