import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

export interface DefinitionItem {
  label: ReactNode
  value: ReactNode
  /** Span both columns. */
  wide?: boolean
}

export function DefinitionList({ items, className, cols = 2 }: { items: DefinitionItem[]; className?: string; cols?: 2 | 3 }) {
  return (
    <dl className={cn('dl', cols === 3 && 'lg:grid-cols-3', className)}>
      {items.map((item, i) => (
        <div key={i} className={cn(item.wide && 'sm:col-span-2', cols === 3 && item.wide && 'lg:col-span-3')}>
          <dt>{item.label}</dt>
          <dd>{item.value ?? '—'}</dd>
        </div>
      ))}
    </dl>
  )
}
