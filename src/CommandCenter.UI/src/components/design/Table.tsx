import type { TableHTMLAttributes } from 'react'

export function Table({ className, ...props }: TableHTMLAttributes<HTMLTableElement>) {
  return <table className={['cc-table', className].filter(Boolean).join(' ')} {...props} />
}
