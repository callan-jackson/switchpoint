import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { ChevronDown, LogOut, Settings as SettingsIcon } from 'lucide-react'
import { cn } from '@/lib/cn'
import { initials } from '@/lib/format'
import { userRoleLabels } from '@/lib/labels'
import { Badge } from '@/components/ui/Badge'
import { useAuth } from '@/features/auth/AuthProvider'

const envLabel = import.meta.env.VITE_ENV_LABEL ?? 'Development'
const usingMocks = import.meta.env.VITE_USE_MOCKS === 'true'

function envTone(label: string) {
  const l = label.toLowerCase()
  if (l.startsWith('prod')) return 'danger' as const
  if (l.startsWith('uat') || l.startsWith('stag')) return 'warning' as const
  return 'accent' as const
}

/** Firm name, environment badge and the user menu. Sticky so it stays put while a page scrolls. */
export function TopBar({ menuButton }: { menuButton?: ReactNode }) {
  const { user, signOut } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      if (!menuRef.current?.contains(e.target as Node)) setOpen(false)
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  return (
    <header
      data-topbar
      className="sticky top-0 z-30 flex h-14 shrink-0 items-center gap-3 border-b border-border bg-surface px-4 sm:px-6 lg:px-8"
    >
      {menuButton}

      <div className="min-w-0">
        <p className="truncate text-sm font-semibold text-fg">{user?.firmName ?? 'SwitchPoint'}</p>
      </div>

      <Badge tone={envTone(envLabel)} size="sm" className="hidden sm:inline-flex">
        {envLabel}
      </Badge>
      {usingMocks && (
        <Badge tone="warning" size="sm" title="Serving sample data from the in-browser mock server">
          Mock data
        </Badge>
      )}

      <div className="ml-auto" ref={menuRef}>
        <div className="relative">
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            aria-haspopup="menu"
            aria-expanded={open}
            className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-surface-sunken"
          >
            <span className="flex size-7 items-center justify-center rounded-full bg-primary-700 text-xs font-semibold text-white">
              {initials(user?.displayName ?? '?')}
            </span>
            <span className="hidden min-w-0 text-left sm:block">
              <span className="block truncate text-sm font-medium text-fg">{user?.displayName}</span>
              <span className="block truncate text-xs text-fg-subtle">
                {user ? userRoleLabels[user.role] : ''}
              </span>
            </span>
            <ChevronDown className={cn('size-4 text-fg-subtle transition-transform', open && 'rotate-180')} aria-hidden="true" />
          </button>

          {open && (
            <div
              role="menu"
              aria-label="Account"
              className="absolute right-0 z-40 mt-1 w-60 rounded-lg border border-border bg-surface p-1 shadow-raised"
            >
              <div className="border-b border-border px-3 py-2">
                <p className="truncate text-sm font-medium text-fg">{user?.displayName}</p>
                <p className="truncate text-xs text-fg-subtle">{user?.email}</p>
              </div>
              <button
                type="button"
                role="menuitem"
                onClick={() => {
                  setOpen(false)
                  navigate('/settings')
                }}
                className="flex w-full items-center gap-2 rounded-md px-3 py-2 text-left text-sm text-fg hover:bg-surface-sunken"
              >
                <SettingsIcon className="size-4" aria-hidden="true" />
                Settings
              </button>
              <button
                type="button"
                role="menuitem"
                onClick={() => {
                  setOpen(false)
                  signOut()
                  navigate('/login', { replace: true })
                }}
                className="flex w-full items-center gap-2 rounded-md px-3 py-2 text-left text-sm text-danger-600 hover:bg-danger-50"
              >
                <LogOut className="size-4" aria-hidden="true" />
                Sign out
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
