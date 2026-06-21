import type { HTMLAttributes, ReactNode } from 'react'

type EmptyStateProps = HTMLAttributes<HTMLParagraphElement> & {
  children: ReactNode
}

export function EmptyState({ className, children, ...props }: EmptyStateProps) {
  return (
    <p className={['cc-empty-state', className].filter(Boolean).join(' ')} {...props}>
      {children}
    </p>
  )
}
