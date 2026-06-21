import type { HTMLAttributes, ReactNode } from 'react'

type MetricProps = HTMLAttributes<HTMLDivElement> & {
  label: string
  value: ReactNode
}

export function Metric({ label, value, className, ...props }: MetricProps) {
  return (
    <div className={['cc-metric', className].filter(Boolean).join(' ')} {...props}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}
