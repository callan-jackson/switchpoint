import { useQuery } from '@tanstack/react-query'
import { useDocumentTitle } from '@/lib/hooks'
import { Link } from 'react-router'
import { Download } from 'lucide-react'
import { reports as reportsApi } from '@/api/endpoints'
import { queryKeys } from '@/api/queries'
import { errorMessage } from '@/api/client'
import type { ReportDto } from '@/api/types'
import { reportFilename, saveBlob } from '@/lib/files'
import { dateTime, fileSize, shortHash } from '@/lib/format'
import { reportFormatLabels, reportKindLabels } from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Badge } from '@/components/ui/Badge'
import { Button, ButtonLink } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { PageHeader } from '@/components/ui/PageHeader'
import { useToast } from '@/components/ui/Toast'

/**
 * Every report the firm has issued. Downloads go through the API client so the bearer token is
 * attached, then reach the browser as a blob — a plain link would drop the token.
 */
export default function ReportsPage() {
  useDocumentTitle('Reports')
  const toast = useToast()
  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.reports.all,
    queryFn: ({ signal }) => reportsApi.list(signal),
  })

  const download = async (report: ReportDto) => {
    try {
      const blob = await reportsApi.download(report.id)
      saveBlob(blob, reportFilename(report))
      toast.success('Report downloaded', reportFilename(report))
    } catch (e) {
      toast.error('Download failed', errorMessage(e))
    }
  }

  const columns: Column<ReportDto>[] = [
    {
      id: 'kind',
      header: 'Report',
      cell: (row) => (
        <div className="min-w-0">
          <p className="font-medium text-fg">{reportKindLabels[row.kind]}</p>
          <p className="text-xs text-fg-muted">
            <Link to={`/clients/${row.clientId}?tab=reports`} className="text-primary-700 hover:underline">
              Open client
            </Link>
          </p>
        </div>
      ),
      sortValue: (row) => row.kind,
    },
    {
      id: 'format',
      header: 'Format',
      cell: (row) => <Badge tone="neutral">{reportFormatLabels[row.format]}</Badge>,
      sortValue: (row) => row.format,
    },
    {
      id: 'version',
      header: 'Analysis version',
      align: 'right',
      cell: (row) => row.analysisVersion,
      sortValue: (row) => row.analysisVersion,
      hideBelow: 'sm',
    },
    {
      id: 'template',
      header: 'Template',
      cell: (row) => row.templateVersion,
      sortValue: (row) => row.templateVersion,
      hideBelow: 'lg',
    },
    {
      id: 'generated',
      header: 'Generated',
      cell: (row) => dateTime(row.generatedAtUtc),
      sortValue: (row) => row.generatedAtUtc,
    },
    {
      id: 'size',
      header: 'Size',
      align: 'right',
      cell: (row) => fileSize(row.sizeBytes),
      sortValue: (row) => row.sizeBytes,
      hideBelow: 'md',
    },
    {
      id: 'hash',
      header: 'SHA-256',
      cell: (row) => <code className="font-mono text-xs">{shortHash(row.sha256)}</code>,
      hideBelow: 'lg',
    },
    {
      id: 'download',
      header: <span className="sr-only">Download</span>,
      align: 'right',
      cell: (row) => (
        <Button size="sm" variant="outline" icon={<Download />} onClick={() => void download(row)}>
          Download
        </Button>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Reports"
        description="Documents issued to clients. Each one is pinned to the analysis version and result hash it was produced from."
      />

      {error && (
        <Alert tone="danger" title="Reports could not be loaded" className="mb-4">
          {errorMessage(error)}
        </Alert>
      )}

      <Card flush>
        <DataTable
          columns={columns}
          rows={data}
          getRowId={(row) => row.id}
          isLoading={isLoading}
          caption="Issued reports"
          initialSort={{ columnId: 'generated', direction: 'desc' }}
          emptyTitle="No reports yet"
          emptyDescription="Generating a report from an analysis locks that analysis and records its result hash here."
          emptyAction={<ButtonLink to="/clients">Choose a client</ButtonLink>}
        />
      </Card>
    </>
  )
}
