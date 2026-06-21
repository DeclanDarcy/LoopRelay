import type { HTMLAttributes, ReactNode } from 'react'

type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'done'

type BadgeProps = HTMLAttributes<HTMLSpanElement> & {
  tone?: BadgeTone
  children: ReactNode
}

export function Badge({ tone = 'neutral', className, children, ...props }: BadgeProps) {
  return (
    <span className={['cc-badge', `cc-badge-${tone}`, className].filter(Boolean).join(' ')} {...props}>
      {children}
    </span>
  )
}
