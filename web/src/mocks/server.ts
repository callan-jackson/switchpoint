import { setupServer } from 'msw/node'
import { handlers } from './handlers'

/** Node mock server used by the test setup. */
export const server = setupServer(...handlers)
