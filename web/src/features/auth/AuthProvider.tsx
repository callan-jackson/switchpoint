import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useSyncExternalStore,
  type ReactNode,
} from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { auth } from '@/api/endpoints'
import type { LoginRequest, UserDto, UserRole } from '@/api/types'
import { clearSession, getSession, setSession, subscribe, type AuthSession } from './authStore'

export interface AuthContextValue {
  session: AuthSession | null
  user: UserDto | null
  isAuthenticated: boolean
  signIn: (credentials: LoginRequest) => Promise<UserDto>
  signOut: () => void
  /** True when the signed-in user holds any of the given roles. */
  hasRole: (...roles: UserRole[]) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

/**
 * Holds the session in memory (and mirrors it to `sessionStorage` for the tab's life) via the
 * framework-free store in `authStore.ts`, which is also where `api/client.ts` reads the bearer
 * token. Signing out clears the React Query cache so one adviser's data never survives into
 * another's session.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const session = useSyncExternalStore(subscribe, getSession, getSession)

  const signIn = useCallback(
    async (credentials: LoginRequest) => {
      const response = await auth.login(credentials)
      setSession({
        accessToken: response.accessToken,
        expiresAtUtc: response.expiresAtUtc,
        user: response.user,
      })
      queryClient.clear()
      return response.user
    },
    [queryClient],
  )

  const signOut = useCallback(() => {
    clearSession()
    queryClient.clear()
  }, [queryClient])

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      user: session?.user ?? null,
      isAuthenticated: session !== null,
      signIn,
      signOut,
      hasRole: (...roles) => (session ? roles.includes(session.user.role) : false),
    }),
    [session, signIn, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within <AuthProvider>')
  return ctx
}
