import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { rmSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    // mockServiceWorker.js lives in public/ so the dev server can serve it, which also copies it into
    // every production build. A mock interceptor shipped to a real deployment is one stale registration
    // away from serving fixtures — or a blank page — instead of the API, so drop it unless this build
    // actually uses mocks.
    {
      name: 'switchpoint:strip-mock-worker',
      apply: 'build',
      generateBundle(_options, bundle) {
        if (process.env.VITE_USE_MOCKS === 'true') return
        for (const name of Object.keys(bundle)) {
          if (name.includes('mockServiceWorker')) delete bundle[name]
        }
      },
      closeBundle() {
        if (process.env.VITE_USE_MOCKS === 'true') return
        const worker = fileURLToPath(new URL('./dist/mockServiceWorker.js', import.meta.url))
        rmSync(worker, { force: true })
      },
    },
  ],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: process.env.VITE_API_PROXY ?? 'http://localhost:5080', changeOrigin: true },
    },
  },
  build: { sourcemap: true, chunkSizeWarningLimit: 900 },
  test: {
    environment: 'jsdom',
    globals: true,
    // Every page is lazily imported by the router, so a test's first assertion waits on a dynamic import
    // as well as the mock request behind it. Testing Library's 1s default is enough on an idle machine and
    // not on a loaded one or a small CI runner, where the suite failed only when files ran in parallel.
    testTimeout: 15_000,
    hookTimeout: 15_000,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    coverage: { provider: 'v8', reporter: ['text', 'lcov'], include: ['src/**/*.{ts,tsx}'], exclude: ['src/test/**', 'src/api/generated/**'] },
  },
})
