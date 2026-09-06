/**
 * Access-token holder shared by the fetch client and the AuthProvider.
 *
 * Kept outside React so `client.ts` has no dependency on the component tree. The AuthProvider is
 * the only writer; everything else reads through `getAccessToken`.
 */

let accessToken: string | null = null
const unauthorizedListeners = new Set<() => void>()

export function getAccessToken(): string | null {
  return accessToken
}

export function setAccessToken(token: string | null): void {
  accessToken = token
}

/** Register a callback fired when the API answers 401 with a token present (session expired). */
export function onUnauthorized(listener: () => void): () => void {
  unauthorizedListeners.add(listener)
  return () => unauthorizedListeners.delete(listener)
}

export function notifyUnauthorized(): void {
  for (const listener of unauthorizedListeners) listener()
}
