import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { ChevronLeft, ChevronRight, Menu, ShieldCheck, X } from 'lucide-react'
import { cn } from '@/lib/cn'
import { useLocalStorage, useMediaQuery } from '@/lib/hooks'
import { navItems } from './nav'
import { Breadcrumbs } from './Breadcrumbs'
import { TopBar } from './TopBar'

/**
 * Application chrome: a sidebar that collapses to icons on wide screens and becomes a modal
 * drawer below 1024px, a top bar, breadcrumbs, and the routed page in `<main>`.
 *
 * Landmarks are explicit (`banner`, `navigation`, `main`) and a skip link jumps straight to the
 * page content, which is the first thing a keyboard user reaches.
 */
export function AppLayout() {
  const isDesktop = useMediaQuery('(min-width: 1024px)')
  const [collapsed, setCollapsed] = useLocalStorage('switchpoint.sidebar.collapsed', false)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const location = useLocation()
  const mainRef = useRef<HTMLElement>(null)

  // Route changes close the drawer and reset scroll; the page heading takes focus for AT users.
  useEffect(() => {
    setDrawerOpen(false)
    // jsdom (and very old browsers) have no Element.scrollTo; scrolling is cosmetic here.
    mainRef.current?.scrollTo?.({ top: 0 })
  }, [location.pathname])

  useEffect(() => {
    if (!drawerOpen) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setDrawerOpen(false)
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [drawerOpen])

  const showRail = isDesktop && collapsed

  return (
    <div className="flex min-h-screen bg-surface-sunken">
      <a href="#main-content" className="skip-link">
        Skip to main content
      </a>

      {/* Drawer scrim below 1024px */}
      {drawerOpen && !isDesktop && (
        <div
          className="fixed inset-0 z-40 bg-primary-950/50 lg:hidden"
          onClick={() => setDrawerOpen(false)}
          aria-hidden="true"
        />
      )}

      <aside
        data-sidebar
        aria-label="Sections"
        className={cn(
          'z-50 flex shrink-0 flex-col bg-sidebar text-sidebar-fg transition-[width] duration-150',
          isDesktop
            ? cn('sticky top-0 h-screen', showRail ? 'w-16' : 'w-60')
            : cn(
                'fixed inset-y-0 left-0 w-64 transform shadow-raised transition-transform duration-200',
                drawerOpen ? 'translate-x-0' : '-translate-x-full',
              ),
        )}
      >
        <div className={cn('flex h-14 items-center gap-2.5 px-4', showRail && 'justify-center px-0')}>
          <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-white/10">
            <ShieldCheck className="size-4.5" aria-hidden="true" />
          </span>
          {!showRail && <span className="text-base font-semibold tracking-tight text-white">SwitchPoint</span>}
          {!isDesktop && (
            <button
              type="button"
              onClick={() => setDrawerOpen(false)}
              aria-label="Close menu"
              className="ml-auto rounded-md p-1.5 hover:bg-sidebar-hover"
            >
              <X className="size-5" />
            </button>
          )}
        </div>

        <nav aria-label="Primary" className="flex-1 overflow-y-auto px-2 py-2">
          <ul className="space-y-0.5">
            {navItems.map((item) => {
              const Icon = item.icon
              return (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.end}
                    title={showRail ? item.label : undefined}
                    className={({ isActive }) =>
                      cn(
                        'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                        showRail && 'justify-center px-0',
                        isActive
                          ? 'bg-sidebar-active text-white'
                          : 'text-sidebar-fg hover:bg-sidebar-hover hover:text-white',
                      )
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <Icon className="size-[18px] shrink-0" aria-hidden="true" />
                        {!showRail && <span className="truncate">{item.label}</span>}
                        {showRail && <span className="sr-only">{item.label}</span>}
                        {isActive && <span className="sr-only">(current page)</span>}
                      </>
                    )}
                  </NavLink>
                </li>
              )
            })}
          </ul>
        </nav>

        {isDesktop && (
          <div className="border-t border-white/10 p-2">
            <button
              type="button"
              onClick={() => setCollapsed(!collapsed)}
              aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
              aria-expanded={!collapsed}
              className={cn(
                'flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-sidebar-fg hover:bg-sidebar-hover hover:text-white',
                showRail && 'justify-center px-0',
              )}
            >
              {collapsed ? (
                <ChevronRight className="size-[18px]" aria-hidden="true" />
              ) : (
                <ChevronLeft className="size-[18px]" aria-hidden="true" />
              )}
              {!showRail && <span>Collapse</span>}
            </button>
          </div>
        )}
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar
          menuButton={
            !isDesktop ? (
              <button
                type="button"
                onClick={() => setDrawerOpen(true)}
                aria-label="Open menu"
                aria-expanded={drawerOpen}
                className="rounded-md p-2 text-fg-muted hover:bg-surface-sunken hover:text-fg lg:hidden"
              >
                <Menu className="size-5" />
              </button>
            ) : null
          }
        />
        <main
          ref={mainRef}
          id="main-content"
          tabIndex={-1}
          className="min-w-0 flex-1 px-4 pt-4 pb-10 focus:outline-none sm:px-6 lg:px-8"
        >
          <Breadcrumbs />
          <Outlet />
        </main>
      </div>
    </div>
  )
}
