import { describe, expect, it } from 'vitest'
import { screen } from '@testing-library/react'
import { renderApp, signIn } from '@/test/utils'

const SAVED_TITLE = 'Sarah Mitchell — consolidation to Investcentre SIPP'

describe('pension switch — copying a locked analysis', () => {
  it('opens a saved analysis with its own title and its locked state', async () => {
    signIn()
    renderApp('/pension-switch/an-switch-1')
    await screen.findByRole('heading', { level: 1, name: new RegExp(SAVED_TITLE.slice(0, 20), 'i') })
    expect(await screen.findByText(/this analysis is locked/i)).toBeInTheDocument()
  })

  it('copies a locked analysis into a new, editable one via ?from=', async () => {
    signIn()
    renderApp('/pension-switch?from=an-switch-1')

    // Issuing a report locks an analysis permanently, and the locked banner has always told the adviser to
    // "copy it to a new analysis" without offering any way to do so. The copy must carry the inputs but NOT
    // the identity, or saving would overwrite a locked record instead of creating a new one.
    await screen.findByRole('heading', { level: 1, name: /\(copy\)/i })
    expect(screen.queryByText(/this analysis is locked/i)).not.toBeInTheDocument()
  })
})
