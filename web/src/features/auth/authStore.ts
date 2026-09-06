import type { UserDto } from '@/api/types'

/**
 * Session store shared by the fetch client and the AuthProvider.
 *
 * Deliberately framework-free: `client.ts` reads the bearer token from here without depending on
 * the component tree, and `AuthProvider` subscribes through `useSyncExternalStore`. The session is
 * mirrored to `sessionStorage` so a page refresh keeps the adviser signed in for the tab's life,
 * but closing the tab drops it (no long-lived tokens in the browser).
 */

export interface AuthSession {
  accessToken: string
  expiresAtUtc: string
  user: UserDto
}

export const SESSION_STORAGE_KEY = 'switchpoint.session'

let session: AuthSession | null | undefined
const listeners = new Set<() => void>()

function readStorage(): AuthSession | null {
  try {
    const raw = globalThis.sessionStorage?.getItem(SESSION_STORAGE_KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (isSession(parsed) && !isExpired(parsed)) return parsed
  } catch {
    // Corrupt or unavailable storage: treat as signed out.
  }
  return null
}

function isSession(value: unknown): value is AuthSession {
  if (!value || typeof value !== 'object') return false
  const v = value as Record<string, unknown>
  return (
    typeof v.accessToken === 'string' &&
    typeof v.expiresAtUtc === 'string' &&
    !!v.user &&
    typeof v.user === 'object'
  )
}

/** True when the token's expiry is in the past (with a 30s safety margin). */
export function isExpired(candidate: AuthSession, now: number = Date.now()): boolean {
  const expires = Date.parse(candidate.expiresAtUtc)
  return Number.isFinite(expires) && expires - 30_000 <= now
}

export function getSession(): AuthSession | null {
  if (session === undefined) session = readStorage()
  return session
}

export function getAccessToken(): string | null {
  return getSession()?.accessToken ?? null
}

export function setSession(next: AuthSession | null): void {
  session = next
  try {
    if (next) globalThis.sessionStorage?.setItem(SESSION_STORAGE_KEY, JSON.stringify(next))
    else globalThis.sessionStorage?.removeItem(SESSION_STORAGE_KEY)
  } catch {
    // Storage may be blocked (private mode); the in-memory session still works.
  }
  for (const listener of listeners) listener()
}

export function clearSession(): void {
  if (getSession() !== null) setSession(null)
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}
