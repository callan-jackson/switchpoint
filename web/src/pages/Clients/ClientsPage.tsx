import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { UserPlus } from 'lucide-react'
import { useClients } from '@/api/queries'
import { errorMessage } from '@/api/client'
import type { ClientSummary } from '@/api/types'
import { useDebouncedValue } from '@/lib/hooks'
import { date, gbp, num } from '@/lib/format'
import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { PageHeader } from '@/components/ui/PageHeader'
import { Field } from '@/components/form/Field'
import { Input } from '@/components/form/Input'
import { ClientFormDialog } from './ClientFormDialog'

const PAGE_SIZE = 25

const columns: Column<ClientSummary>[] = [
  {
    id: 'name',
    header: 'Client',
    cell: (row) => (
      <Link to={`/clients/${row.id}`} className="font-medium text-primary-700 hover:underline">
        {row.fullName}
      </Link>
    ),
    sortValue: (row) => row.fullName,
  },
  {
    id: 'dob',
    header: 'Date of birth',
    cell: (row) => date(row.dateOfBirth),
    sortValue: (row) => row.dateOfBirth,
    hideBelow: 'md',
  },
  { id: 'age', header: 'Age', align: 'right', cell: (row) => row.age, sortValue: (row) => row.age },
  {
    id: 'risk',
    header: 'Risk',
    align: 'right',
    cell: (row) => `${row.riskProfile}/7`,
    sortValue: (row) => row.riskProfile,
    hideBelow: 'sm',
  },
  {
    id: 'schemes',
    header: 'Arrangements',
    align: 'right',
    cell: (row) => num(row.schemeCount),
    sortValue: (row) => row.schemeCount,
    hideBelow: 'sm',
  },
  {
    id: 'value',
    header: 'Pension value',
    align: 'right',
    cell: (row) => gbp(row.totalPensionValue),
    sortValue: (row) => row.totalPensionValue,
  },
  {
    id: 'email',
    header: 'Email',
    cell: (row) => row.email ?? '—',
    sortValue: (row) => row.email,
    hideBelow: 'lg',
  },
]

export default function ClientsPage() {
  const [params, setParams] = useSearchParams()
  const navigate = useNavigate()
  const [adding, setAdding] = useState(false)

  const search = params.get('search') ?? ''
  const page = Number(params.get('page') ?? 1)
  const debouncedSearch = useDebouncedValue(search, 300)

  const { data, isLoading, isFetching, error } = useClients({
    search: debouncedSearch || undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    if (key === 'search') next.delete('page')
    setParams(next, { replace: true })
  }

  return (
    <>
      <PageHeader
        title="Clients"
        description="Everyone on the firm's book, with the arrangements SwitchPoint holds for them."
        actions={
          <Button icon={<UserPlus />} onClick={() => setAdding(true)}>
            Add client
          </Button>
        }
      />

      {error && (
        <Alert tone="danger" title="Clients could not be loaded" className="mb-4">
          {errorMessage(error)}
        </Alert>
      )}

      <Card flush>
        <div className="border-b border-border p-4">
          <Field label="Search" className="max-w-md" labelAside={isFetching ? <span className="text-xs text-fg-subtle">Searching…</span> : undefined}>
            <Input
              type="search"
              value={search}
              placeholder="Name or email address"
              onChange={(e) => setParam('search', e.target.value)}
            />
          </Field>
        </div>

        <DataTable
          columns={columns}
          rows={data?.items}
          getRowId={(row) => row.id}
          isLoading={isLoading}
          caption="Clients"
          onRowClick={(row) => navigate(`/clients/${row.id}`)}
          emptyTitle={search ? 'No clients match that search' : 'No clients yet'}
          emptyDescription={
            search
              ? 'Try a shorter search term, or clear the box to see everyone.'
              : 'Add a client to start recording arrangements and running analyses.'
          }
          emptyAction={
            search ? (
              <Button variant="outline" onClick={() => setParam('search', '')}>
                Clear search
              </Button>
            ) : (
              <Button icon={<UserPlus />} onClick={() => setAdding(true)}>
                Add client
              </Button>
            )
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

      <ClientFormDialog
        open={adding}
        onClose={() => setAdding(false)}
        onSaved={(client) => {
          setAdding(false)
          navigate(`/clients/${client.id}`)
        }}
      />
    </>
  )
}
