import { useMemo, useState } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import type { FundDto, HoldingDto } from '@/api/types'
import { useFunds } from '@/api/queries'
import { totalWeightPct, weightedOcfPct } from '@/lib/charges'
import { fmtPct } from '@/lib/format'
import { Alert } from '@/components/ui/Alert'
import { Button, IconButton } from '@/components/ui/Button'
import { EmptyState } from '@/components/ui/EmptyState'
import { Field } from '@/components/form/Field'
import { Input, Select } from '@/components/form/Input'
import { PercentInput } from '@/components/form/NumericInput'

export interface HoldingsEditorProps {
  holdings: HoldingDto[]
  onChange: (holdings: HoldingDto[]) => void
  disabled?: boolean
  /** Show the weighted OCF summary line. */
  showOcf?: boolean
}

/** Total weight is valid when it is 100% (to 2 dp) and there is at least one holding. */
export function holdingsWeightError(holdings: HoldingDto[]): string | null {
  if (holdings.length === 0) return 'Add at least one holding.'
  const total = totalWeightPct(holdings)
  if (total !== 100) {
    return `Weights must total 100%. They currently total ${total}% (${total > 100 ? 'over' : 'under'} by ${Math.abs(Math.round((100 - total) * 100) / 100)}%).`
  }
  return null
}

/**
 * Fund picker plus weights. Weights are percentage numbers per the contract and must total 100
 * before a calculation is allowed; the running total is shown as the adviser types so the error
 * is never a surprise at submit time.
 */
export function HoldingsEditor({ holdings, onChange, disabled, showOcf = true }: HoldingsEditorProps) {
  const [search, setSearch] = useState('')
  const [selectedIsin, setSelectedIsin] = useState('')
  const { data: fundPage, isLoading } = useFunds({ search: search || undefined, page: 1, pageSize: 50 })

  const available = useMemo(
    () => (fundPage?.items ?? []).filter((f) => !holdings.some((h) => h.isin === f.isin)),
    [fundPage, holdings],
  )

  const total = totalWeightPct(holdings)
  const error = holdingsWeightError(holdings)
  const ocf = weightedOcfPct(holdings)

  const addFund = (fund: FundDto) => {
    const remaining = Math.max(0, Math.round((100 - total) * 100) / 100)
    onChange([
      ...holdings,
      { name: fund.name, isin: fund.isin, fundId: fund.id, ocfPct: fund.ocfPct, weightPct: remaining },
    ])
    setSelectedIsin('')
  }

  const spreadEvenly = () => {
    if (holdings.length === 0) return
    const each = Math.floor((100 / holdings.length) * 100) / 100
    const next = holdings.map((h) => ({ ...h, weightPct: each }))
    next[next.length - 1].weightPct = Math.round((100 - each * (holdings.length - 1)) * 100) / 100
    onChange(next)
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Search the fund catalogue" className="min-w-48 flex-1">
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Name, manager or ISIN"
            disabled={disabled}
          />
        </Field>
        <Field label="Fund" className="min-w-56 flex-1">
          <Select
            value={selectedIsin}
            onChange={(e) => setSelectedIsin(e.target.value)}
            placeholder={isLoading ? 'Loading funds…' : 'Choose a fund to add'}
            disabled={disabled}
            options={available.map((f) => ({
              value: f.isin,
              label: `${f.name} — OCF ${f.ocfPct}%`,
            }))}
          />
        </Field>
        <Button
          icon={<Plus />}
          disabled={disabled || !selectedIsin}
          onClick={() => {
            const fund = available.find((f) => f.isin === selectedIsin)
            if (fund) addFund(fund)
          }}
        >
          Add holding
        </Button>
      </div>

      {holdings.length === 0 ? (
        <EmptyState
          title="No holdings yet"
          description="Search the catalogue above and add the funds the proposed product will hold."
        />
      ) : (
        <div className="overflow-x-auto">
          <table className="table">
            <caption className="sr-only">Proposed holdings and weights</caption>
            <thead>
              <tr>
                <th scope="col">Fund</th>
                <th scope="col" className="hidden sm:table-cell">
                  ISIN
                </th>
                <th scope="col" className="text-right">
                  OCF
                </th>
                <th scope="col" className="text-right" style={{ width: 140 }}>
                  Weight
                </th>
                <th scope="col">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {holdings.map((holding, index) => (
                <tr key={holding.isin ?? `${holding.name}-${index}`}>
                  <td className="font-medium text-fg">{holding.name}</td>
                  <td className="hidden font-mono text-xs text-fg-muted sm:table-cell">{holding.isin ?? '—'}</td>
                  <td className="num">{fmtPct(holding.ocfPct, 2)}</td>
                  <td>
                    <PercentInput
                      aria-label={`Weight for ${holding.name}`}
                      sizing="sm"
                      value={holding.weightPct}
                      disabled={disabled}
                      onChange={(value) => {
                        const next = [...holdings]
                        next[index] = { ...next[index], weightPct: value ?? 0 }
                        onChange(next)
                      }}
                    />
                  </td>
                  <td className="text-right">
                    <IconButton
                      size="sm"
                      label={`Remove ${holding.name}`}
                      disabled={disabled}
                      onClick={() => onChange(holdings.filter((_, i) => i !== index))}
                    >
                      <Trash2 />
                    </IconButton>
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={2} className="text-xs font-semibold uppercase tracking-wide text-fg-subtle">
                  Total
                </td>
                <td className="num text-xs">{showOcf ? fmtPct(ocf, 3) : ''}</td>
                <td className={`num font-semibold ${total === 100 ? 'text-success-600' : 'text-danger-600'}`}>
                  {total}%
                </td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>
      )}

      <div className="flex flex-wrap items-center gap-2">
        <Button size="sm" variant="outline" onClick={spreadEvenly} disabled={disabled || holdings.length === 0}>
          Spread evenly
        </Button>
        {showOcf && holdings.length > 0 && (
          <p className="text-xs text-fg-muted">
            Weighted OCF <span className="font-semibold text-fg">{fmtPct(ocf, 3)}</span>
          </p>
        )}
      </div>

      {error && (
        <Alert tone="warning" title="Holdings need attention">
          {error}
        </Alert>
      )}
    </div>
  )
}
