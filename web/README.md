# SwitchPoint web

React 19 + TypeScript + Vite single-page application for SwitchPoint.

Everything you need to know about the structure, conventions, mocks and how pages consume the
calculation endpoints lives in [`docs/frontend.md`](../docs/frontend.md).

Quick start:

```bash
npm install
npm run dev          # http://localhost:5173 with MSW mock data (VITE_USE_MOCKS=true)
npm run test         # vitest
npm run typecheck && npm run lint && npm run build
```

Demo sign-in (mocks and seeded API): `adviser@demo.switchpoint.local` / `Demo!Pass123`.
