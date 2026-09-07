import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { Download, Pencil, Plus, Trash2 } from 'lucide-react'
import { errorMessage } from '@/api/client'
import { reports as reportsApi } from '@/api/endpoints'
import {
  useClient,
  useClientAnalyses,
  useClientReports,
  useCreateScheme,
  useDeleteScheme,
  useUpdateScheme,
} from '@/api/queries'
import type { AnalysisSummary, ReportDto, SchemeDto, SchemeWrite } from '@/api/types'
import { saveBlob, reportFilename } from '@/lib/files'
import { date, dateTime, fileSize, fmtPct, gbp, shortHash } from '@/lib/format'
import {
  analysisKindLabels,
  analysisPath,
  employmentStatusLabels,
  maritalStatusLabels,
  reportFormatLabels,
  reportKindLabels,
  schemeTypeLabels,
  taxRegimeLabels,
} from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Badge, StatusBadge } from '@/components/ui/Badge'
import { Button, ButtonLink, IconButton } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { DefinitionList } from '@/components/ui/DefinitionList'
import { Dialog } from '@/components/ui/Dialog'
import { PageHeader } from '@/components/ui/PageHeader'
import { PageSkeleton } from '@/components/ui/Skeleton'
import { Tabs } from '@/components/ui/Tabs'
import { useToast } from '@/components/ui/Toast'
import { SchemeDialog } from '@/components/domain/SchemeDialog'
import { ClientFormDialog } from './ClientFormDialog'

type TabId = 'details' | 'schemes' | 'analyses' | 'reports'

export default function ClientDetailPage() {
  const { clientId = '' } = useParams()
  const [params, setParams] = useSearchParams()
  const toast = useToast()

  const tab = (params.get('tab') as TabId | null) ?? 'details'
  const setTab = (next: TabId) => setParams({ tab: next }, { replace: true })

  const { data: client, isLoading, error } = useClient(clientId)
  const { data: analyses } = useClientAnalyses(clientId)
  const { data: reports } = useClientReports(clientId)

  const createScheme = useCreateScheme(clientId)
  const updateScheme = useUpdateScheme(clientId)
  const deleteScheme = useDeleteScheme(clientId)

  const [editingClient, setEditingClient] = useState(false)
  const [schemeDialog, setSchemeDialog] = useState<{ open: boolean; scheme?: SchemeDto }>({ open: false })
  const [confirmDelete, setConfirmDelete] = useState<SchemeDto | null>(null)

  if (isLoading) return <PageSkeleton />
  if (error || !client) {
    return (
      <Alert tone="danger" title="Client not found">
        {errorMessage(error, 'That client does not exist, or belongs to another firm.')}
      </Alert>
    )
  }

  const saveScheme = async (body: SchemeWrite) => {
    if (schemeDialog.scheme) {
      await updateScheme.mutateAsync({ schemeId: schemeDialog.scheme.id, body })
      toast.success('Arrangement updated', body.productName)
    } else {
      await createScheme.mutateAsync(body)
      toast.success('Arrangement added', body.productName)
    }
    setSchemeDialog({ open: false })
  }

  const download = async (report: ReportDto) => {
    try {
      const blob = await reportsApi.download(report.id)
      saveBlob(blob, reportFilename(report))
      toast.success('Report downloaded')
    } catch (e) {
      toast.error('Download failed', errorMessage(e))
    }
  }

  const schemeColumns: Column<SchemeDto>[] = [
    {
      id: 'product',
      header: 'Arrangement',
      cell: (row) => (
        <div className="min-w-0">
          <p className="truncate font-medium text-fg">{row.productName}</p>
          <p className="truncate text-xs text-fg-muted">
            {row.providerName ?? 'Provider not on the catalogue'}
            {row.policyNumber ? ` · ${row.policyNumber}` : ''}
          </p>
        </div>
      ),
      sortValue: (row) => row.productName,
    },
    { id: 'type', header: 'Type', cell: (row) => schemeTypeLabels[row.type], sortValue: (row) => row.type, hideBelow: 'sm' },
    {
      id: 'value',
      header: 'Current value',
      align: 'right',
      cell: (row) => gbp(row.currentValue),
      sortValue: (row) => row.currentValue,
    },
    {
      id: 'transfer',
      header: 'Transfer value',
      align: 'right',
      cell: (row) => gbp(row.transferValue),
      sortValue: (row) => row.transferValue,
      hideBelow: 'md',
    },
    {
      id: 'ocf',
      header: 'Weighted OCF',
      align: 'right',
      cell: (row) => fmtPct(row.weightedOcfPct, 2),
      sortValue: (row) => row.weightedOcfPct,
      hideBelow: 'lg',
    },
    {
      id: 'flags',
      header: 'Flags',
      cell: (row) => (
        <div className="flex flex-wrap gap-1">
          {row.guarantees.guaranteedAnnuityRatePct != null && (
            <Badge tone="warning" size="sm">
              GAR {fmtPct(row.guarantees.guaranteedAnnuityRatePct, 2)}
            </Badge>
          )}
          {row.guarantees.withProfits && (
            <Badge tone="warning" size="sm">
              With-profits
            </Badge>
          )}
          {row.definedBenefit && (
            <Badge tone="primary" size="sm">
              DB
            </Badge>
          )}
          {row.inDrawdown && (
            <Badge tone="info" size="sm">
              Drawdown
            </Badge>
          )}
        </div>
      ),
    },
    {
      id: 'actions',
      header: <span className="sr-only">Actions</span>,
      align: 'right',
      cell: (row) => (
        <div className="flex justify-end gap-1">
          <IconButton size="sm" label={`Edit ${row.productName}`} onClick={() => setSchemeDialog({ open: true, scheme: row })}>
            <Pencil />
          </IconButton>
          <IconButton size="sm" label={`Delete ${row.productName}`} onClick={() => setConfirmDelete(row)}>
            <Trash2 />
          </IconButton>
        </div>
      ),
    },
  ]

  const analysisColumns: Column<AnalysisSummary>[] = [
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
    { id: 'kind', header: 'Type', cell: (row) => analysisKindLabels[row.kind], sortValue: (row) => row.kind },
    { id: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} />, sortValue: (row) => row.status },
    { id: 'version', header: 'Version', align: 'right', cell: (row) => row.version, sortValue: (row) => row.version },
    {
      id: 'calculated',
      header: 'Calculated',
      cell: (row) => (row.calculatedAtUtc ? dateTime(row.calculatedAtUtc) : '—'),
      sortValue: (row) => row.calculatedAtUtc,
      hideBelow: 'md',
    },
  ]

  const reportColumns: Column<ReportDto>[] = [
    { id: 'kind', header: 'Report', cell: (row) => reportKindLabels[row.kind], sortValue: (row) => row.kind },
    { id: 'format', header: 'Format', cell: (row) => reportFormatLabels[row.format], sortValue: (row) => row.format },
    {
      id: 'version',
      header: 'Analysis version',
      align: 'right',
      cell: (row) => row.analysisVersion,
      sortValue: (row) => row.analysisVersion,
      hideBelow: 'sm',
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
        eyebrow="Client"
        title={client.fullName}
        description={`Age ${client.age} · target retirement ${client.targetRetirementAge} · risk ${client.riskProfile}/7`}
        meta={
          <>
            <Badge tone="primary">{taxRegimeLabels[client.taxRegime]}</Badge>
            <Badge>{employmentStatusLabels[client.employmentStatus]}</Badge>
            {client.externalReference.source !== 'manual' && (
              <Badge tone="info">Imported from {client.externalReference.source}</Badge>
            )}
          </>
        }
        actions={
          <>
            <Button variant="outline" icon={<Pencil />} onClick={() => setEditingClient(true)}>
              Edit client
            </Button>
            <ButtonLink to={`/pension-switch?clientId=${client.id}`}>New switch analysis</ButtonLink>
          </>
        }
      />

      <Tabs
        aria-label="Client sections"
        value={tab}
        onChange={setTab}
        items={[
          { id: 'details', label: 'Details' },
          { id: 'schemes', label: 'Schemes', badge: client.schemes.length },
          { id: 'analyses', label: 'Analyses', badge: analyses?.length },
          { id: 'reports', label: 'Reports', badge: reports?.length },
        ]}
      >
        {tab === 'details' && (
          <div className="grid gap-4 lg:grid-cols-2">
            <Card title="Personal">
              <DefinitionList
                items={[
                  { label: 'Full name', value: client.fullName },
                  { label: 'Date of birth', value: `${date(client.dateOfBirth)} (age ${client.age})` },
                  { label: 'Sex', value: client.sex === 'female' ? 'Female' : 'Male' },
                  { label: 'Marital status', value: maritalStatusLabels[client.maritalStatus] },
                  { label: 'Email', value: client.email ?? '—' },
                  { label: 'Phone', value: client.phone ?? '—' },
                  { label: 'NI number', value: client.nationalInsuranceNumberMasked ?? '—' },
                  { label: 'Smoker', value: client.isSmoker ? 'Yes' : 'No' },
                  {
                    label: 'Address',
                    wide: true,
                    value:
                      [client.address?.line1, client.address?.line2, client.address?.town, client.address?.county, client.address?.postcode]
                        .filter(Boolean)
                        .join(', ') || '—',
                  },
                ]}
              />
            </Card>
            <Card title="Financial">
              <DefinitionList
                items={[
                  { label: 'Employment', value: employmentStatusLabels[client.employmentStatus] },
                  { label: 'Annual salary', value: gbp(client.annualSalary) },
                  { label: 'Tax regime', value: taxRegimeLabels[client.taxRegime] },
                  { label: 'Target retirement age', value: client.targetRetirementAge },
                  { label: 'Risk profile', value: `${client.riskProfile} of 7` },
                  { label: 'Health', value: client.health === 'enhanced' ? 'Enhanced' : 'Standard' },
                  {
                    label: 'State pension forecast',
                    value: client.statePension.forecastWeeklyAmount
                      ? `${gbp(client.statePension.forecastWeeklyAmount, { dp: 2 })} per week`
                      : '—',
                  },
                  { label: 'Qualifying years', value: client.statePension.qualifyingYears ?? '—' },
                  { label: 'Record updated', value: dateTime(client.updatedAtUtc), wide: true },
                ]}
              />
            </Card>
          </div>
        )}

        {tab === 'schemes' && (
          <Card
            flush
            title="Arrangements"
            description="Everything the engines read: values, charges, holdings, guarantees and DB tranches."
            actions={
              <Button size="sm" icon={<Plus />} onClick={() => setSchemeDialog({ open: true })}>
                Add arrangement
              </Button>
            }
          >
            <DataTable
              columns={schemeColumns}
              rows={client.schemes}
              getRowId={(row) => row.id}
              caption="Arrangements held by this client"
              initialSort={{ columnId: 'value', direction: 'desc' }}
              emptyTitle="No arrangements recorded"
              emptyDescription="Add the client's pensions and investments so an analysis can be run."
              emptyAction={
                <Button icon={<Plus />} onClick={() => setSchemeDialog({ open: true })}>
                  Add arrangement
                </Button>
              }
            />
          </Card>
        )}

        {tab === 'analyses' && (
          <Card flush title="Analyses" description="Every persisted analysis for this client.">
            <DataTable
              columns={analysisColumns}
              rows={analyses}
              getRowId={(row) => row.id}
              caption="Analyses"
              initialSort={{ columnId: 'calculated', direction: 'desc' }}
              emptyTitle="No analyses yet"
              emptyDescription="Start a pension switch, DB transfer or cashflow plan for this client."
              emptyAction={<ButtonLink to={`/pension-switch?clientId=${client.id}`}>Start a switch analysis</ButtonLink>}
            />
          </Card>
        )}

        {tab === 'reports' && (
          <Card flush title="Reports" description="Issued documents, each pinned to a locked analysis version.">
            <DataTable
              columns={reportColumns}
              rows={reports}
              getRowId={(row) => row.id}
              caption="Reports"
              initialSort={{ columnId: 'generated', direction: 'desc' }}
              emptyTitle="No reports issued"
              emptyDescription="Generating a report locks the analysis it was produced from."
            />
          </Card>
        )}
      </Tabs>

      <ClientFormDialog open={editingClient} client={client} onClose={() => setEditingClient(false)} />

      <SchemeDialog
        open={schemeDialog.open}
        scheme={schemeDialog.scheme}
        onClose={() => setSchemeDialog({ open: false })}
        onSave={saveScheme}
        saving={createScheme.isPending || updateScheme.isPending}
        error={
          createScheme.error || updateScheme.error
            ? errorMessage(createScheme.error ?? updateScheme.error)
            : null
        }
      />

      <Dialog
        open={confirmDelete !== null}
        onClose={() => setConfirmDelete(null)}
        title="Delete this arrangement?"
        description={confirmDelete?.productName}
        footer={
          <>
            <Button variant="ghost" onClick={() => setConfirmDelete(null)}>
              Cancel
            </Button>
            <Button
              variant="danger"
              loading={deleteScheme.isPending}
              onClick={async () => {
                if (!confirmDelete) return
                await deleteScheme.mutateAsync(confirmDelete.id)
                toast.success('Arrangement deleted', confirmDelete.productName)
                setConfirmDelete(null)
              }}
            >
              Delete
            </Button>
          </>
        }
      >
        <p className="text-sm text-fg-muted">
          Analyses that already reference this arrangement keep the figures they were calculated
          with, but it will no longer be available to new analyses.
        </p>
      </Dialog>
    </>
  )
}
