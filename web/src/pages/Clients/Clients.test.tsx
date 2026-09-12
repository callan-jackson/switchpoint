import { describe, expect, it } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import { renderApp, signIn } from '@/test/utils'

describe('clients', () => {
  it('lists the firm’s clients', async () => {
    signIn()
    renderApp('/clients')
    expect(await screen.findByRole('heading', { level: 1, name: 'Clients' })).toBeInTheDocument()
    expect(await screen.findByText('Ms Sarah Mitchell')).toBeInTheDocument()
    expect(screen.getByText('Mr David Okafor')).toBeInTheDocument()
    expect(screen.getByText('Dr Priya Shah')).toBeInTheDocument()
  })

  it('filters the list from the search box', async () => {
    signIn()
    const { user } = renderApp('/clients')
    await screen.findByText('Ms Sarah Mitchell')

    await user.type(screen.getByRole('searchbox', { name: /search/i }), 'okafor')
    await waitFor(() => expect(screen.queryByText('Ms Sarah Mitchell')).not.toBeInTheDocument())
    expect(screen.getByText('Mr David Okafor')).toBeInTheDocument()
  })

  it('opens a client and shows the arrangements held for them', async () => {
    signIn()
    const { user } = renderApp('/clients')
    await user.click(await screen.findByText('Ms Sarah Mitchell'))

    expect(await screen.findByRole('heading', { level: 1, name: 'Ms Sarah Mitchell' })).toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: /schemes/i }))
    // Sarah has the workplace plan and the legacy with-profits plan carrying a guaranteed annuity rate.
    expect(await screen.findByText(/Legacy Personal Pension/i)).toBeInTheDocument()
    expect(screen.getByText(/Employer Group Personal Pension/i)).toBeInTheDocument()
  })

  it('opens a client on their arrangements rather than their personal details', async () => {
    signIn()
    renderApp('/clients')
    const row = await screen.findByText('Ms Sarah Mitchell')
    row.click()
    await screen.findByRole('heading', { level: 1, name: 'Ms Sarah Mitchell' })
    // An adviser opening a file wants the money, not the date of birth.
    expect(await screen.findByText(/Legacy Personal Pension/i)).toBeInTheDocument()
  })

  it('masks the National Insurance number', async () => {
    signIn()
    const { user } = renderApp('/clients')
    const row = await screen.findByText('Ms Sarah Mitchell')
    row.click()
    await screen.findByRole('heading', { level: 1, name: 'Ms Sarah Mitchell' })
    await user.click(screen.getByRole('tab', { name: /details/i }))
    // The API never returns the number itself to the browser, only the masked form.
    await waitFor(() => expect(screen.getByText(/\*{6}\d{2}[A-D]/)).toBeInTheDocument())
  })

  it('adds a client through the dialog', async () => {
    signIn()
    const { user } = renderApp('/clients')
    await user.click(await screen.findByRole('button', { name: /add client/i }))

    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText(/first name/i), 'Nia')
    await user.type(within(dialog).getByLabelText(/last name/i), 'Roberts')
    await user.type(within(dialog).getByLabelText(/date of birth/i), '1980-05-04')
    await user.click(within(dialog).getByRole('button', { name: /^(add|save|create)/i }))

    expect(await screen.findByText(/Nia Roberts/)).toBeInTheDocument()
  })
})
