import type { HTMLAttributes, ReactNode } from 'react'

type SectionHeaderProps = HTMLAttributes<HTMLDivElement> & {
  eyebrow?: string
  title: string
  actions?: ReactNode
  headingLevel?: 3 | 4 | 5
}

export function SectionHeader({
  eyebrow,
  title,
  actions,
  headingLevel = 3,
  className,
  ...props
}: SectionHeaderProps) {
  const Heading = `h${headingLevel}` as 'h3' | 'h4' | 'h5'

  return (
    <div className={['cc-section-header', className].filter(Boolean).join(' ')} {...props}>
      <div>
        {eyebrow ? <p className="eyebrow">{eyebrow}</p> : null}
        <Heading>{title}</Heading>
      </div>
      {actions}
    </div>
  )
}
