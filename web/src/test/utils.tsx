import type { ReactElement, ReactNode } from 'react'
import { QueryClient } from '@tanstack/react-query'
import { render, type RenderOptions, type RenderResult } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { RouterProvider, createMemoryRouter } from 'react-router'
import { Providers } from '@/app/Providers'
import { routes } from '@/app/routes'
import { setSession } from '@/features/auth/authStore'
import { users } from '@/mocks/data'

/** A cache that never retries and never keeps data between tests. */
export function testQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0, staleTime: 0 },
      mutations: { retry: false },
    },
  })
}

/** Put a signed-in adviser (or another seeded user) into the session store. */
export function signIn(user: keyof typeof users = 'adviser'): void {
  setSession({
    accessToken: 'test.token',
    expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    user: users[user],
  })
}

export function renderWithProviders(
  ui: ReactElement,
  options: RenderOptions & { client?: QueryClient } = {},
): RenderResult & { user: ReturnType<typeof userEvent.setup> } {
  const { client = testQueryClient(), ...rest } = options
  const wrapper = ({ children }: { children: ReactNode }) => <Providers client={client}>{children}</Providers>
  return { ...render(ui, { wrapper, ...rest }), user: userEvent.setup() }
}

/** Render the whole app at a route, through the real route table. */
export function renderApp(initialEntry = '/'): RenderResult & { user: ReturnType<typeof userEvent.setup> } {
  const router = createMemoryRouter(routes, { initialEntries: [initialEntry] })
  const client = testQueryClient()
  return {
    ...render(
      <Providers client={client}>
        <RouterProvider router={router} />
      </Providers>,
    ),
    user: userEvent.setup(),
  }
}

/** Render one page component inside a memory router, at a route that matches it. */
export function renderRoute(
  element: ReactElement,
  { path = '/', entry = '/' }: { path?: string; entry?: string } = {},
): RenderResult & { user: ReturnType<typeof userEvent.setup> } {
  const router = createMemoryRouter([{ path, element }], { initialEntries: [entry] })
  const client = testQueryClient()
  return {
    ...render(
      <Providers client={client}>
        <RouterProvider router={router} />
      </Providers>,
    ),
    user: userEvent.setup(),
  }
}
