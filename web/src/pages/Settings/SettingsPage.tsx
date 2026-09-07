import { useEffect, useState } from 'react'
import { Copy, Lock, Save } from 'lucide-react'
import { useAssumptionSets, useCopyAssumptionSet, useIntegrations, useUpdateAssumptionSet } from '@/api/queries'
import { errorMessage, isApiError } from '@/api/client'
import type { AssumptionSetDto, AssumptionSetWrite, ProjectionBasis } from '@/api/types'
import { date, fmtPct } from '@/lib/format'
import { userRoleLabels } from '@/lib/labels'
import { useAuth } from '@/features/auth/AuthProvider'
import { Alert } from '@/components/ui/Alert'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DefinitionList } from '@/components/ui/DefinitionList'
import { Dialog } from '@/components/ui/Dialog'
import { PageHeader } from '@/components/ui/PageHeader'
import { useToast } from '@/components/ui/Toast'
import { Field } from '@/components/form/Field'
import { Input, Select } from '@/components/form/Input'
import { IntegerInput, PercentInput } from '@/components/form/NumericInput'

function toWrite(set: AssumptionSetDto): AssumptionSetWrite {
  const { id: _id, firmId: _firmId, isFcaStandard: _isFcaStandard, version: _version, ...rest } = set
  return rest
}

/**
 * Firm settings. FCA standard assumption sets are read-only by design: to change a figure you
 * copy the set to the firm and edit the copy, which keeps the standard basis auditable. Editing
 * requires the Firm admin role, so a 403 is explained rather than surfaced as a raw error.
 */
export default function SettingsPage() {
  const toast = useToast()
  const { user } = useAuth()
  const { data: sets, isLoading } = useAssumptionSets()
  const { data: integrations } = useIntegrations()
  const copy = useCopyAssumptionSet()

  const [selectedId, setSelectedId] = useState<string | undefined>()
  const [copying, setCopying] = useState<AssumptionSetDto | null>(null)
  const [copyName, setCopyName] = useState('')

  useEffect(() => {
    if (!selectedId && sets?.length) setSelectedId(sets[0].id)
  }, [sets, selectedId])

  const selected = sets?.find((s) => s.id === selectedId)

  return (
    <>
      <PageHeader
        title="Settings"
        description="Assumption sets, integrations and the account you are signed in with."
      />

      <div className="grid gap-4 lg:grid-cols-[280px_minmax(0,1fr)]">
        <Card title="Assumption sets" flush description="FCA standard sets are read-only.">
          {isLoading ? (
            <p className="p-4 text-sm text-fg-muted">Loading…</p>
          ) : (
            <ul className="divide-y divide-border">
              {(sets ?? []).map((set) => (
                <li key={set.id}>
                  <button
                    type="button"
                    onClick={() => setSelectedId(set.id)}
                    aria-current={set.id === selectedId ? 'true' : undefined}
                    className={`flex w-full flex-col gap-1 p-3 text-left text-sm hover:bg-surface-muted ${
                      set.id === selectedId ? 'bg-accent-50' : ''
                    }`}
                  >
                    <span className="font-medium text-fg">{set.name}</span>
                    <span className="flex flex-wrap items-center gap-1.5">
                      {set.isFcaStandard ? (
                        <Badge tone="primary" size="sm">
                          FCA standard
                        </Badge>
                      ) : (
                        <Badge tone="accent" size="sm">
                          Firm
                        </Badge>
                      )}
                      <span className="text-xs text-fg-subtle">
                        v{set.version} · {set.taxYear}
                      </span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <div className="space-y-4">
          {selected && (
            <AssumptionSetEditor
              key={selected.id}
              set={selected}
              onCopy={() => {
                setCopying(selected)
                setCopyName(`${selected.name} (firm copy)`)
              }}
            />
          )}

          <Card title="Integrations" description="Back-office and market data connectors configured for this firm.">
            <ul className="space-y-2">
              {(integrations ?? []).map((integration) => (
                <li
                  key={integration.connector}
                  className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border p-3"
                >
                  <span className="text-sm font-medium text-fg">{integration.connector}</span>
                  <span className="flex items-center gap-2">
                    <Badge tone={integration.mode === 'Live' ? 'success' : integration.mode === 'Sandbox' ? 'warning' : 'neutral'} size="sm">
                      {integration.mode}
                    </Badge>
                    <span className="text-xs text-fg-subtle">
                      {integration.lastSyncUtc ? `Last sync ${date(integration.lastSyncUtc)}` : 'Never synced'}
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          </Card>

          <Card title="Your account">
            <DefinitionList
              items={[
                { label: 'Name', value: user?.displayName ?? '—' },
                { label: 'Email', value: user?.email ?? '—' },
                { label: 'Role', value: user ? userRoleLabels[user.role] : '—' },
                { label: 'Firm', value: user?.firmName ?? '—' },
              ]}
            />
          </Card>
        </div>
      </div>

      <Dialog
        open={copying !== null}
        onClose={() => setCopying(null)}
        title="Copy to the firm"
        description="A firm copy can be edited; the standard set stays untouched."
        footer={
          <>
            <Button variant="ghost" onClick={() => setCopying(null)}>
              Cancel
            </Button>
            <Button
              loading={copy.isPending}
              onClick={async () => {
                if (!copying) return
                try {
                  const created = await copy.mutateAsync({ id: copying.id, body: { name: copyName } })
                  setSelectedId(created.id)
                  setCopying(null)
                  toast.success('Copied to the firm', created.name)
                } catch (e) {
                  toast.error(
                    'Could not copy',
                    isApiError(e) && e.isForbidden
                      ? 'Copying an assumption set needs the Firm admin role.'
                      : errorMessage(e),
                  )
                }
              }}
            >
              Create copy
            </Button>
          </>
        }
      >
        <Field label="Name for the copy" required>
          <Input value={copyName} onChange={(e) => setCopyName(e.target.value)} />
        </Field>
      </Dialog>
    </>
  )
}

function AssumptionSetEditor({ set, onCopy }: { set: AssumptionSetDto; onCopy: () => void }) {
  const toast = useToast()
  const update = useUpdateAssumptionSet(set.id)
  const [draft, setDraft] = useState<AssumptionSetWrite>(() => toWrite(set))
  const readOnly = set.isFcaStandard

  useEffect(() => setDraft(toWrite(set)), [set])

  const field = <K extends keyof AssumptionSetWrite>(key: K, value: AssumptionSetWrite[K]) =>
    setDraft((d) => ({ ...d, [key]: value }))

  const save = async () => {
    try {
      await update.mutateAsync(draft)
      toast.success('Assumption set saved', draft.name)
    } catch (e) {
      toast.error(
        'Could not save',
        isApiError(e) && e.isForbidden
          ? 'Editing an assumption set needs the Firm admin role.'
          : errorMessage(e),
      )
    }
  }

  const rates: [keyof AssumptionSetWrite, string][] = [
    ['growthLowerPct', 'Lower growth rate'],
    ['growthIntermediatePct', 'Intermediate growth rate'],
    ['growthHigherPct', 'Higher growth rate'],
    ['inflationPct', 'Price inflation (CPI)'],
    ['rpiInflationPct', 'RPI inflation'],
    ['earningsGrowthPct', 'Earnings growth'],
    ['chargeInflationPct', 'Charge inflation'],
    ['preRetirementProductChargePct', 'Pre-retirement product charge'],
    ['annuityExpenseLoadingPct', 'Annuity expense loading'],
    ['statePensionIncreasePct', 'State pension increase'],
  ]

  return (
    <Card
      title={set.name}
      description={`Tax year ${set.taxYear}, version ${set.version}. Market inputs as at ${date(set.marketInputs.asAt)}.`}
      actions={
        <>
          <Button size="sm" variant="outline" icon={<Copy />} onClick={onCopy}>
            Copy to firm
          </Button>
          <Button size="sm" icon={<Save />} onClick={save} loading={update.isPending} disabled={readOnly}>
            Save
          </Button>
        </>
      }
    >
      {readOnly && (
        <Alert tone="info" title="Read-only" className="mb-4">
          <span className="flex items-center gap-1.5">
            <Lock className="size-3.5" aria-hidden="true" />
            This is an FCA standard set. Copy it to the firm to change any figure.
          </span>
        </Alert>
      )}

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Field label="Name" required>
          <Input value={draft.name} disabled={readOnly} onChange={(e) => field('name', e.target.value)} />
        </Field>
        <Field label="Tax year">
          <Input value={draft.taxYear} disabled={readOnly} onChange={(e) => field('taxYear', e.target.value)} />
        </Field>
        <Field label="Projection basis">
          <Select
            value={draft.projectionBasis}
            disabled={readOnly}
            onChange={(e) => field('projectionBasis', e.target.value as ProjectionBasis)}
            options={[
              { value: 'real', label: "Real (today's money)" },
              { value: 'nominal', label: 'Nominal' },
            ]}
          />
        </Field>
        {rates.map(([key, label]) => (
          <Field key={key} label={label}>
            <PercentInput
              value={draft[key] as number}
              disabled={readOnly}
              onChange={(v) => field(key, (v ?? 0) as AssumptionSetWrite[typeof key])}
            />
          </Field>
        ))}
        <Field label="Spouse age gap (years)">
          <IntegerInput
            value={draft.spouseAgeGapYears}
            disabled={readOnly}
            onChange={(v) => field('spouseAgeGapYears', v ?? 0)}
          />
        </Field>
        <Field label="Mortality basis">
          <Input value={draft.mortalityBasis} disabled={readOnly} onChange={(e) => field('mortalityBasis', e.target.value)} />
        </Field>
      </div>

      <h3 className="mt-6 section-title">Market inputs</h3>
      <DefinitionList
        cols={3}
        className="mt-2"
        items={[
          { label: 'Gilt yield, up to 5 years', value: fmtPct(set.marketInputs.giltYieldUpTo5Pct) },
          { label: 'Gilt yield, 5–10 years', value: fmtPct(set.marketInputs.giltYield5To10Pct) },
          { label: 'Gilt yield, 10–15 years', value: fmtPct(set.marketInputs.giltYield10To15Pct) },
          { label: 'Gilt yield, over 15 years', value: fmtPct(set.marketInputs.giltYieldOver15Pct) },
          { label: 'TVC annuity rate — RPI linked', value: fmtPct(set.marketInputs.tvcAnnuityRateRpiLinkedPct) },
          { label: 'TVC annuity rate — level', value: fmtPct(set.marketInputs.tvcAnnuityRateLevelPct) },
          { label: 'COBS 13 Y%', value: fmtPct(set.marketInputs.cobs13YPct) },
          { label: 'As at', value: date(set.marketInputs.asAt) },
        ]}
      />
    </Card>
  )
}
