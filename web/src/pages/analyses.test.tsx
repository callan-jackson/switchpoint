import { describe, expect, it } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderApp, signIn } from '@/test/utils'

/**
 * The three analysis wizards, driven the way an adviser drives them: choose a client, fill the
 * inputs, save, calculate, and read the numbers back. The mock server returns the same result
 * shapes as the API, so these assert the wiring between the form, the result and the tables.
 */

describe('pension switch', () => {
  it('runs a switch analysis from an empty form to a critical yield', async () => {
    signIn()
    const { user } = renderApp('/pension-switch')
    expect(await screen.findByRole('heading', { level: 1, name: /new pension switch analysis/i })).toBeInTheDocument()

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Client' }), [
      await screen.findByRole('option', { name: /Sarah Mitchell/i }),
    ])
    await user.type(screen.getByRole('textbox', { name: /analysis title/i }), 'Sarah — consolidate to a SIPP')

    // Both of Sarah's plans are money purchase, so both are offered as ceding arrangements.
    const ceding = await screen.findByRole('checkbox', { name: /Employer Group Personal Pension/i })
    await user.click(ceding)
    expect(await screen.findByText(/total transfer value/i)).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox', { name: /proposed product/i }), [
      await screen.findByRole('option', { name: /Investcentre SIPP/i }),
    ])

    await user.click(screen.getByRole('button', { name: /^save$/i }))
    await waitFor(() => expect(screen.getByRole('button', { name: /^calculate$/i })).toBeEnabled())
    await user.click(screen.getByRole('button', { name: /^calculate$/i }))

    // The headline result: what it costs to move, and the return the new plan must make to match.
    expect((await screen.findAllByText(/initial adviser charge/i)).length).toBeGreaterThan(0)
    expect(await screen.findByText(/headroom \(intermediate\)/i)).toBeInTheDocument()
    expect(screen.getAllByText(/critical yield/i).length).toBeGreaterThan(0)
  })

  it('flags an arrangement that carries guarantees', async () => {
    signIn()
    const { user } = renderApp('/pension-switch')
    await user.selectOptions(await screen.findByRole('combobox', { name: 'Client' }), [
      await screen.findByRole('option', { name: /Sarah Mitchell/i }),
    ])

    // The legacy with-profits plan has a guaranteed annuity rate; it must be visibly flagged
    // before an adviser can select it (FSA pension switching outcome 2).
    expect(await screen.findByText(/Guarantees/)).toBeInTheDocument()
    expect(screen.getByText(/GAR /)).toBeInTheDocument()
  })
})

describe('defined benefit transfer', () => {
  it('produces a transfer value comparator for the deferred scheme', async () => {
    signIn()
    const { user } = renderApp('/db-transfer')
    expect(await screen.findByRole('heading', { level: 1, name: /new db transfer analysis/i })).toBeInTheDocument()

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Client' }), [
      await screen.findByRole('option', { name: /David Okafor/i }),
    ])
    await user.selectOptions(await screen.findByRole('combobox', { name: /defined benefit scheme/i }), [
      await screen.findByRole('option', { name: /Wealden Engineering/i }),
    ])
    await user.selectOptions(screen.getByRole('combobox', { name: /receiving product/i }), [
      await screen.findByRole('option', { name: /Investcentre SIPP/i }),
    ])

    // The receiving product needs holdings whose weights come to 100%; the first fund added
    // takes the whole remaining weight.
    await user.selectOptions(await screen.findByRole('combobox', { name: 'Fund' }), [
      await screen.findByRole('option', { name: /LifeStrategy 60/i }),
    ])
    await user.click(screen.getByRole('button', { name: /add holding/i }))

    // The CETV and the tranche count are read straight off the scheme.
    expect(await screen.findByText('CETV')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^save$/i }))
    await waitFor(() => expect(screen.getByRole('button', { name: /^calculate$/i })).toBeEnabled())
    await user.click(screen.getByRole('button', { name: /^calculate$/i }))

    expect((await screen.findAllByText(/transfer value comparator/i)).length).toBeGreaterThan(0)
    expect(await screen.findByText(/revaluation by tranche/i)).toBeInTheDocument()
  })
})

describe('cashflow', () => {
  it('projects a plan and reports whether it lasts', async () => {
    signIn()
    const { user } = renderApp('/cashflow')
    expect(await screen.findByRole('heading', { level: 1, name: /new cashflow plan/i })).toBeInTheDocument()

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Client' }), [
      await screen.findByRole('option', { name: /Priya Shah/i }),
    ])

    await user.click(screen.getByRole('button', { name: /^save$/i }))
    await waitFor(() => expect(screen.getByRole('button', { name: /^calculate$/i })).toBeEnabled())
    await user.click(screen.getByRole('button', { name: /^calculate$/i }))

    expect(await screen.findByText(/sustainable/i)).toBeInTheDocument()
  })
})
