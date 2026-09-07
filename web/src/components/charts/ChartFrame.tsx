import { useState, type ReactNode } from 'react'
import { Table2, ChartArea } from 'lucide-react'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/Button'

export interface ChartFrameProps {
  title: ReactNode
  /** Sentence describing what the chart shows; also the chart's accessible description. */
  description?: string
  /** The chart itself; must be a ResponsiveContainer. */
  children: ReactNode
  /** Equivalent data as a table, offered behind a toggle for keyboard and screen-reader users. */
  table?: ReactNode
  actions?: ReactNode
  className?: string
  height?: number
}

/**
 * Wrapper every chart on the site sits in. It supplies the accessible name and description
 * (`role="img"` + `aria-label`), and a "Show as table" toggle so no figure is the only way to
 * read a number — the tabular alternative is also what gets printed.
 */
export function ChartFrame({
  title,
  description,
  children,
  table,
  actions,
  className,
  height = 300,
}: ChartFrameProps) {
  const [asTable, setAsTable] = useState(false)
  const label = typeof title === 'string' ? title : 'Chart'

  return (
    <figure className={cn('m-0', className)}>
      <figcaption className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <div className="min-w-0">
          <p className="text-sm font-semibold text-fg">{title}</p>
          {description && <p className="text-xs text-fg-muted">{description}</p>}
        </div>
        <div className="flex items-center gap-2 no-print">
          {actions}
          {table && (
            <Button
              size="sm"
              variant="ghost"
              icon={asTable ? <ChartArea /> : <Table2 />}
              onClick={() => setAsTable((v) => !v)}
              aria-pressed={asTable}
            >
              {asTable ? 'Show as chart' : 'Show as table'}
            </Button>
          )}
        </div>
      </figcaption>

      {table && asTable ? (
        <div className="overflow-x-auto">{table}</div>
      ) : (
        <div
          role="img"
          aria-label={description ? `${label}. ${description}` : label}
          style={{ height }}
          className="w-full"
        >
          {children}
        </div>
      )}

      {/* Printing always gets the numbers, never just a picture. */}
      {table && !asTable && <div className="hidden print:block">{table}</div>}
    </figure>
  )
}

/** Legend row shared by charts that pair an existing and a proposed series. */
export function ChartLegend({ items }: { items: { label: string; color: string; dashed?: boolean }[] }) {
  return (
    <ul className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-fg-muted">
      {items.map((item) => (
        <li key={item.label} className="flex items-center gap-1.5">
          <svg width="18" height="8" aria-hidden="true">
            <line
              x1="0"
              y1="4"
              x2="18"
              y2="4"
              stroke={item.color}
              strokeWidth="2.5"
              strokeDasharray={item.dashed ? '5 3' : undefined}
            />
          </svg>
          {item.label}
        </li>
      ))}
    </ul>
  )
}
