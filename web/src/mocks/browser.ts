import { setupWorker } from 'msw/browser'
import { handlers } from './handlers'

/** In-browser mock server; started from `main.tsx` when `VITE_USE_MOCKS === 'true'`. */
export const worker = setupWorker(...handlers)

export async function startMockWorker(): Promise<void> {
  await worker.start({
    onUnhandledRequest: 'bypass',
    quiet: true,
    serviceWorker: { url: '/mockServiceWorker.js' },
  })
}
