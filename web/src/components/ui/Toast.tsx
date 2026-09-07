import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
import { CheckCircle2, Info, X, XCircle, AlertTriangle } from 'lucide-react'
import { cn } from '@/lib/cn'

export type ToastKind = 'info' | 'success' | 'error' | 'warning'

export interface ToastItem {
  id: number
  kind: ToastKind
  title: string
  description?: string
}

export interface ToastApi {
  push: (toast: Omit<ToastItem, 'id'>) => void
  success: (title: string, description?: string) => void
  error: (title: string, description?: string) => void
  info: (title: string, description?: string) => void
  warning: (title: string, description?: string) => void
  dismiss: (id: number) => void
}

const ToastContext = createContext<ToastApi | null>(null)

const DURATION_MS = 5000

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([])
  const nextId = useRef(1)

  const dismiss = useCallback((id: number) => setItems((prev) => prev.filter((t) => t.id !== id)), [])

  const push = useCallback(
    (toast: Omit<ToastItem, 'id'>) => {
      const id = nextId.current++
      setItems((prev) => [...prev.slice(-4), { ...toast, id }])
      setTimeout(() => dismiss(id), DURATION_MS)
    },
    [dismiss],
  )

  const api = useMemo<ToastApi>(
    () => ({
      push,
      dismiss,
      success: (title, description) => push({ kind: 'success', title, description }),
      error: (title, description) => push({ kind: 'error', title, description }),
      info: (title, description) => push({ kind: 'info', title, description }),
      warning: (title, description) => push({ kind: 'warning', title, description }),
    }),
    [push, dismiss],
  )

  return (
    <ToastContext.Provider value={api}>
      {children}
      <ToastViewport items={items} onDismiss={dismiss} />
    </ToastContext.Provider>
  )
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used within <ToastProvider>')
  return ctx
}

const kindClasses: Record<ToastKind, string> = {
  info: 'border-sky-200 [&_svg.icon]:text-sky-600',
  success: 'border-success-100 [&_svg.icon]:text-success-600',
  error: 'border-danger-100 [&_svg.icon]:text-danger-600',
  warning: 'border-warning-100 [&_svg.icon]:text-warning-600',
}

const kindIcon: Record<ToastKind, ReactNode> = {
  info: <Info className="icon size-5" />,
  success: <CheckCircle2 className="icon size-5" />,
  error: <XCircle className="icon size-5" />,
  warning: <AlertTriangle className="icon size-5" />,
}

function ToastViewport({ items, onDismiss }: { items: ToastItem[]; onDismiss: (id: number) => void }) {
  return (
    <div
      data-toast-viewport
      role="status"
      aria-live="polite"
      className="pointer-events-none fixed right-4 bottom-4 z-[90] flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2"
    >
      {items.map((t) => (
        <div
          key={t.id}
          className={cn(
            'pointer-events-auto flex items-start gap-3 rounded-lg border bg-surface p-3 shadow-raised',
            kindClasses[t.kind],
          )}
        >
          <span aria-hidden="true">{kindIcon[t.kind]}</span>
          <div className="min-w-0 flex-1 text-sm">
            <p className="font-semibold text-fg">{t.title}</p>
            {t.description && <p className="mt-0.5 text-fg-muted">{t.description}</p>}
          </div>
          <button
            type="button"
            onClick={() => onDismiss(t.id)}
            aria-label="Dismiss notification"
            className="rounded p-0.5 text-fg-subtle hover:bg-surface-sunken hover:text-fg"
          >
            <X className="size-4" />
          </button>
        </div>
      ))}
    </div>
  )
}
