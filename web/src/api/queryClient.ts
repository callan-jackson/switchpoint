import { QueryClient } from '@tanstack/react-query'
import { isAbortError, isApiError } from './client'

/** Retry once for transient failures only; never for 4xx or cancelled requests. */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (failureCount >= 1) return false
  if (isAbortError(error)) return false
  if (isApiError(error) && error.status > 0 && error.status < 500) return false
  return true
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        retry: shouldRetry,
        refetchOnWindowFocus: false,
        refetchOnReconnect: true,
      },
      mutations: {
        retry: false,
      },
    },
  })
}

export const queryClient = createQueryClient()
