import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { cleanup } from '@testing-library/react'
import { server } from '@/mocks/server'
import { resetMockDb } from '@/mocks/handlers'
import { setSession } from '@/features/auth/authStore'

// The mock server answers every request in the test run; an unhandled one is a bug in the test.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))

afterEach(() => {
  cleanup()
  server.resetHandlers()
  resetMockDb()
  setSession(null)
})

afterAll(() => server.close())

// jsdom implements <dialog> only partially: showModal/close are missing in some versions, and
// ResizeObserver (recharts) is absent entirely.
if (typeof HTMLDialogElement !== 'undefined') {
  const proto = HTMLDialogElement.prototype as HTMLDialogElement & {
    showModal: () => void
    close: () => void
  }
  if (!proto.showModal) {
    proto.showModal = function showModal(this: HTMLDialogElement) {
      this.open = true
    }
  }
  if (!proto.close) {
    proto.close = function close(this: HTMLDialogElement) {
      this.open = false
    }
  }
}

if (!('ResizeObserver' in globalThis)) {
  class ResizeObserverStub {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  Object.defineProperty(globalThis, 'ResizeObserver', { value: ResizeObserverStub, writable: true })
}

if (!('matchMedia' in globalThis)) {
  Object.defineProperty(globalThis, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
      onchange: null,
    }),
  })
}
