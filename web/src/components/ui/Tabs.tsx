import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react'
import { cn } from '@/lib/cn'

export interface TabItem<TId extends string = string> {
  id: TId
  label: ReactNode
  badge?: ReactNode
  disabled?: boolean
}

export interface TabsProps<TId extends string> {
  items: TabItem<TId>[]
  value: TId
  onChange: (id: TId) => void
  /** Renders the active panel. */
  children?: ReactNode
  className?: string
  variant?: 'underline' | 'pills'
  'aria-label'?: string
}

/** WAI-ARIA tabs with roving focus (arrow keys, Home/End). */
export function Tabs<TId extends string>({
  items,
  value,
  onChange,
  children,
  className,
  variant = 'underline',
  'aria-label': ariaLabel,
}: TabsProps<TId>) {
  const baseId = useId()
  const listRef = useRef<HTMLDivElement>(null)

  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    const enabled = items.filter((t) => !t.disabled)
    const index = enabled.findIndex((t) => t.id === value)
    if (index < 0) return
    let next = index
    if (e.key === 'ArrowRight') next = (index + 1) % enabled.length
    else if (e.key === 'ArrowLeft') next = (index - 1 + enabled.length) % enabled.length
    else if (e.key === 'Home') next = 0
    else if (e.key === 'End') next = enabled.length - 1
    else return
    e.preventDefault()
    const target = enabled[next]
    onChange(target.id)
    const btn = listRef.current?.querySelector<HTMLButtonElement>(`[data-tab-id="${target.id}"]`)
    btn?.focus()
  }

  return (
    <div className={className}>
      <div
        ref={listRef}
        role="tablist"
        aria-label={ariaLabel}
        onKeyDown={onKeyDown}
        className={cn(
          'flex gap-1 overflow-x-auto',
          variant === 'underline' ? 'border-b border-border' : 'rounded-lg bg-surface-sunken p-1',
        )}
      >
        {items.map((item) => {
          const selected = item.id === value
          return (
            <button
              key={item.id}
              type="button"
              role="tab"
              id={`${baseId}-tab-${item.id}`}
              data-tab-id={item.id}
              aria-selected={selected}
              aria-controls={`${baseId}-panel-${item.id}`}
              tabIndex={selected ? 0 : -1}
              disabled={item.disabled}
              onClick={() => onChange(item.id)}
              className={cn(
                'inline-flex items-center gap-2 whitespace-nowrap text-sm font-medium transition-colors disabled:opacity-50',
                variant === 'underline'
                  ? cn(
                      '-mb-px border-b-2 px-3 py-2.5',
                      selected
                        ? 'border-accent-500 text-primary-700'
                        : 'border-transparent text-fg-muted hover:border-border-strong hover:text-fg',
                    )
                  : cn(
                      'rounded-md px-3 py-1.5',
                      selected ? 'bg-surface text-primary-700 shadow-card' : 'text-fg-muted hover:text-fg',
                    ),
              )}
            >
              {item.label}
              {item.badge !== undefined && (
                <span className="rounded-full bg-surface-sunken px-1.5 text-xs text-fg-muted">{item.badge}</span>
              )}
            </button>
          )
        })}
      </div>
      <div
        role="tabpanel"
        id={`${baseId}-panel-${value}`}
        aria-labelledby={`${baseId}-tab-${value}`}
        tabIndex={0}
        className="pt-4 focus:outline-none"
      >
        {children}
      </div>
    </div>
  )
}
