import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Loader2, RefreshCw } from 'lucide-react'
import type { AdviserChargesDto, AssumptionOverrides, AssumptionSetDto, ClientSummary } from '@/api/types'
import { errorMessage } from '@/api/client'
import { useClients } from '@/api/queries'
import { cn } from '@/lib/cn'
import { gbp } from '@/lib/format'
import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { Field } from '@/components/form/Field'
import { Select } from '@/components/form/Input'
import { MoneyInput, PercentInput } from '@/components/form/NumericInput'

/** Client chooser used at the start of every analysis wizard. */
export function ClientPicker({
  value,
  onChange,
  label = 'Client',
  disabled,
}: {
  value: string | undefined
  onChange: (clientId: string | undefined, client: ClientSummary | undefined) => void
  label?: string
  disabled?: boolean
}) {
  const { data, isLoading } = useClients({ page: 1, pageSize: 100 })
  const items = data?.items ?? []

  return (
    <Field
      label={label}
      required
      hint={
        items.length === 0 && !isLoading ? (
          <>
            No clients yet — <Link to="/clients" className="underline">add one first</Link>.
          </>
        ) : undefined
      }
    >
      <Select
        value={value ?? ''}
        disabled={disabled || isLoading}
        placeholder={isLoading ? 'Loading clients…' : 'Choose a client'}
        onChange={(e) => {
          const id = e.target.value || undefined
          onChange(id, items.find((c) => c.id === id))
        }}
        options={items.map((c) => ({ value: c.id, label: `${c.fullName} (age ${c.age})` }))}
      />
    </Field>
  )
}

/** The four adviser-charge fields, shared by the switch and DB transfer wizards. */
export function AdviserChargeFields({
  value,
  onChange,
  disabled,
  transferValue,
}: {
  value: AdviserChargesDto
  onChange: (next: AdviserChargesDto) => void
  disabled?: boolean
  /** Used to show the resulting initial charge in pounds. */
  transferValue?: number
}) {
  const initial = transferValue !== undefined ? (transferValue * value.initialPct) / 100 + value.initialAmount : undefined
  return (
    <div>
      <div className="grid gap-3 sm:grid-cols-4">
        <Field label="Initial %">
          <PercentInput
            value={value.initialPct}
            disabled={disabled}
            onChange={(v) => onChange({ ...value, initialPct: v ?? 0 })}
          />
        </Field>
        <Field label="Initial £">
          <MoneyInput
            value={value.initialAmount}
            disabled={disabled}
            onChange={(v) => onChange({ ...value, initialAmount: v ?? 0 })}
          />
        </Field>
        <Field label="Ongoing % pa">
          <PercentInput
            value={value.ongoingPct}
            disabled={disabled}
            onChange={(v) => onChange({ ...value, ongoingPct: v ?? 0 })}
          />
        </Field>
        <Field label="Ongoing £ pa">
          <MoneyInput
            value={value.ongoingAmount}
            disabled={disabled}
            onChange={(v) => onChange({ ...value, ongoingAmount: v ?? 0 })}
          />
        </Field>
      </div>
      {initial !== undefined && (
        <p className="mt-2 text-xs text-fg-muted">
          Initial adviser charge on the transfer value: <span className="font-semibold text-fg">{gbp(initial)}</span>
        </p>
      )}
    </div>
  )
}

/**
 * Assumption set chooser plus the optional per-analysis overrides. Leaving an override blank
 * keeps the set's own figure, which is what the API expects (`undefined`, not zero).
 */
export function AssumptionFields({
  sets,
  assumptionSetId,
  onAssumptionSetChange,
  overrides,
  onOverridesChange,
  fields = ['growthIntermediatePct', 'inflationPct'],
  disabled,
}: {
  sets: AssumptionSetDto[] | undefined
  assumptionSetId: string | undefined
  onAssumptionSetChange: (id: string) => void
  overrides: AssumptionOverrides
  onOverridesChange: (next: AssumptionOverrides) => void
  fields?: (keyof AssumptionOverrides)[]
  disabled?: boolean
}) {
  const selected = sets?.find((s) => s.id === assumptionSetId)
  const labels: Record<keyof AssumptionOverrides, string> = {
    growthLowerPct: 'Lower growth',
    growthIntermediatePct: 'Intermediate growth',
    growthHigherPct: 'Higher growth',
    inflationPct: 'Price inflation',
    earningsGrowthPct: 'Earnings growth',
    statePensionIncreasePct: 'State pension increase',
  }

  return (
    <div className="space-y-3">
      <Field label="Assumption set" required>
        <Select
          value={assumptionSetId ?? ''}
          disabled={disabled}
          placeholder="Choose an assumption set"
          onChange={(e) => onAssumptionSetChange(e.target.value)}
          options={(sets ?? []).map((s) => ({
            value: s.id,
            label: `${s.name}${s.isFcaStandard ? ' (FCA standard)' : ''} — v${s.version}`,
          }))}
        />
      </Field>

      <div className="grid gap-3 sm:grid-cols-3">
        {fields.map((key) => (
          <Field
            key={key}
            label={labels[key]}
            hint={selected ? `Set uses ${selected[key as keyof AssumptionSetDto] as number}%` : undefined}
          >
            <PercentInput
              value={overrides[key] ?? null}
              disabled={disabled}
              placeholder="Use the set"
              onChange={(v) => onOverridesChange({ ...overrides, [key]: v ?? undefined })}
            />
          </Field>
        ))}
      </div>
    </div>
  )
}

/**
 * Status strip above a live preview. It shows what the panel is doing without ever blanking the
 * numbers underneath: stale results stay on screen, dimmed, while a newer request is on the wire.
 */
export function PreviewStatus({
  isPending,
  isFetching,
  error,
  onRefresh,
  calculatedAtUtc,
  engineVersion,
}: {
  isPending: boolean
  isFetching: boolean
  error: unknown
  onRefresh: () => void
  calculatedAtUtc?: string
  engineVersion?: string
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2">
      <p className="flex items-center gap-2 text-xs text-fg-muted" role="status" aria-live="polite">
        {isPending || isFetching ? (
          <>
            <Loader2 className="size-3.5 animate-spin" aria-hidden="true" />
            Recalculating…
          </>
        ) : calculatedAtUtc ? (
          <>
            Calculated {new Date(calculatedAtUtc).toLocaleTimeString('en-GB')}
            {engineVersion && ` · engine ${engineVersion}`}
          </>
        ) : (
          'Waiting for enough detail to calculate.'
        )}
      </p>
      <Button size="sm" variant="ghost" icon={<RefreshCw />} onClick={onRefresh} className="no-print">
        Recalculate
      </Button>
      {error !== undefined && error !== null && (
        <Alert tone="danger" title="The calculation failed" className="w-full">
          {errorMessage(error)}
        </Alert>
      )}
    </div>
  )
}

/** Dims a preview while superseded results are still on screen. */
export function StaleWrapper({ stale, children }: { stale: boolean; children: ReactNode }) {
  return <div className={cn('transition-opacity', stale && 'opacity-60')}>{children}</div>
}
