import { Link } from 'react-router'
import { BarChart3, FileText, LineChart, PiggyBank, Search, Users } from 'lucide-react'
import { useDashboardSummary } from '@/api/queries'
import { errorMessage } from '@/api/client'
import type { AnalysisSummary } from '@/api/types'
import { dateTime, num } from '@/lib/format'
import { analysisKindLabels, analysisPath } from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { ButtonLink } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { PageHeader } from '@/components/ui/PageHeader'
import { StatTile } from '@/components/ui/StatTile'
import { StatusBadge } from '@/components/ui/Badge'

const columns: Column<AnalysisSummary>[] = [
  {
    id: 'title',
    header: 'Analysis',
    cell: (row) => (
      <Link to={analysisPath(row.kind, row.id)} className="font-medium text-primary-700 hover:underline">
        {row.title}
      </Link>
    ),
    sortValue: (row) => row.title,
  },
  {
    id: 'kind',
    header: 'Type',
    cell: (row) => analysisKindLabels[row.kind],
    sortValue: (row) => row.kind,
    hideBelow: 'sm',
  },
  { id: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} />, sortValue: (row) => row.status },
  {
    id: 'version',
    header: 'Version',
    align: 'right',
    cell: (row) => row.version,
    sortValue: (row) => row.version,
    hideBelow: 'md',
  },
  {
    id: 'updated',
    header: 'Last updated',
    cell: (row) => dateTime(row.updatedAtUtc),
    sortValue: (row) => row.updatedAtUtc,
    hideBelow: 'sm',
  },
]

export default function DashboardPage() {
  const { data, isLoading, error } = useDashboardSummary()

  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Where the firm's advice work stands today."
        actions={
          <>
            <ButtonLink to="/pension-switch" variant="outline" icon={<PiggyBank />}>
              New switch analysis
            </ButtonLink>
            <ButtonLink to="/clients" icon={<Users />}>
              Clients
            </ButtonLink>
          </>
        }
      />

      {error && (
        <Alert tone="danger" title="The dashboard could not load" className="mb-4">
          {errorMessage(error)}
        </Alert>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile
          label="Clients"
          value={num(data?.clients)}
          hint="On the firm's book"
          icon={<Users />}
          loading={isLoading}
        />
        <StatTile
          label="Analyses in progress"
          value={num(data?.analysesInProgress)}
          hint="Draft or calculated, not yet locked"
          icon={<LineChart />}
          tone="accent"
          loading={isLoading}
        />
        <StatTile
          label="Reports this month"
          value={num(data?.reportsThisMonth)}
          hint="Issued to clients"
          icon={<FileText />}
          loading={isLoading}
        />
        <StatTile
          label="Funds in catalogue"
          value={num(data?.fundsInCatalogue)}
          hint="Available to the fund picker"
          icon={<Search />}
          loading={isLoading}
        />
      </div>

      <Card
        className="mt-6"
        title="Recent analyses"
        description="The most recently updated work across the firm."
        flush
        actions={
          <ButtonLink to="/clients" size="sm" variant="outline">
            All clients
          </ButtonLink>
        }
      >
        <DataTable
          columns={columns}
          rows={data?.recentAnalyses}
          getRowId={(row) => row.id}
          isLoading={isLoading}
          caption="Recently updated analyses"
          initialSort={{ columnId: 'updated', direction: 'desc' }}
          emptyTitle="No analyses yet"
          emptyDescription="Start a pension switch, DB transfer or cashflow plan from a client record."
          emptyAction={<ButtonLink to="/clients">Choose a client</ButtonLink>}
        />
      </Card>

      <div className="mt-6 grid gap-4 sm:grid-cols-3">
        <Card title="Pension switch" description="Critical yield, RIY and the COBS 13 charge tables.">
          <ButtonLink to="/pension-switch" variant="outline" icon={<PiggyBank />}>
            Start an analysis
          </ButtonLink>
        </Card>
        <Card title="DB transfer" description="Transfer value comparator in the COBS 19 Annex 5 layout.">
          <ButtonLink to="/db-transfer" variant="outline" icon={<LineChart />}>
            Start an analysis
          </ButtonLink>
        </Card>
        <Card title="Cashflow" description="Deterministic plan and a stochastic fan chart.">
          <ButtonLink to="/cashflow" variant="outline" icon={<BarChart3 />}>
            Build a plan
          </ButtonLink>
        </Card>
      </div>
    </>
  )
}
