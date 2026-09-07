import type { ReactNode } from 'react'
import { AlertTriangle, CheckCircle2, Info, XCircle } from 'lucide-react'
import { cn } from '@/lib/cn'

export type AlertTone = 'info' | 'success' | 'warning' | 'danger'

const toneClasses: Record<AlertTone, string> = {
  info: 'border-sky-200 bg-sky-50 text-sky-900 [&_svg]:text-sky-600',
  success: 'border-success-100 bg-success-50 text-success-700 [&_svg]:text-success-600',
  warning: 'border-warning-100 bg-warning-50 text-warning-700 [&_svg]:text-warning-600',
  danger: 'border-danger-100 bg-danger-50 text-danger-700 [&_svg]:text-danger-600',
}

const icons: Record<AlertTone, ReactNode> = {
  info: <Info />,
  success: <CheckCircle2 />,
  warning: <AlertTriangle />,
  danger: <XCircle />,
}

export interface AlertProps {
  tone?: AlertTone
  title?: ReactNode
  children?: ReactNode
  className?: string
  actions?: ReactNode
}

export function Alert({ tone = 'info', title, children, className, actions }: AlertProps) {
  return (
    <div
      role={tone === 'danger' || tone === 'warning' ? 'alert' : 'status'}
      className={cn('flex gap-3 rounded-lg border px-4 py-3 text-sm', toneClasses[tone], className)}
    >
      <span className="mt-0.5 shrink-0 [&>svg]:size-4" aria-hidden="true">
        {icons[tone]}
      </span>
      <div className="min-w-0 flex-1">
        {title && <p className="font-semibold">{title}</p>}
        {children && <div className={cn(title && 'mt-0.5')}>{children}</div>}
        {actions && <div className="mt-2 flex gap-2">{actions}</div>}
      </div>
    </div>
  )
}

/** Renders a list of warnings from the calculation engine, or nothing. */
export function WarningsAlert({ warnings, title = 'Warnings' }: { warnings: string[] | undefined; title?: string }) {
  if (!warnings || warnings.length === 0) return null
  return (
    <Alert tone="warning" title={title}>
      <ul className="list-disc space-y-0.5 pl-4">
        {warnings.map((w, i) => (
          <li key={i}>{w}</li>
        ))}
      </ul>
    </Alert>
  )
}
