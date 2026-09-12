import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

/**
 * Removes a mock service worker left registered by an earlier session.
 *
 * The worker intercepts every request on its origin and outlives the page that registered it, so an
 * origin that once ran with mocks keeps serving them — or, once the fixtures no longer match, serves
 * nothing and renders a blank page. Switching a flag off cannot undo a registration; only unregistering
 * can, and it has to happen before the app makes its first request.
 */
async function dropStaleMockWorker() {
  if (!('serviceWorker' in navigator)) return
  try {
    const registrations = await navigator.serviceWorker.getRegistrations()
    await Promise.all(
      registrations
        .filter((r) => r.active?.scriptURL.includes('mockServiceWorker'))
        .map((r) => r.unregister()),
    )
  } catch {
    // A blocked or unavailable service worker registry is not a reason to fail to boot.
  }
}

/** Start the in-browser mock server before the app makes its first request. */
async function start() {
  if (import.meta.env.VITE_USE_MOCKS === 'true') {
    const { startMockWorker } = await import('./mocks/browser')
    await startMockWorker()
  } else {
    await dropStaleMockWorker()
  }

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}

void start()
