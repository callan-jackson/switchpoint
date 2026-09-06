# Generated API types

This folder is reserved for the output of `npm run gen:api`, which runs `openapi-typescript`
against the committed OpenAPI export at `docs/api/openapi.json` and writes `schema.d.ts` here.

Do not edit files in this folder by hand. Until the API export exists, `src/api/types.ts` carries
hand-written DTO interfaces that mirror `docs/ARCHITECTURE.md`; once `schema.d.ts` is generated,
`types.ts` should re-export from it and the hand-written shapes should be removed.

`schema.d.ts` is git-ignored until the OpenAPI drift check in CI is live.
