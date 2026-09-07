import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from '@/features/auth/AuthProvider'

/**
 * Gate for every route inside the layout. An unauthenticated visit is redirected to `/login`,
 * carrying the attempted path so sign-in can return there. A 401 from the API clears the session
 * (see `api/client.ts`), which re-renders this guard and sends the user back to sign in.
 */
export function RequireAuth() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }
  return <Outlet />
}
