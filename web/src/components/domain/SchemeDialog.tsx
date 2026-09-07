import { useEffect, useState } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import type {
  ContributionDto,
  ContributionPayer,
  Frequency,
  SchemeDto,
  SchemeType,
  SchemeWrite,
} from '@/api/types'
import { useProviders } from '@/api/queries'
import { emptyChargeSchedule } from '@/lib/charges'
import { contributionPayerLabels, frequencyLabels, optionsFrom, schemeTypeLabels } from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Button, IconButton } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Tabs } from '@/components/ui/Tabs'
import { Field } from '@/components/form/Field'
import { Checkbox, Input, Select } from '@/components/form/Input'
import { IntegerInput, MoneyInput, PercentInput } from '@/components/form/NumericInput'
import { ChargeScheduleEditor, chargeScheduleErrors } from './ChargeScheduleEditor'
import { DefinedBenefitEditor, definedBenefitErrors, emptyDefinedBenefit } from './DefinedBenefitEditor'
import { HoldingsEditor, holdingsWeightError } from './HoldingsEditor'

export function emptyScheme(): SchemeWrite {
  return {
    type: 'personalPension',
    productName: '',
    currentValue: 0,
    transferValue: 0,
    valuationDate: new Date().toISOString().slice(0, 10),
    charges: emptyChargeSchedule(),
    guarantees: {
      withProfits: false,
      marketValueReductionPct: 0,
      terminalBonus: 0,
      loyaltyBonusPct: 0,
    },
    inDrawdown: false,
    contributions: [],
    holdings: [],
  }
}

type TabId = 'details' | 'holdings' | 'charges' | 'guarantees' | 'db'

export interface SchemeDialogProps {
  open: boolean
  onClose: () => void
  /** Undefined when adding; the existing scheme when editing. */
  scheme?: SchemeDto
  onSave: (body: SchemeWrite) => Promise<unknown>
  saving?: boolean
  error?: string | null
}

/**
 * Add or edit one arrangement. Covers the whole of `SchemeWrite`: the base details, holdings,
 * the full charge schedule and — for a DB scheme — the tranche editor. Validation runs across
 * every tab so a problem hidden behind another tab still blocks saving and is named in the alert.
 */
export function SchemeDialog({ open, onClose, scheme, onSave, saving, error }: SchemeDialogProps) {
  const { data: providers } = useProviders()
  const [tab, setTab] = useState<TabId>('details')
  const [value, setValue] = useState<SchemeWrite>(() => scheme ?? emptyScheme())
  const [submitted, setSubmitted] = useState(false)

  useEffect(() => {
    if (!open) return
    setValue(scheme ? structuredClone(scheme) : emptyScheme())
    setTab('details')
    setSubmitted(false)
  }, [open, scheme])

  const isDb = value.type === 'definedBenefit'
  const set = <K extends keyof SchemeWrite>(key: K, next: SchemeWrite[K]) => setValue((v) => ({ ...v, [key]: next }))

  const problems = [
    ...(value.productName.trim() ? [] : ['Enter the product name.']),
    ...(value.valuationDate ? [] : ['Enter the valuation date.']),
    ...(value.currentValue < 0 ? ['Current value cannot be negative.'] : []),
    ...(isDb
      ? definedBenefitErrors(value.definedBenefit ?? emptyDefinedBenefit()).map((e) => `Defined benefit: ${e}`)
      : []),
    ...(!isDb && value.holdings.length > 0 && holdingsWeightError(value.holdings)
      ? [`Holdings: ${holdingsWeightError(value.holdings)}`]
      : []),
    ...chargeScheduleErrors(value.charges),
  ]

  const setContribution = (index: number, patch: Partial<ContributionDto>) => {
    const contributions = [...value.contributions]
    contributions[index] = { ...contributions[index], ...patch }
    set('contributions', contributions)
  }

  const submit = async () => {
    setSubmitted(true)
    if (problems.length > 0) return
    const body: SchemeWrite = {
      ...value,
      definedBenefit: isDb ? (value.definedBenefit ?? emptyDefinedBenefit()) : undefined,
    }
    await onSave(body)
  }

  const tabs = [
    { id: 'details' as const, label: 'Details' },
    { id: 'holdings' as const, label: 'Holdings', badge: value.holdings.length || undefined },
    { id: 'charges' as const, label: 'Charges' },
    { id: 'guarantees' as const, label: 'Guarantees' },
    ...(isDb ? [{ id: 'db' as const, label: 'Defined benefit' }] : []),
  ]

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="xl"
      locked={saving}
      title={scheme ? `Edit ${scheme.productName}` : 'Add an arrangement'}
      description="Everything the calculation engines need about this plan: value, charges, holdings and guarantees."
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} loading={saving}>
            {scheme ? 'Save changes' : 'Add arrangement'}
          </Button>
        </>
      }
    >
      {(error || (submitted && problems.length > 0)) && (
        <Alert tone="danger" title="This arrangement cannot be saved yet" className="mb-4">
          {error ? (
            error
          ) : (
            <ul className="list-disc space-y-0.5 pl-4">
              {problems.map((p) => (
                <li key={p}>{p}</li>
              ))}
            </ul>
          )}
        </Alert>
      )}

      <Tabs items={tabs} value={tab} onChange={setTab} aria-label="Arrangement sections">
        {tab === 'details' && (
          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Arrangement type" required>
                <Select
                  value={value.type}
                  onChange={(e) => {
                    const type = e.target.value as SchemeType
                    setValue((v) => ({
                      ...v,
                      type,
                      definedBenefit:
                        type === 'definedBenefit' ? (v.definedBenefit ?? emptyDefinedBenefit()) : undefined,
                    }))
                  }}
                  options={optionsFrom(schemeTypeLabels)}
                />
              </Field>
              <Field label="Provider">
                <Select
                  value={value.providerId ?? ''}
                  placeholder="Not on the catalogue"
                  onChange={(e) => set('providerId', e.target.value || undefined)}
                  options={(providers ?? []).map((p) => ({ value: p.id, label: p.name }))}
                />
              </Field>
              <Field label="Product name" required>
                <Input value={value.productName} onChange={(e) => set('productName', e.target.value)} />
              </Field>
              <Field label="Policy number">
                <Input value={value.policyNumber ?? ''} onChange={(e) => set('policyNumber', e.target.value || undefined)} />
              </Field>
              <Field label="Current value" required>
                <MoneyInput value={value.currentValue} onChange={(v) => set('currentValue', v ?? 0)} />
              </Field>
              <Field label="Transfer value" hint="After any exit penalty quoted by the ceding scheme.">
                <MoneyInput value={value.transferValue} onChange={(v) => set('transferValue', v ?? 0)} />
              </Field>
              <Field label="Valuation date" required>
                <Input type="date" value={value.valuationDate} onChange={(e) => set('valuationDate', e.target.value)} />
              </Field>
              <Field label="Start date">
                <Input
                  type="date"
                  value={value.startDate ?? ''}
                  onChange={(e) => set('startDate', e.target.value || undefined)}
                />
              </Field>
              <Field label="Selected retirement age">
                <IntegerInput
                  value={value.selectedRetirementAge ?? null}
                  placeholder="Client default"
                  onChange={(v) => set('selectedRetirementAge', v ?? undefined)}
                />
              </Field>
              <div className="flex items-end">
                <Checkbox
                  label="Already in drawdown"
                  checked={value.inDrawdown}
                  onChange={(e) => set('inDrawdown', e.target.checked)}
                />
              </div>
            </div>

            <div className="rounded-lg border border-border p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm font-medium text-fg">Contributions</p>
                <Button
                  size="sm"
                  variant="outline"
                  icon={<Plus />}
                  onClick={() =>
                    set('contributions', [
                      ...value.contributions,
                      {
                        payer: 'member',
                        amount: 0,
                        frequency: 'monthly',
                        escalationPct: 0,
                        isGrossOfTaxRelief: true,
                      },
                    ])
                  }
                >
                  Add contribution
                </Button>
              </div>
              {value.contributions.length === 0 ? (
                <p className="mt-1 text-xs text-fg-muted">No regular contributions recorded.</p>
              ) : (
                <table className="table mt-3">
                  <caption className="sr-only">Contributions</caption>
                  <thead>
                    <tr>
                      <th scope="col">Payer</th>
                      <th scope="col" className="text-right">
                        Amount
                      </th>
                      <th scope="col">Frequency</th>
                      <th scope="col" className="text-right">
                        Escalation
                      </th>
                      <th scope="col">Gross</th>
                      <th scope="col">
                        <span className="sr-only">Actions</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {value.contributions.map((contribution, index) => (
                      <tr key={index}>
                        <td>
                          <Select
                            sizing="sm"
                            aria-label={`Contribution ${index + 1} payer`}
                            value={contribution.payer}
                            onChange={(e) => setContribution(index, { payer: e.target.value as ContributionPayer })}
                            options={optionsFrom(contributionPayerLabels)}
                          />
                        </td>
                        <td>
                          <MoneyInput
                            sizing="sm"
                            aria-label={`Contribution ${index + 1} amount`}
                            value={contribution.amount}
                            onChange={(v) => setContribution(index, { amount: v ?? 0 })}
                          />
                        </td>
                        <td>
                          <Select
                            sizing="sm"
                            aria-label={`Contribution ${index + 1} frequency`}
                            value={contribution.frequency}
                            onChange={(e) => setContribution(index, { frequency: e.target.value as Frequency })}
                            options={optionsFrom(frequencyLabels)}
                          />
                        </td>
                        <td>
                          <PercentInput
                            sizing="sm"
                            aria-label={`Contribution ${index + 1} escalation`}
                            value={contribution.escalationPct}
                            onChange={(v) => setContribution(index, { escalationPct: v ?? 0 })}
                          />
                        </td>
                        <td>
                          <Checkbox
                            label={<span className="sr-only">Gross of tax relief</span>}
                            checked={contribution.isGrossOfTaxRelief}
                            onChange={(e) => setContribution(index, { isGrossOfTaxRelief: e.target.checked })}
                          />
                        </td>
                        <td className="text-right">
                          <IconButton
                            size="sm"
                            label={`Remove contribution ${index + 1}`}
                            onClick={() => set('contributions', value.contributions.filter((_, i) => i !== index))}
                          >
                            <Trash2 />
                          </IconButton>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        )}

        {tab === 'holdings' && (
          <HoldingsEditor holdings={value.holdings} onChange={(holdings) => set('holdings', holdings)} />
        )}

        {tab === 'charges' && (
          <ChargeScheduleEditor value={value.charges} onChange={(charges) => set('charges', charges)} />
        )}

        {tab === 'guarantees' && (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Guaranteed annuity rate" hint="Leave blank if the plan has no GAR.">
              <PercentInput
                value={value.guarantees.guaranteedAnnuityRatePct ?? null}
                onChange={(v) => set('guarantees', { ...value.guarantees, guaranteedAnnuityRatePct: v ?? undefined })}
              />
            </Field>
            <Field label="Guaranteed growth rate">
              <PercentInput
                value={value.guarantees.guaranteedGrowthRatePct ?? null}
                onChange={(v) => set('guarantees', { ...value.guarantees, guaranteedGrowthRatePct: v ?? undefined })}
              />
            </Field>
            <Field label="Protected tax-free cash">
              <PercentInput
                value={value.guarantees.protectedTaxFreeCashPct ?? null}
                onChange={(v) => set('guarantees', { ...value.guarantees, protectedTaxFreeCashPct: v ?? undefined })}
              />
            </Field>
            <Field label="Protected pension age">
              <IntegerInput
                value={value.guarantees.protectedPensionAge ?? null}
                onChange={(v) => set('guarantees', { ...value.guarantees, protectedPensionAge: v ?? undefined })}
              />
            </Field>
            <Field label="Market value reduction">
              <PercentInput
                value={value.guarantees.marketValueReductionPct}
                onChange={(v) => set('guarantees', { ...value.guarantees, marketValueReductionPct: v ?? 0 })}
              />
            </Field>
            <Field label="Terminal bonus">
              <MoneyInput
                value={value.guarantees.terminalBonus}
                onChange={(v) => set('guarantees', { ...value.guarantees, terminalBonus: v ?? 0 })}
              />
            </Field>
            <Field label="Loyalty bonus">
              <PercentInput
                value={value.guarantees.loyaltyBonusPct}
                onChange={(v) => set('guarantees', { ...value.guarantees, loyaltyBonusPct: v ?? 0 })}
              />
            </Field>
            <div className="flex items-end">
              <Checkbox
                label="With-profits fund"
                hint="Flags the arrangement for adviser judgement in a switch."
                checked={value.guarantees.withProfits}
                onChange={(e) => set('guarantees', { ...value.guarantees, withProfits: e.target.checked })}
              />
            </div>
          </div>
        )}

        {tab === 'db' && (
          <DefinedBenefitEditor
            value={value.definedBenefit ?? emptyDefinedBenefit()}
            onChange={(db) => set('definedBenefit', db)}
          />
        )}
      </Tabs>
    </Dialog>
  )
}
