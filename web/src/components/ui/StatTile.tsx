import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { Skeleton } from './Skeleton'

export type StatTone = 'neutral' | 'primary' | 'accent' | 'success' | 'warning' | 'danger'

const toneText: Record<StatTone, string> = {
  neutral: 'text-fg',
  primary: 'text-primary-700',
  accent: 'text-accent-600',
  success: 'text-success-600',
  warning: 'text-warning-600',
  danger: 'text-danger-600',
}

export interface StatTileProps {
  label: ReactNode
  value: ReactNode
  hint?: ReactNode
  icon?: ReactNode
  tone?: StatTone
  loading?: boolean
  className?: string
}

/** Headline figure. Value is rendered with tabular numerals so tiles line up. */
export function StatTile({ label, value, hint, icon, tone = 'neutral', loading, className }: StatTileProps) {
  return (
    <div className={cn('card flex items-start gap-4 p-4', className)}>
      {icon && (
        <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary-50 text-primary-700 [&>svg]:size-5" aria-hidden="true">
          {icon}
        </div>
      )}
      <div className="min-w-0 flex-1">
        <p className="truncate text-xs font-medium uppercase tracking-wide text-fg-subtle">{label}</p>
        {loading ? (
          <Skeleton className="mt-1.5 h-7 w-24" />
        ) : (
          <p className={cn('mt-0.5 text-2xl font-semibold tabular-nums tracking-tight', toneText[tone])}>{value}</p>
        )}
        {hint && <p className="mt-0.5 text-xs text-fg-muted">{hint}</p>}
      </div>
    </div>
  )
}
