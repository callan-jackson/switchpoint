import { useEffect, useId, useRef, type ReactNode, type SyntheticEvent } from 'react'
import { X } from 'lucide-react'
import { cn } from '@/lib/cn'

export interface DialogProps {
  open: boolean
  onClose: () => void
  title: ReactNode
  description?: ReactNode
  children: ReactNode
  footer?: ReactNode
  size?: 'sm' | 'md' | 'lg' | 'xl'
  /** Prevent closing via Escape / backdrop (e.g. while saving). */
  locked?: boolean
}

const sizeClasses = { sm: 'max-w-md', md: 'max-w-lg', lg: 'max-w-3xl', xl: 'max-w-5xl' }

/**
 * Modal built on the native `<dialog>` element: focus trapping, Escape handling, inertness of the
 * page behind and `::backdrop` styling all come from the platform.
 */
export function Dialog({ open, onClose, title, description, children, footer, size = 'md', locked }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = `dlg-${useId()}`

  useEffect(() => {
    const el = ref.current
    if (!el) return
    if (open && !el.open) el.showModal()
    else if (!open && el.open) el.close()
  }, [open])

  const handleCancel = (e: SyntheticEvent<HTMLDialogElement>) => {
    e.preventDefault()
    if (!locked) onClose()
  }

  const handleBackdropClick = (e: React.MouseEvent<HTMLDialogElement>) => {
    if (locked || e.target !== ref.current) return
    onClose()
  }

  if (!open) return null

  return (
    <dialog
      ref={ref}
      className={cn('dialog', sizeClasses[size])}
      aria-labelledby={titleId}
      onCancel={handleCancel}
      onClick={handleBackdropClick}
    >
      <div className="flex max-h-[85vh] flex-col">
        <header className="flex items-start justify-between gap-4 border-b border-border px-5 py-4">
          <div>
            <h2 id={titleId} className="text-base font-semibold text-fg">
              {title}
            </h2>
            {description && <p className="mt-0.5 text-sm text-fg-muted">{description}</p>}
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={locked}
            aria-label="Close dialog"
            className="rounded-md p-1 text-fg-subtle hover:bg-surface-sunken hover:text-fg"
          >
            <X className="size-5" />
          </button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">{children}</div>
        {footer && <footer className="flex flex-wrap items-center justify-end gap-2 border-t border-border bg-surface-muted px-5 py-3">{footer}</footer>}
      </div>
    </dialog>
  )
}
