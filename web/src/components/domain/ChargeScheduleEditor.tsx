import { Plus, Trash2 } from 'lucide-react'
import type {
  ChargeScheduleDto,
  FixedChargeDto,
  Frequency,
  IndexationBasis,
  FixedChargeScope,
  TieredChargeDto,
  TieredChargeMode,
} from '@/api/types'
import { tieredEffectivePct } from '@/lib/charges'
import { fmtPct, gbp } from '@/lib/format'
import {
  fundChargeKindLabels,
  frequencyLabels,
  indexationBasisLabels,
  optionsFrom,
  tieredChargeModeLabels,
} from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Button, IconButton } from '@/components/ui/Button'
import { Field } from '@/components/form/Field'
import { Checkbox, Input, Select } from '@/components/form/Input'
import { IntegerInput, MoneyInput, PercentInput } from '@/components/form/NumericInput'

export interface ChargeScheduleEditorProps {
  value: ChargeScheduleDto
  onChange: (next: ChargeScheduleDto) => void
  disabled?: boolean
  /** Adviser charges live on the analysis for a proposed product, so they can be hidden here. */
  showAdviserCharges?: boolean
}

/**
 * Tier bands must be strictly increasing and only the last band may be unbounded, otherwise the
 * engine cannot decide which band a value falls into.
 */
export function tierBandErrors(tiered: TieredChargeDto | undefined): string[] {
  if (!tiered) return []
  const errors: string[] = []
  if (tiered.bands.length === 0) errors.push('Add at least one band, or remove the charge.')
  let previous = 0
  tiered.bands.forEach((band, index) => {
    const isLast = index === tiered.bands.length - 1
    if (band.upTo === undefined || band.upTo === null) {
      if (!isLast) errors.push(`Band ${index + 1} has no upper limit, so it must be the last band.`)
    } else {
      if (band.upTo <= previous) {
        errors.push(`Band ${index + 1} must end above ${gbp(previous)}.`)
      }
      previous = band.upTo
    }
    if (band.annualRatePct < 0) errors.push(`Band ${index + 1} cannot have a negative rate.`)
  })
  return errors
}

/** Every validation message for a schedule, used to gate saving. */
export function chargeScheduleErrors(schedule: ChargeScheduleDto): string[] {
  return [
    ...tierBandErrors(schedule.platformCharge).map((e) => `Platform charge: ${e}`),
    ...tierBandErrors(schedule.productCharge).map((e) => `Product charge: ${e}`),
    ...(schedule.fundCharge.kind === 'explicit' && (schedule.fundCharge.ocfPct ?? 0) <= 0
      ? ['Fund charge: enter the explicit OCF, or switch to weighted from holdings.']
      : []),
    ...(schedule.allocationRatePct <= 0 ? ['Allocation rate must be greater than zero.'] : []),
  ]
}

function TieredEditor({
  label,
  value,
  onChange,
  disabled,
}: {
  label: string
  value: TieredChargeDto | undefined
  onChange: (next: TieredChargeDto | undefined) => void
  disabled?: boolean
}) {
  const errors = tierBandErrors(value)

  if (!value) {
    return (
      <div className="rounded-lg border border-dashed border-border-strong p-4">
        <p className="text-sm font-medium text-fg">{label}</p>
        <p className="mt-0.5 text-xs text-fg-muted">Not charged on this product.</p>
        <Button
          size="sm"
          variant="outline"
          className="mt-2"
          icon={<Plus />}
          disabled={disabled}
          onClick={() => onChange({ mode: 'marginal', bands: [{ annualRatePct: 0.3 }] })}
        >
          Add {label.toLowerCase()}
        </Button>
      </div>
    )
  }

  return (
    <div className="rounded-lg border border-border p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <p className="text-sm font-medium text-fg">{label}</p>
        <Button size="sm" variant="ghost" disabled={disabled} onClick={() => onChange(undefined)}>
          Remove
        </Button>
      </div>

      <Field label="How bands apply" className="mt-3 max-w-md">
        <Select
          value={value.mode}
          disabled={disabled}
          onChange={(e) => onChange({ ...value, mode: e.target.value as TieredChargeMode })}
          options={optionsFrom(tieredChargeModeLabels)}
        />
      </Field>

      <table className="table mt-3">
        <caption className="sr-only">{label} bands</caption>
        <thead>
          <tr>
            <th scope="col">Band</th>
            <th scope="col" className="text-right">
              Up to
            </th>
            <th scope="col" className="text-right">
              Annual rate
            </th>
            <th scope="col">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {value.bands.map((band, index) => (
            <tr key={index}>
              <td className="text-xs text-fg-muted">{index + 1}</td>
              <td>
                <MoneyInput
                  sizing="sm"
                  aria-label={`Band ${index + 1} upper limit`}
                  dp={0}
                  disabled={disabled}
                  value={band.upTo ?? null}
                  placeholder="No limit"
                  onChange={(v) => {
                    const bands = [...value.bands]
                    bands[index] = { ...bands[index], upTo: v ?? undefined }
                    onChange({ ...value, bands })
                  }}
                />
              </td>
              <td>
                <PercentInput
                  sizing="sm"
                  aria-label={`Band ${index + 1} annual rate`}
                  disabled={disabled}
                  value={band.annualRatePct}
                  onChange={(v) => {
                    const bands = [...value.bands]
                    bands[index] = { ...bands[index], annualRatePct: v ?? 0 }
                    onChange({ ...value, bands })
                  }}
                />
              </td>
              <td className="text-right">
                <IconButton
                  size="sm"
                  label={`Remove band ${index + 1}`}
                  disabled={disabled || value.bands.length === 1}
                  onClick={() => onChange({ ...value, bands: value.bands.filter((_, i) => i !== index) })}
                >
                  <Trash2 />
                </IconButton>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
        <Button
          size="sm"
          variant="outline"
          icon={<Plus />}
          disabled={disabled}
          onClick={() => onChange({ ...value, bands: [...value.bands, { annualRatePct: 0.1 }] })}
        >
          Add band
        </Button>
        <p className="text-xs text-fg-muted">
          Effective rate at £100,000: <span className="font-semibold text-fg">{fmtPct(tieredEffectivePct(value, 100_000), 3)}</span>
          {' · '}at £500,000: <span className="font-semibold text-fg">{fmtPct(tieredEffectivePct(value, 500_000), 3)}</span>
        </p>
      </div>

      {errors.length > 0 && (
        <Alert tone="danger" title="Fix the bands" className="mt-3">
          <ul className="list-disc space-y-0.5 pl-4">
            {errors.map((e) => (
              <li key={e}>{e}</li>
            ))}
          </ul>
        </Alert>
      )}
    </div>
  )
}

/**
 * Editor for the whole `ChargeScheduleDto`: tiered platform and product charges, fixed charges,
 * the fund-charge basis, adviser charges and exit penalties. Every `Pct` field stores a
 * percentage number, matching the contract.
 */
export function ChargeScheduleEditor({
  value,
  onChange,
  disabled,
  showAdviserCharges = true,
}: ChargeScheduleEditorProps) {
  const set = <K extends keyof ChargeScheduleDto>(key: K, next: ChargeScheduleDto[K]) =>
    onChange({ ...value, [key]: next })

  const setFixed = (index: number, patch: Partial<FixedChargeDto>) => {
    const fixedCharges = [...value.fixedCharges]
    fixedCharges[index] = { ...fixedCharges[index], ...patch }
    set('fixedCharges', fixedCharges)
  }

  return (
    <div className="space-y-4">
      <TieredEditor
        label="Platform charge"
        value={value.platformCharge}
        disabled={disabled}
        onChange={(next) => set('platformCharge', next)}
      />
      <TieredEditor
        label="Product charge"
        value={value.productCharge}
        disabled={disabled}
        onChange={(next) => set('productCharge', next)}
      />

      {/* Fixed charges ---------------------------------------------------- */}
      <div className="rounded-lg border border-border p-4">
        <p className="text-sm font-medium text-fg">Fixed charges</p>
        {value.fixedCharges.length === 0 ? (
          <p className="mt-0.5 text-xs text-fg-muted">No flat fees on this product.</p>
        ) : (
          <table className="table mt-3">
            <caption className="sr-only">Fixed charges</caption>
            <thead>
              <tr>
                <th scope="col">Description</th>
                <th scope="col" className="text-right">
                  Amount
                </th>
                <th scope="col">Frequency</th>
                <th scope="col">Indexation</th>
                <th scope="col">Applies to</th>
                <th scope="col">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {value.fixedCharges.map((charge, index) => (
                <tr key={index}>
                  <td>
                    <Input
                      sizing="sm"
                      aria-label={`Fixed charge ${index + 1} description`}
                      disabled={disabled}
                      value={charge.description ?? ''}
                      onChange={(e) => setFixed(index, { description: e.target.value })}
                    />
                  </td>
                  <td>
                    <MoneyInput
                      sizing="sm"
                      aria-label={`Fixed charge ${index + 1} amount`}
                      disabled={disabled}
                      value={charge.amount}
                      onChange={(v) => setFixed(index, { amount: v ?? 0 })}
                    />
                  </td>
                  <td>
                    <Select
                      sizing="sm"
                      aria-label={`Fixed charge ${index + 1} frequency`}
                      disabled={disabled}
                      value={charge.frequency}
                      onChange={(e) => setFixed(index, { frequency: e.target.value as Frequency })}
                      options={optionsFrom(frequencyLabels)}
                    />
                  </td>
                  <td className="flex items-center gap-1">
                    <Select
                      sizing="sm"
                      aria-label={`Fixed charge ${index + 1} indexation`}
                      disabled={disabled}
                      value={charge.indexation.basis}
                      onChange={(e) =>
                        setFixed(index, {
                          indexation: { ...charge.indexation, basis: e.target.value as IndexationBasis },
                        })
                      }
                      options={optionsFrom(indexationBasisLabels)}
                    />
                    {charge.indexation.basis === 'fixed' && (
                      <PercentInput
                        sizing="sm"
                        aria-label={`Fixed charge ${index + 1} indexation rate`}
                        disabled={disabled}
                        value={charge.indexation.ratePct}
                        onChange={(v) => setFixed(index, { indexation: { ...charge.indexation, ratePct: v ?? 0 } })}
                      />
                    )}
                  </td>
                  <td>
                    <Select
                      sizing="sm"
                      aria-label={`Fixed charge ${index + 1} scope`}
                      disabled={disabled}
                      value={charge.appliesTo}
                      onChange={(e) => setFixed(index, { appliesTo: e.target.value as FixedChargeScope })}
                      options={[
                        { value: 'wrapper', label: 'Wrapper' },
                        { value: 'drawdown', label: 'Drawdown' },
                        { value: 'sipp', label: 'SIPP' },
                      ]}
                    />
                  </td>
                  <td className="text-right">
                    <IconButton
                      size="sm"
                      label={`Remove fixed charge ${index + 1}`}
                      disabled={disabled}
                      onClick={() => set('fixedCharges', value.fixedCharges.filter((_, i) => i !== index))}
                    >
                      <Trash2 />
                    </IconButton>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <Button
          size="sm"
          variant="outline"
          className="mt-2"
          icon={<Plus />}
          disabled={disabled}
          onClick={() =>
            set('fixedCharges', [
              ...value.fixedCharges,
              {
                amount: 0,
                frequency: 'annually',
                indexation: { basis: 'none', ratePct: 0 },
                appliesTo: 'wrapper',
                description: '',
              },
            ])
          }
        >
          Add fixed charge
        </Button>
      </div>

      {/* Fund and transaction costs --------------------------------------- */}
      <div className="rounded-lg border border-border p-4">
        <p className="text-sm font-medium text-fg">Investment costs</p>
        <div className="mt-3 grid gap-3 sm:grid-cols-3">
          <Field label="Fund charge basis">
            <Select
              value={value.fundCharge.kind}
              disabled={disabled}
              onChange={(e) =>
                set('fundCharge', {
                  kind: e.target.value as ChargeScheduleDto['fundCharge']['kind'],
                  ocfPct: value.fundCharge.ocfPct,
                })
              }
              options={optionsFrom(fundChargeKindLabels)}
            />
          </Field>
          {value.fundCharge.kind === 'explicit' && (
            <Field label="Explicit OCF">
              <PercentInput
                value={value.fundCharge.ocfPct ?? null}
                disabled={disabled}
                onChange={(v) => set('fundCharge', { ...value.fundCharge, ocfPct: v ?? undefined })}
              />
            </Field>
          )}
          <Field label="Transaction costs">
            <PercentInput
              value={value.transactionCostsPct}
              disabled={disabled}
              onChange={(v) => set('transactionCostsPct', v ?? 0)}
            />
          </Field>
          <Field label="Bid/offer spread">
            <PercentInput
              value={value.bidOfferSpreadPct}
              disabled={disabled}
              onChange={(v) => set('bidOfferSpreadPct', v ?? 0)}
            />
          </Field>
          <Field label="Allocation rate" hint="100% means every pound is invested.">
            <PercentInput
              value={value.allocationRatePct}
              disabled={disabled}
              onChange={(v) => set('allocationRatePct', v ?? 100)}
            />
          </Field>
        </div>
      </div>

      {/* Adviser charges -------------------------------------------------- */}
      {showAdviserCharges && (
        <div className="rounded-lg border border-border p-4">
          <p className="text-sm font-medium text-fg">Adviser charges</p>
          <div className="mt-3 grid gap-3 sm:grid-cols-4">
            <Field label="Initial %">
              <PercentInput
                value={value.adviserCharges.initialPct}
                disabled={disabled}
                onChange={(v) => set('adviserCharges', { ...value.adviserCharges, initialPct: v ?? 0 })}
              />
            </Field>
            <Field label="Initial £">
              <MoneyInput
                value={value.adviserCharges.initialAmount}
                disabled={disabled}
                onChange={(v) => set('adviserCharges', { ...value.adviserCharges, initialAmount: v ?? 0 })}
              />
            </Field>
            <Field label="Ongoing % pa">
              <PercentInput
                value={value.adviserCharges.ongoingPct}
                disabled={disabled}
                onChange={(v) => set('adviserCharges', { ...value.adviserCharges, ongoingPct: v ?? 0 })}
              />
            </Field>
            <Field label="Ongoing £ pa">
              <MoneyInput
                value={value.adviserCharges.ongoingAmount}
                disabled={disabled}
                onChange={(v) => set('adviserCharges', { ...value.adviserCharges, ongoingAmount: v ?? 0 })}
              />
            </Field>
          </div>
        </div>
      )}

      {/* Activity charges ------------------------------------------------- */}
      <div className="rounded-lg border border-border p-4">
        <p className="text-sm font-medium text-fg">Dealing and switching</p>
        <div className="mt-3 grid gap-3 sm:grid-cols-3 lg:grid-cols-6">
          <Field label="Fund deal £">
            <MoneyInput
              value={value.dealingCharges.fundDealAmount}
              disabled={disabled}
              onChange={(v) => set('dealingCharges', { ...value.dealingCharges, fundDealAmount: v ?? 0 })}
            />
          </Field>
          <Field label="ETF deal £">
            <MoneyInput
              value={value.dealingCharges.etfDealAmount}
              disabled={disabled}
              onChange={(v) => set('dealingCharges', { ...value.dealingCharges, etfDealAmount: v ?? 0 })}
            />
          </Field>
          <Field label="Fund deals pa">
            <IntegerInput
              value={value.dealingCharges.expectedFundDealsPerYear}
              disabled={disabled}
              onChange={(v) => set('dealingCharges', { ...value.dealingCharges, expectedFundDealsPerYear: v ?? 0 })}
            />
          </Field>
          <Field label="ETF deals pa">
            <IntegerInput
              value={value.dealingCharges.expectedEtfDealsPerYear}
              disabled={disabled}
              onChange={(v) => set('dealingCharges', { ...value.dealingCharges, expectedEtfDealsPerYear: v ?? 0 })}
            />
          </Field>
          <Field label="Switch £">
            <MoneyInput
              value={value.switchCharge.amountPerSwitch}
              disabled={disabled}
              onChange={(v) => set('switchCharge', { ...value.switchCharge, amountPerSwitch: v ?? 0 })}
            />
          </Field>
          <Field label="Switches pa">
            <IntegerInput
              value={value.switchCharge.expectedSwitchesPerYear}
              disabled={disabled}
              onChange={(v) => set('switchCharge', { ...value.switchCharge, expectedSwitchesPerYear: v ?? 0 })}
            />
          </Field>
        </div>
      </div>

      {/* Exit penalties --------------------------------------------------- */}
      <div className="rounded-lg border border-border p-4">
        <p className="text-sm font-medium text-fg">Exit penalties</p>
        <p className="mt-0.5 text-xs text-fg-muted">
          Bands are read in order; the first band whose &quot;until year&quot; has not passed applies.
        </p>
        {value.exitPenalty.bands.length > 0 && (
          <table className="table mt-3">
            <caption className="sr-only">Exit penalty bands</caption>
            <thead>
              <tr>
                <th scope="col" className="text-right">
                  Until year
                </th>
                <th scope="col" className="text-right">
                  Rate
                </th>
                <th scope="col" className="text-right">
                  Flat amount
                </th>
                <th scope="col">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {value.exitPenalty.bands.map((band, index) => (
                <tr key={index}>
                  <td>
                    <IntegerInput
                      sizing="sm"
                      aria-label={`Exit penalty ${index + 1} until year`}
                      disabled={disabled}
                      value={band.untilYearsFromStart ?? null}
                      placeholder="Always"
                      onChange={(v) => {
                        const bands = [...value.exitPenalty.bands]
                        bands[index] = { ...bands[index], untilYearsFromStart: v ?? undefined }
                        set('exitPenalty', { bands })
                      }}
                    />
                  </td>
                  <td>
                    <PercentInput
                      sizing="sm"
                      aria-label={`Exit penalty ${index + 1} rate`}
                      disabled={disabled}
                      value={band.ratePct}
                      onChange={(v) => {
                        const bands = [...value.exitPenalty.bands]
                        bands[index] = { ...bands[index], ratePct: v ?? 0 }
                        set('exitPenalty', { bands })
                      }}
                    />
                  </td>
                  <td>
                    <MoneyInput
                      sizing="sm"
                      aria-label={`Exit penalty ${index + 1} amount`}
                      disabled={disabled}
                      value={band.amount}
                      onChange={(v) => {
                        const bands = [...value.exitPenalty.bands]
                        bands[index] = { ...bands[index], amount: v ?? 0 }
                        set('exitPenalty', { bands })
                      }}
                    />
                  </td>
                  <td className="text-right">
                    <IconButton
                      size="sm"
                      label={`Remove exit penalty ${index + 1}`}
                      disabled={disabled}
                      onClick={() =>
                        set('exitPenalty', { bands: value.exitPenalty.bands.filter((_, i) => i !== index) })
                      }
                    >
                      <Trash2 />
                    </IconButton>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <Button
          size="sm"
          variant="outline"
          className="mt-2"
          icon={<Plus />}
          disabled={disabled}
          onClick={() =>
            set('exitPenalty', {
              bands: [...value.exitPenalty.bands, { untilYearsFromStart: 5, ratePct: 1, amount: 0 }],
            })
          }
        >
          Add exit penalty band
        </Button>
      </div>

      <div className="rounded-lg border border-border p-4">
        <Checkbox
          label="Large fund discount applies"
          hint="Rebate rate credited once the fund value passes the threshold."
          disabled={disabled}
          checked={value.largeFundDiscounts.length > 0}
          onChange={(e) =>
            set('largeFundDiscounts', e.target.checked ? [{ threshold: 250_000, rebateRatePct: 0.05 }] : [])
          }
        />
        {value.largeFundDiscounts.map((discount, index) => (
          <div key={index} className="mt-3 grid gap-3 sm:grid-cols-2">
            <Field label="Threshold">
              <MoneyInput
                dp={0}
                value={discount.threshold}
                disabled={disabled}
                onChange={(v) => {
                  const next = [...value.largeFundDiscounts]
                  next[index] = { ...next[index], threshold: v ?? 0 }
                  set('largeFundDiscounts', next)
                }}
              />
            </Field>
            <Field label="Rebate rate">
              <PercentInput
                value={discount.rebateRatePct}
                disabled={disabled}
                onChange={(v) => {
                  const next = [...value.largeFundDiscounts]
                  next[index] = { ...next[index], rebateRatePct: v ?? 0 }
                  set('largeFundDiscounts', next)
                }}
              />
            </Field>
          </div>
        ))}
      </div>
    </div>
  )
}
