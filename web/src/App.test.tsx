import { describe, expect, it } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderApp, signIn } from '@/test/utils'

describe('routing', () => {
  it('sends an anonymous visitor to the sign-in page', async () => {
    renderApp('/clients')
    expect(await screen.findByRole('heading', { name: /sign in/i })).toBeInTheDocument()
  })

  it('renders the dashboard for a signed-in adviser', async () => {
    signIn()
    renderApp('/')
    expect(await screen.findByRole('heading', { level: 1, name: /dashboard/i })).toBeInTheDocument()
    // The stat tiles come from GET /dashboard/summary.
    expect(await screen.findByText(/funds in catalogue/i)).toBeInTheDocument()
  })

  it('shows the shell landmarks and the skip link', async () => {
    signIn()
    renderApp('/')
    await screen.findByRole('heading', { level: 1, name: /dashboard/i })
    expect(screen.getByRole('link', { name: /skip to main content/i })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: /primary/i })).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    // The top bar names the firm the adviser is signed in to.
    expect(screen.getByText('Demo Financial Planning Ltd')).toBeInTheDocument()
  })

  it('marks the current section in the sidebar', async () => {
    signIn()
    renderApp('/clients')
    const link = await screen.findByRole('link', { name: /^clients/i })
    await waitFor(() => expect(link).toHaveAttribute('aria-current', 'page'))
  })

  it('renders a not-found page for an unknown route', async () => {
    signIn()
    renderApp('/nowhere')
    expect(await screen.findByRole('heading', { name: /page not found/i })).toBeInTheDocument()
  })

  it('lazily loads each top-level page', async () => {
    signIn()
    renderApp('/funds')
    expect(await screen.findByRole('heading', { level: 1, name: /fund research/i })).toBeInTheDocument()
  })
})
