import { Plus, Trash2 } from 'lucide-react'
import type { DefinedBenefitDto, DbTrancheDto, IndexBasis, IndexRuleDto, SchemeFundingStatus } from '@/api/types'
import { gbp } from '@/lib/format'
import { fundingStatusLabels, indexBasisLabels, optionsFrom } from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Button, IconButton } from '@/components/ui/Button'
import { Field } from '@/components/form/Field'
import { Checkbox, Input, Select } from '@/components/form/Input'
import { IntegerInput, MoneyInput, PercentInput } from '@/components/form/NumericInput'

export function emptyDefinedBenefit(): DefinedBenefitDto {
  return {
    dateOfLeaving: '',
    normalRetirementAge: 65,
    cetvGuaranteeExpiry: '',
    tranches: [
      {
        name: 'Post-97',
        accruedAnnualPension: 0,
        revaluation: { basis: 'lpiCpi', ratePct: 2.5, capPct: 5 },
        escalation: { basis: 'lpiRpi', ratePct: 3, capPct: 5 },
        isGmp: false,
      },
    ],
    spousePensionPct: 50,
    guaranteePeriodYears: 5,
    pclsCommutationFactor: 20,
    maxPclsPct: 25,
    earlyRetirementReductionPct: 4,
    bridgingPensionAnnual: 0,
    fundingStatus: 'fullyFunded',
  }
}

export function definedBenefitErrors(db: DefinedBenefitDto): string[] {
  const errors: string[] = []
  if (!db.dateOfLeaving) errors.push('Enter the date of leaving pensionable service.')
  if (!db.cetvGuaranteeExpiry) errors.push('Enter the date the CETV guarantee expires.')
  if (db.tranches.length === 0) errors.push('Add at least one benefit tranche.')
  if (db.tranches.some((t) => t.accruedAnnualPension <= 0)) {
    errors.push('Every tranche needs an accrued annual pension above zero.')
  }
  if (db.tranches.some((t) => !t.name.trim())) errors.push('Every tranche needs a name.')
  return errors
}

function IndexRuleFields({
  legend,
  value,
  onChange,
  disabled,
  idPrefix,
}: {
  legend: string
  value: IndexRuleDto
  onChange: (next: IndexRuleDto) => void
  disabled?: boolean
  idPrefix: string
}) {
  const capped = value.basis === 'lpiCpi' || value.basis === 'lpiRpi'
  return (
    <div className="grid gap-2 sm:grid-cols-3">
      <Field label={`${legend} basis`} htmlFor={`${idPrefix}-basis`}>
        <Select
          id={`${idPrefix}-basis`}
          sizing="sm"
          disabled={disabled}
          value={value.basis}
          onChange={(e) => onChange({ ...value, basis: e.target.value as IndexBasis })}
          options={optionsFrom(indexBasisLabels)}
        />
      </Field>
      <Field label={`${legend} rate`} htmlFor={`${idPrefix}-rate`}>
        <PercentInput
          id={`${idPrefix}-rate`}
          sizing="sm"
          disabled={disabled || value.basis === 'none'}
          value={value.ratePct}
          onChange={(v) => onChange({ ...value, ratePct: v ?? 0 })}
        />
      </Field>
      <Field label={`${legend} cap`} htmlFor={`${idPrefix}-cap`}>
        <PercentInput
          id={`${idPrefix}-cap`}
          sizing="sm"
          disabled={disabled || !capped}
          value={value.capPct ?? null}
          placeholder={capped ? '5.00' : 'n/a'}
          onChange={(v) => onChange({ ...value, capPct: v ?? undefined })}
        />
      </Field>
    </div>
  )
}

/**
 * Defined benefit scheme details, including the tranche table the TVC and revaluation
 * calculations are driven from. Tranches are the unit of revaluation and escalation: GMP,
 * pre-97 excess and post-97 accrual each revalue differently, so they are entered separately.
 */
export function DefinedBenefitEditor({
  value,
  onChange,
  disabled,
}: {
  value: DefinedBenefitDto
  onChange: (next: DefinedBenefitDto) => void
  disabled?: boolean
}) {
  const set = <K extends keyof DefinedBenefitDto>(key: K, next: DefinedBenefitDto[K]) =>
    onChange({ ...value, [key]: next })

  const setTranche = (index: number, patch: Partial<DbTrancheDto>) => {
    const tranches = [...value.tranches]
    tranches[index] = { ...tranches[index], ...patch }
    set('tranches', tranches)
  }

  const errors = definedBenefitErrors(value)
  const totalAccrued = value.tranches.reduce((s, t) => s + t.accruedAnnualPension, 0)

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Date of leaving" required>
          <Input
            type="date"
            disabled={disabled}
            value={value.dateOfLeaving}
            onChange={(e) => set('dateOfLeaving', e.target.value)}
          />
        </Field>
        <Field label="Normal retirement age" required>
          <IntegerInput
            disabled={disabled}
            value={value.normalRetirementAge}
            onChange={(v) => set('normalRetirementAge', v ?? 65)}
          />
        </Field>
        <Field label="CETV guarantee expires" required>
          <Input
            type="date"
            disabled={disabled}
            value={value.cetvGuaranteeExpiry}
            onChange={(e) => set('cetvGuaranteeExpiry', e.target.value)}
          />
        </Field>
      </div>

      <div className="rounded-lg border border-border p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <p className="text-sm font-medium text-fg">Benefit tranches</p>
            <p className="mt-0.5 text-xs text-fg-muted">
              Total accrued pension <span className="font-semibold text-fg">{gbp(totalAccrued)}</span> pa at the date of
              leaving.
            </p>
          </div>
          <Button
            size="sm"
            variant="outline"
            icon={<Plus />}
            disabled={disabled}
            onClick={() =>
              set('tranches', [
                ...value.tranches,
                {
                  name: 'New tranche',
                  accruedAnnualPension: 0,
                  revaluation: { basis: 'lpiCpi', ratePct: 2.5, capPct: 5 },
                  escalation: { basis: 'lpiRpi', ratePct: 3, capPct: 5 },
                  isGmp: false,
                },
              ])
            }
          >
            Add tranche
          </Button>
        </div>

        <ul className="mt-3 space-y-3">
          {value.tranches.map((tranche, index) => (
            <li key={index} className="rounded-lg border border-border bg-surface-muted p-3">
              <div className="grid gap-3 sm:grid-cols-3">
                <Field label="Tranche name" required>
                  <Input
                    sizing="sm"
                    disabled={disabled}
                    value={tranche.name}
                    onChange={(e) => setTranche(index, { name: e.target.value })}
                  />
                </Field>
                <Field label="Accrued pension pa" required>
                  <MoneyInput
                    sizing="sm"
                    disabled={disabled}
                    value={tranche.accruedAnnualPension}
                    onChange={(v) => setTranche(index, { accruedAnnualPension: v ?? 0 })}
                  />
                </Field>
                <div className="flex items-end justify-between gap-2">
                  <Checkbox
                    label="GMP"
                    disabled={disabled}
                    checked={tranche.isGmp}
                    onChange={(e) => setTranche(index, { isGmp: e.target.checked })}
                  />
                  <IconButton
                    size="sm"
                    label={`Remove tranche ${tranche.name}`}
                    disabled={disabled || value.tranches.length === 1}
                    onClick={() => set('tranches', value.tranches.filter((_, i) => i !== index))}
                  >
                    <Trash2 />
                  </IconButton>
                </div>
              </div>
              <div className="mt-3 space-y-2">
                <IndexRuleFields
                  legend="Revaluation in deferment"
                  idPrefix={`tranche-${index}-rev`}
                  value={tranche.revaluation}
                  disabled={disabled}
                  onChange={(next) => setTranche(index, { revaluation: next })}
                />
                <IndexRuleFields
                  legend="Escalation in payment"
                  idPrefix={`tranche-${index}-esc`}
                  value={tranche.escalation}
                  disabled={disabled}
                  onChange={(next) => setTranche(index, { escalation: next })}
                />
              </div>
            </li>
          ))}
        </ul>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Spouse's pension" hint="Percentage of the member's pension.">
          <PercentInput
            disabled={disabled}
            value={value.spousePensionPct}
            onChange={(v) => set('spousePensionPct', v ?? 0)}
          />
        </Field>
        <Field label="Guarantee period">
          <IntegerInput
            disabled={disabled}
            value={value.guaranteePeriodYears}
            onChange={(v) => set('guaranteePeriodYears', v ?? 0)}
          />
        </Field>
        <Field label="Commutation factor" hint="£ of lump sum per £1 of pension given up.">
          <MoneyInput
            disabled={disabled}
            value={value.pclsCommutationFactor}
            onChange={(v) => set('pclsCommutationFactor', v ?? 0)}
          />
        </Field>
        <Field label="Maximum PCLS">
          <PercentInput disabled={disabled} value={value.maxPclsPct} onChange={(v) => set('maxPclsPct', v ?? 0)} />
        </Field>
        <Field label="Early retirement reduction" hint="Per year before the unreduced age.">
          <PercentInput
            disabled={disabled}
            value={value.earlyRetirementReductionPct}
            onChange={(v) => set('earlyRetirementReductionPct', v ?? 0)}
          />
        </Field>
        <Field label="Earliest unreduced age">
          <IntegerInput
            disabled={disabled}
            value={value.earliestUnreducedAge ?? null}
            placeholder="NRA"
            onChange={(v) => set('earliestUnreducedAge', v ?? undefined)}
          />
        </Field>
        <Field label="Bridging pension pa">
          <MoneyInput
            disabled={disabled}
            value={value.bridgingPensionAnnual}
            onChange={(v) => set('bridgingPensionAnnual', v ?? 0)}
          />
        </Field>
        <Field label="Funding status">
          <Select
            disabled={disabled}
            value={value.fundingStatus}
            onChange={(e) => set('fundingStatus', e.target.value as SchemeFundingStatus)}
            options={optionsFrom(fundingStatusLabels)}
          />
        </Field>
      </div>

      {errors.length > 0 && (
        <Alert tone="warning" title="Scheme details are incomplete">
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
