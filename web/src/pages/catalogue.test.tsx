import { describe, expect, it } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderApp, signIn } from '@/test/utils'

describe('dashboard', () => {
  it('shows the headline counts and recent work', async () => {
    signIn()
    renderApp('/')
    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(await screen.findByText(/funds in catalogue/i)).toBeInTheDocument()
    expect(screen.getByText(/analyses in progress/i)).toBeInTheDocument()
  })
})

describe('products and charges', () => {
  it('lists products with their effective charges', async () => {
    signIn()
    renderApp('/products')
    expect(await screen.findByRole('heading', { level: 1, name: /products and charges/i })).toBeInTheDocument()
    expect(await screen.findByText(/Investcentre SIPP/i)).toBeInTheDocument()
  })

  it('filters by search term', async () => {
    signIn()
    const { user } = renderApp('/products')
    await screen.findByText(/Investcentre SIPP/i)

    await user.type(screen.getByRole('searchbox', { name: /search/i }), 'transact')
    await waitFor(() => expect(screen.queryByText(/Investcentre SIPP/i)).not.toBeInTheDocument())
    expect(screen.getAllByText(/Transact/i).length).toBeGreaterThan(0)
  })

  it('opens a product and shows its charge versions', async () => {
    signIn()
    const { user } = renderApp('/products')
    await user.click(await screen.findByText(/Investcentre SIPP/i))
    expect(await screen.findByRole('heading', { level: 1, name: /Investcentre SIPP/i })).toBeInTheDocument()
    // The detail page states the effective charge at both reference fund sizes.
    expect(screen.getByText(/at £100,000/i)).toBeInTheDocument()
  })
})

describe('fund research', () => {
  it('searches the catalogue by ISIN', async () => {
    signIn()
    const { user } = renderApp('/funds')
    expect(await screen.findByRole('heading', { level: 1, name: /fund research/i })).toBeInTheDocument()
    await screen.findByText(/Vanguard LifeStrategy 60% Equity/i)

    await user.type(screen.getByRole('searchbox', { name: /search/i }), 'IE00B4L5Y983')
    expect(await screen.findByText(/iShares Core MSCI World/i)).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByText(/Vanguard LifeStrategy 60% Equity/i)).not.toBeInTheDocument(),
    )
  })
})

describe('reports', () => {
  it('lists the documents issued to clients', async () => {
    signIn()
    renderApp('/reports')
    expect(await screen.findByRole('heading', { level: 1, name: 'Reports' })).toBeInTheDocument()
    // The seeded documents are a DB transfer report and a suitability report.
    expect(await screen.findByText('DB transfer')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /download/i }).length).toBeGreaterThan(0)
  })
})

describe('audit', () => {
  it('lists events and verifies the hash chain', async () => {
    signIn('compliance')
    const { user } = renderApp('/audit')
    expect(await screen.findByRole('heading', { level: 1, name: 'Audit' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /verify chain/i }))
    expect(await screen.findByText(/chain verified/i)).toBeInTheDocument()
    expect(screen.getByText(/hash correctly against their predecessor/i)).toBeInTheDocument()
  })
})

describe('settings', () => {
  it('shows the assumption sets and the signed-in account', async () => {
    signIn()
    renderApp('/settings')
    expect(await screen.findByRole('heading', { level: 1, name: 'Settings' })).toBeInTheDocument()
    expect(await screen.findByText(/FCA standard 2026\/27/i)).toBeInTheDocument()
    expect(screen.getByText('adviser@demo.switchpoint.local')).toBeInTheDocument()
  })
})
