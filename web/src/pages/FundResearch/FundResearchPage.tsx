import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { ExternalLink, Scale, X } from 'lucide-react'
import { useFunds } from '@/api/queries'
import { errorMessage } from '@/api/client'
import type { FundDto } from '@/api/types'
import { weightedOcfPct } from '@/lib/charges'
import { useDebouncedValue } from '@/lib/hooks'
import { date, fmtPct, num } from '@/lib/format'
import { fundTypeLabels } from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Badge } from '@/components/ui/Badge'
import { Button, IconButton } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { DefinitionList } from '@/components/ui/DefinitionList'
import { EmptyState } from '@/components/ui/EmptyState'
import { PageHeader } from '@/components/ui/PageHeader'
import { Field } from '@/components/form/Field'
import { Input, Select } from '@/components/form/Input'
import { PercentInput } from '@/components/form/NumericInput'
import { ChartFrame } from '@/components/charts/ChartFrame'
import { AllocationDonut } from '@/components/charts/Charts'

const PAGE_SIZE = 25
const MAX_BASKET = 6

const SECTORS = [
  'Global',
  'North America',
  'UK All Companies',
  'UK Gilts',
  'Mixed Investment 20-60% Shares',
  'Mixed Investment 40-85% Shares',
  'Property Other',
]

/**
 * Fund research: a filtered table, a detail drawer with the allocation donut, and a comparison
 * basket of up to six funds whose weighted OCF is worked out in the browser (the API stays the
 * source of truth for anything that reaches a report).
 */
export default function FundResearchPage() {
  const [params, setParams] = useSearchParams()
  const [selected, setSelected] = useState<FundDto | null>(null)
  const [basket, setBasket] = useState<FundDto[]>([])

  const search = params.get('search') ?? ''
  const sector = params.get('sector') ?? ''
  const maxOcf = params.get('maxOcfPct')
  const page = Number(params.get('page') ?? 1)
  const debouncedSearch = useDebouncedValue(search, 300)

  const { data, isLoading, error } = useFunds({
    search: debouncedSearch || undefined,
    sector: sector || undefined,
    maxOcfPct: maxOcf ? Number(maxOcf) : undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    if (key !== 'page') next.delete('page')
    setParams(next, { replace: true })
  }

  const toggleBasket = (fund: FundDto) => {
    setBasket((current) =>
      current.some((f) => f.isin === fund.isin)
        ? current.filter((f) => f.isin !== fund.isin)
        : current.length >= MAX_BASKET
          ? current
          : [...current, fund],
    )
  }

  const columns: Column<FundDto>[] = [
    {
      id: 'name',
      header: 'Fund',
      cell: (row) => (
        <button
          type="button"
          onClick={() => setSelected(row)}
          className="text-left font-medium text-primary-700 hover:underline"
        >
          {row.name}
        </button>
      ),
      sortValue: (row) => row.name,
    },
    { id: 'manager', header: 'Manager', cell: (row) => row.managerName, sortValue: (row) => row.managerName, hideBelow: 'md' },
    { id: 'type', header: 'Type', cell: (row) => fundTypeLabels[row.type], sortValue: (row) => row.type, hideBelow: 'lg' },
    { id: 'sector', header: 'IA sector', cell: (row) => row.iaSector ?? '—', sortValue: (row) => row.iaSector, hideBelow: 'lg' },
    { id: 'ocf', header: 'OCF', align: 'right', cell: (row) => fmtPct(row.ocfPct), sortValue: (row) => row.ocfPct },
    {
      id: 'tc',
      header: 'Transaction costs',
      align: 'right',
      cell: (row) => fmtPct(row.transactionCostsPct),
      sortValue: (row) => row.transactionCostsPct,
      hideBelow: 'lg',
    },
    {
      id: 'equity',
      header: 'Equity',
      align: 'right',
      cell: (row) => fmtPct(row.assetAllocation.equityPct, 0),
      sortValue: (row) => row.assetAllocation.equityPct,
      hideBelow: 'md',
    },
    { id: 'srri', header: 'SRRI', align: 'right', cell: (row) => row.srri ?? '—', sortValue: (row) => row.srri, hideBelow: 'sm' },
    {
      id: 'r3y',
      header: '3-year return',
      align: 'right',
      cell: (row) => fmtPct(row.statistics.return3YPct, 1),
      sortValue: (row) => row.statistics.return3YPct,
      hideBelow: 'lg',
    },
    {
      id: 'compare',
      header: <span className="sr-only">Compare</span>,
      align: 'right',
      cell: (row) => {
        const inBasket = basket.some((f) => f.isin === row.isin)
        return (
          <Button
            size="sm"
            variant={inBasket ? 'secondary' : 'outline'}
            onClick={() => toggleBasket(row)}
            disabled={!inBasket && basket.length >= MAX_BASKET}
            aria-pressed={inBasket}
          >
            {inBasket ? 'In basket' : 'Compare'}
          </Button>
        )
      },
    },
  ]

  const basketOcf = weightedOcfPct(basket.map((f) => ({ weightPct: 100 / Math.max(basket.length, 1), ocfPct: f.ocfPct })))

  return (
    <>
      <PageHeader
        title="Fund research"
        description="Search the catalogue, inspect a fund's allocation, and build a comparison basket of up to six."
      />

      {error && (
        <Alert tone="danger" title="Funds could not be loaded" className="mb-4">
          {errorMessage(error)}
        </Alert>
      )}

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
        <Card flush>
          <div className="grid gap-3 border-b border-border p-4 sm:grid-cols-3">
            <Field label="Search">
              <Input
                type="search"
                value={search}
                placeholder="Name, manager or ISIN"
                onChange={(e) => setParam('search', e.target.value)}
              />
            </Field>
            <Field label="IA sector">
              <Select
                value={sector}
                placeholder="All sectors"
                onChange={(e) => setParam('sector', e.target.value)}
                options={SECTORS.map((s) => ({ value: s, label: s }))}
              />
            </Field>
            <Field label="Maximum OCF">
              <PercentInput
                value={maxOcf ? Number(maxOcf) : null}
                placeholder="No limit"
                onChange={(v) => setParam('maxOcfPct', v === null ? '' : String(v))}
              />
            </Field>
          </div>

          <DataTable
            columns={columns}
            rows={data?.items}
            getRowId={(row) => row.isin}
            isLoading={isLoading}
            caption="Funds in the catalogue"
            initialSort={{ columnId: 'ocf', direction: 'asc' }}
            selectedIds={new Set(basket.map((f) => f.isin))}
            emptyTitle="No funds match those filters"
            emptyDescription="The catalogue search is case sensitive on the live API — try 'Vanguard' rather than 'vanguard'."
            emptyAction={
              <Button variant="outline" onClick={() => setParams({}, { replace: true })}>
                Clear filters
              </Button>
            }
          />

          {data && data.total > PAGE_SIZE && (
            <div className="border-t border-border px-4">
              <Pagination
                page={data.page}
                pageSize={data.pageSize}
                total={data.total}
                onPageChange={(next) => setParam('page', String(next))}
              />
            </div>
          )}
        </Card>

        <div className="space-y-4">
          <Card
            title="Comparison basket"
            description={`Up to ${MAX_BASKET} funds, equally weighted.`}
            actions={
              basket.length > 0 ? (
                <Button size="sm" variant="ghost" onClick={() => setBasket([])}>
                  Clear
                </Button>
              ) : undefined
            }
          >
            {basket.length === 0 ? (
              <EmptyState
                icon={<Scale />}
                title="Nothing to compare yet"
                description="Press Compare on a fund to add it to the basket."
              />
            ) : (
              <>
                <ul className="space-y-2">
                  {basket.map((fund) => (
                    <li key={fund.isin} className="flex items-start justify-between gap-2 rounded-md border border-border p-2">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-fg">{fund.name}</p>
                        <p className="text-xs text-fg-muted">
                          OCF {fmtPct(fund.ocfPct)} · equity {fmtPct(fund.assetAllocation.equityPct, 0)}
                        </p>
                      </div>
                      <IconButton size="sm" label={`Remove ${fund.name} from the basket`} onClick={() => toggleBasket(fund)}>
                        <X />
                      </IconButton>
                    </li>
                  ))}
                </ul>
                <div className="mt-3 rounded-lg bg-surface-muted p-3">
                  <p className="text-xs font-medium uppercase tracking-wide text-fg-subtle">Weighted OCF</p>
                  <p className="mt-0.5 text-2xl font-semibold tabular-nums text-accent-600">{fmtPct(basketOcf, 3)}</p>
                  <p className="mt-1 text-xs text-fg-muted">
                    Equally weighted across {basket.length} {basket.length === 1 ? 'fund' : 'funds'}.
                  </p>
                </div>
              </>
            )}
          </Card>

          {selected && <FundDetail fund={selected} onClose={() => setSelected(null)} />}
        </div>
      </div>
    </>
  )
}

function FundDetail({ fund, onClose }: { fund: FundDto; onClose: () => void }) {
  const allocation = [
    { name: 'Equity', value: fund.assetAllocation.equityPct },
    { name: 'Fixed interest', value: fund.assetAllocation.fixedInterestPct },
    { name: 'Property', value: fund.assetAllocation.propertyPct },
    { name: 'Cash', value: fund.assetAllocation.cashPct },
    { name: 'Alternatives', value: fund.assetAllocation.alternativesPct },
  ]

  return (
    <Card
      title={fund.name}
      description={`${fund.managerName} · ${fund.isin}`}
      actions={
        <IconButton size="sm" label="Close fund details" onClick={onClose}>
          <X />
        </IconButton>
      }
    >
      <ChartFrame
        title="Asset allocation"
        description={`How ${fund.name} is invested, by percentage of the fund.`}
        height={240}
        table={
          <table className="table table-dense">
            <caption className="sr-only">Asset allocation</caption>
            <thead>
              <tr>
                <th scope="col">Asset class</th>
                <th scope="col" className="text-right">
                  Share
                </th>
              </tr>
            </thead>
            <tbody>
              {allocation.map((slice) => (
                <tr key={slice.name}>
                  <th scope="row" className="font-normal">
                    {slice.name}
                  </th>
                  <td className="num">{fmtPct(slice.value, 1)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        }
      >
        <AllocationDonut data={allocation} />
      </ChartFrame>

      <DefinitionList
        className="mt-4"
        items={[
          { label: 'Type', value: fundTypeLabels[fund.type] },
          { label: 'IA sector', value: fund.iaSector ?? '—' },
          { label: 'OCF', value: fmtPct(fund.ocfPct) },
          { label: 'Transaction costs', value: fmtPct(fund.transactionCostsPct) },
          { label: 'SRRI', value: fund.srri ?? '—' },
          { label: 'Share class', value: fund.shareClass ?? '—' },
          { label: '1-year return', value: fmtPct(fund.statistics.return1YPct, 1) },
          { label: '3-year return', value: fmtPct(fund.statistics.return3YPct, 1) },
          { label: '5-year return', value: fmtPct(fund.statistics.return5YPct, 1) },
          { label: '3-year volatility', value: fmtPct(fund.statistics.volatility3YPct, 1) },
          { label: '3-year Sharpe', value: fund.statistics.sharpe3Y != null ? num(fund.statistics.sharpe3Y, 2) : '—' },
          { label: 'Max drawdown (3y)', value: fmtPct(fund.statistics.maxDrawdown3YPct, 1) },
          { label: 'Yield', value: fmtPct(fund.statistics.yieldPct, 2) },
          {
            label: 'Morningstar',
            value: fund.statistics.morningstarRating ? `${fund.statistics.morningstarRating} star` : '—',
          },
          { label: 'Medalist rating', value: fund.statistics.medalistRating ?? '—' },
          { label: 'Data as at', value: date(fund.asAt) },
        ]}
      />

      <div className="mt-4 flex flex-wrap items-center gap-2">
        {fund.statistics.medalistRating && <Badge tone="accent">{fund.statistics.medalistRating}</Badge>}
        {fund.factsheetUrl && (
          <a
            href={fund.factsheetUrl}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1 text-sm text-primary-700 underline"
          >
            Factsheet
            <ExternalLink className="size-3.5" aria-hidden="true" />
          </a>
        )}
      </div>
    </Card>
  )
}
