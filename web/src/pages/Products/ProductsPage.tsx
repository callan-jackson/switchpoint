import { Link, useSearchParams } from 'react-router'
import { useProducts } from '@/api/queries'
import { errorMessage } from '@/api/client'
import type { ProductSummary } from '@/api/types'
import { useDebouncedValue } from '@/lib/hooks'
import { date, fmtPct, gbp } from '@/lib/format'
import { fundUniverseLabels } from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Badge, DataQualityBadge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { PageHeader } from '@/components/ui/PageHeader'
import { Field } from '@/components/form/Field'
import { Input, Select } from '@/components/form/Input'

const WRAPPERS = ['Sipp', 'PersonalPension', 'Isa', 'GeneralInvestmentAccount', 'Drawdown', 'Bond']

const columns: Column<ProductSummary>[] = [
  {
    id: 'name',
    header: 'Product',
    cell: (row) => (
      <div className="min-w-0">
        <Link to={`/products/${row.id}`} className="font-medium text-primary-700 hover:underline">
          {row.name}
        </Link>
        <p className="truncate text-xs text-fg-muted">{row.providerName}</p>
      </div>
    ),
    sortValue: (row) => row.name,
  },
  {
    id: 'wrappers',
    header: 'Wrappers',
    cell: (row) => (
      <div className="flex flex-wrap gap-1">
        {row.wrapperTypes.map((w) => (
          <Badge key={w} size="sm">
            {w}
          </Badge>
        ))}
      </div>
    ),
    hideBelow: 'lg',
  },
  {
    id: 'at100k',
    header: 'Effective charge at £100k',
    align: 'right',
    cell: (row) => fmtPct(row.effectiveChargePctAt100k, 3),
    sortValue: (row) => row.effectiveChargePctAt100k,
  },
  {
    id: 'at500k',
    header: 'Effective charge at £500k',
    align: 'right',
    cell: (row) => fmtPct(row.effectiveChargePctAt500k, 3),
    sortValue: (row) => row.effectiveChargePctAt500k,
  },
  {
    id: 'minimum',
    header: 'Minimum',
    align: 'right',
    cell: (row) => gbp(row.minimumInvestment),
    sortValue: (row) => row.minimumInvestment,
    hideBelow: 'md',
  },
  {
    id: 'universe',
    header: 'Fund universe',
    cell: (row) => fundUniverseLabels[row.fundUniverse],
    sortValue: (row) => row.fundUniverse,
    hideBelow: 'lg',
  },
  {
    id: 'quality',
    header: 'Data',
    cell: (row) => (
      <div className="flex flex-wrap items-center gap-1.5">
        <DataQualityBadge quality={row.dataQuality} />
        <span className="text-xs text-fg-subtle">as at {date(row.asAt)}</span>
      </div>
    ),
    sortValue: (row) => row.dataQuality,
  },
]

/** Charge comparison across the product catalogue, at the two values advisers quote most. */
export default function ProductsPage() {
  const [params, setParams] = useSearchParams()
  const search = params.get('search') ?? ''
  const wrapper = params.get('wrapper') ?? ''
  const debouncedSearch = useDebouncedValue(search, 300)

  const { data, isLoading, error } = useProducts({
    search: debouncedSearch || undefined,
    wrapper: wrapper || undefined,
  })

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    setParams(next, { replace: true })
  }

  return (
    <>
      <PageHeader
        title="Products and charges"
        description="Effective charges at £100,000 and £500,000, with the charge versions and sources behind them."
      />

      {error && (
        <Alert tone="danger" title="Products could not be loaded" className="mb-4">
          {errorMessage(error)}
        </Alert>
      )}

      <Alert tone="info" title="Where these figures come from" className="mb-4">
        Charge data is captured from published rate cards and marked with a data-quality badge.
        Anything marked indicative or placeholder should be checked against the provider's own
        documentation before it reaches a client.
      </Alert>

      <Card flush>
        <div className="grid gap-3 border-b border-border p-4 sm:grid-cols-2">
          <Field label="Search">
            <Input
              type="search"
              value={search}
              placeholder="Product or provider"
              onChange={(e) => setParam('search', e.target.value)}
            />
          </Field>
          <Field label="Wrapper">
            <Select
              value={wrapper}
              placeholder="All wrappers"
              onChange={(e) => setParam('wrapper', e.target.value)}
              options={WRAPPERS.map((w) => ({ value: w, label: w }))}
            />
          </Field>
        </div>

        <DataTable
          columns={columns}
          rows={data}
          getRowId={(row) => row.id}
          isLoading={isLoading}
          caption="Products and their effective charges"
          initialSort={{ columnId: 'at100k', direction: 'asc' }}
          emptyTitle="No products match those filters"
          emptyAction={
            <Button variant="outline" onClick={() => setParams({}, { replace: true })}>
              Clear filters
            </Button>
          }
        />
      </Card>
    </>
  )
}
