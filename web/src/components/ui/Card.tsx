import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/cn'

export interface CardProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  title?: ReactNode
  description?: ReactNode
  actions?: ReactNode
  /** Remove the body padding (for tables that fill the card). */
  flush?: boolean
  footer?: ReactNode
}

/** Primary content surface. Title renders as an `<h2>` so screen readers get a page outline. */
export function Card({ title, description, actions, flush, footer, className, children, ...rest }: CardProps) {
  const hasHeader = title || description || actions
  return (
    <section className={cn('card flex flex-col', className)} {...rest}>
      {hasHeader && (
        <header className="flex flex-wrap items-start justify-between gap-3 border-b border-border px-5 py-3.5">
          <div className="min-w-0">
            {title && <h2 className="text-base font-semibold text-fg">{title}</h2>}
            {description && <p className="mt-0.5 text-sm text-fg-muted">{description}</p>}
          </div>
          {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
        </header>
      )}
      <div className={cn('flex-1', flush ? '' : 'p-5')}>{children}</div>
      {footer && <footer className="border-t border-border bg-surface-muted px-5 py-3 text-sm">{footer}</footer>}
    </section>
  )
}
