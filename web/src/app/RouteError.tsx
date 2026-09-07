import { Link, isRouteErrorResponse, useRouteError } from 'react-router'
import { AlertTriangle, Compass, RotateCcw } from 'lucide-react'
import { isApiError } from '@/api/client'
import { Button, ButtonLink } from '@/components/ui/Button'

/**
 * Route-level error boundary. Renders inside the layout so the user keeps their navigation, and
 * shows the API's problem-details title/detail when the failure came from a loader or action.
 */
export function RouteError() {
  const error = useRouteError()

  let title = 'Something went wrong'
  let detail = 'The page could not be displayed. Try again, or go back to the dashboard.'
  let status: number | undefined

  if (isRouteErrorResponse(error)) {
    status = error.status
    title = error.status === 404 ? 'Page not found' : `Request failed (${error.status})`
    detail = typeof error.data === 'string' ? error.data : error.statusText
  } else if (isApiError(error)) {
    status = error.status
    title = error.title
    detail = error.detail ?? detail
  } else if (error instanceof Error) {
    detail = error.message
  }

  return (
    <div className="mx-auto max-w-xl py-12 text-center">
      <span className="mx-auto mb-4 flex size-12 items-center justify-center rounded-full bg-danger-50 text-danger-600">
        <AlertTriangle className="size-6" aria-hidden="true" />
      </span>
      <h1 className="text-xl font-semibold tracking-tight text-fg">{title}</h1>
      {status !== undefined && <p className="mt-1 text-xs font-medium text-fg-subtle">Status {status}</p>}
      <p className="mt-2 text-sm text-fg-muted">{detail}</p>
      <div className="mt-6 flex justify-center gap-2">
        <Button variant="outline" icon={<RotateCcw />} onClick={() => window.location.reload()}>
          Reload
        </Button>
        <ButtonLink to="/" icon={<Compass />}>
          Go to dashboard
        </ButtonLink>
      </div>
    </div>
  )
}

export function NotFound() {
  return (
    <div className="mx-auto max-w-xl py-16 text-center">
      <p className="text-5xl font-semibold tracking-tight text-primary-200">404</p>
      <h1 className="mt-3 text-xl font-semibold tracking-tight text-fg">Page not found</h1>
      <p className="mt-2 text-sm text-fg-muted">
        The address you followed does not exist in SwitchPoint. It may have been renamed or the
        analysis may have been deleted.
      </p>
      <div className="mt-6 flex justify-center gap-2">
        <ButtonLink to="/" icon={<Compass />}>
          Go to dashboard
        </ButtonLink>
        <Link
          to="/clients"
          className="inline-flex h-9 items-center rounded-md px-3.5 text-sm font-medium text-fg-muted hover:bg-surface-sunken hover:text-fg"
        >
          Browse clients
        </Link>
      </div>
    </div>
  )
}
