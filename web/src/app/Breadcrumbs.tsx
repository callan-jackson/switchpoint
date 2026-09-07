import { Fragment } from 'react'
import { Link, useLocation, useMatches } from 'react-router'
import { ChevronRight } from 'lucide-react'
import { segmentLabels } from './nav'

/** A route can publish a crumb label through its `handle`. */
export interface RouteHandle {
  crumb?: string
}

function isHandle(value: unknown): value is RouteHandle {
  return !!value && typeof value === 'object' && 'crumb' in value
}

/** Trail from Dashboard to the current page. GUID segments show as the route's own crumb label. */
export function Breadcrumbs() {
  const { pathname } = useLocation()
  const matches = useMatches()

  if (pathname === '/') return null

  const segments = pathname.split('/').filter(Boolean)
  const crumbs = segments.map((segment, index) => {
    const to = `/${segments.slice(0, index + 1).join('/')}`
    const match = matches.find((m) => m.pathname.replace(/\/$/, '') === to)
    const handleCrumb = isHandle(match?.handle) ? match.handle.crumb : undefined
    const isId = /^[0-9a-f]{8}-[0-9a-f]{4}-/i.test(segment)
    const label = handleCrumb ?? segmentLabels[segment] ?? (isId ? 'Details' : segment)
    return { to, label, last: index === segments.length - 1 }
  })

  return (
    <nav aria-label="Breadcrumb" className="mb-3 no-print">
      <ol className="flex flex-wrap items-center gap-1 text-xs text-fg-subtle">
        <li>
          <Link to="/" className="rounded hover:text-fg hover:underline">
            Dashboard
          </Link>
        </li>
        {crumbs.map((crumb) => (
          <Fragment key={crumb.to}>
            <li aria-hidden="true">
              <ChevronRight className="size-3" />
            </li>
            <li>
              {crumb.last ? (
                <span aria-current="page" className="font-medium text-fg-muted">
                  {crumb.label}
                </span>
              ) : (
                <Link to={crumb.to} className="rounded hover:text-fg hover:underline">
                  {crumb.label}
                </Link>
              )}
            </li>
          </Fragment>
        ))}
      </ol>
    </nav>
  )
}
