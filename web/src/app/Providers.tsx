import type { ReactNode } from 'react'
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query'
import { queryClient as defaultQueryClient } from '@/api/queryClient'
import { ToastProvider } from '@/components/ui/Toast'
import { AuthProvider } from '@/features/auth/AuthProvider'

/**
 * Everything the tree needs regardless of route: the query cache, the session, and the toast
 * viewport. Tests pass their own `QueryClient` so caches never leak between cases.
 */
export function Providers({
  children,
  client = defaultQueryClient,
}: {
  children: ReactNode
  client?: QueryClient
}) {
  return (
    <QueryClientProvider client={client}>
      <AuthProvider>
        <ToastProvider>{children}</ToastProvider>
      </AuthProvider>
    </QueryClientProvider>
  )
}
