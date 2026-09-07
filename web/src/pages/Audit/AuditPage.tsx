import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { ShieldCheck } from 'lucide-react'
import { audit as auditApi } from '@/api/endpoints'
import { useAuditEvents } from '@/api/queries'
import { errorMessage, isApiError } from '@/api/client'
import type { AuditEventDto, AuditVerifyResult } from '@/api/types'
import { dateTime, shortHash } from '@/lib/format'
import { Alert } from '@/components/ui/Alert'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable'
import { PageHeader } from '@/components/ui/PageHeader'
import { Field } from '@/components/form/Field'
import { Input, Select } from '@/components/form/Input'

const PAGE_SIZE = 25

const ENTITY_TYPES = [
  'Client',
  'Scheme',
  'PensionSwitchAnalysis',
  'DbTransferAnalysis',
  'CashflowPlan',
  'Report',
  'Calculation',
  'AssumptionSet',
]

/** Pretty-print the audit payload, which arrives as a JSON *string* rather than an object. */
function formatPayload(payload: string): string {
  try {
    return JSON.stringify(JSON.parse(payload), null, 1)
  } catch {
    return payload
  }
}

/**
 * The hash-chained audit log. "Verify chain" re-walks the chain server-side; it needs the
 * Compliance (or FirmAdmin) role, so a 403 is reported as a permissions message rather than an
 * error the adviser can do anything about.
 */
export default function AuditPage() {
  const [params, setParams] = useSearchParams()
  const [verification, setVerification] = useState<AuditVerifyResult | null>(null)
  const [verifyError, setVerifyError] = useState<string | null>(null)
  const [verifying, setVerifying] = useState(false)
  const [expanded, setExpanded] = useState<string | null>(null)

  const entityId = params.get('entityId') ?? ''
  const entityType = params.get('entityType') ?? ''
  const page = Number(params.get('page') ?? 1)

  const { data, isLoading, error } = useAuditEvents({
    entityId: entityId || undefined,
    entityType: entityType || undefined,
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

  const verify = async () => {
    setVerifying(true)
    setVerifyError(null)
    try {
      setVerification(await auditApi.verify())
    } catch (e) {
      setVerification(null)
      setVerifyError(
        isApiError(e) && e.isForbidden
          ? 'Verifying the chain needs the Compliance or Firm admin role. Ask a compliance user to run it.'
          : errorMessage(e),
      )
    } finally {
      setVerifying(false)
    }
  }

  const columns: Column<AuditEventDto>[] = [
    { id: 'sequence', header: '#', align: 'right', cell: (row) => row.sequence, sortValue: (row) => row.sequence },
    {
      id: 'when',
      header: 'When',
      cell: (row) => dateTime(row.occurredAtUtc),
      sortValue: (row) => row.occurredAtUtc,
    },
    {
      id: 'action',
      header: 'Action',
      cell: (row) => (
        <span className="flex flex-wrap items-center gap-2">
          <Badge tone={row.action === 'Locked' ? 'success' : row.action === 'Preview' ? 'neutral' : 'primary'} size="sm">
            {row.action}
          </Badge>
          <span className="text-fg-muted">{row.entityType}</span>
        </span>
      ),
      sortValue: (row) => row.action,
    },
    {
      id: 'user',
      header: 'User',
      cell: (row) => row.userName ?? (row.userId ? shortHash(row.userId, 8) : 'System'),
      sortValue: (row) => row.userName ?? row.userId,
      hideBelow: 'md',
    },
    {
      id: 'entity',
      header: 'Entity',
      cell: (row) =>
        row.entityId ? (
          <button
            type="button"
            onClick={() => setParam('entityId', row.entityId!)}
            className="font-mono text-xs text-primary-700 hover:underline"
          >
            {shortHash(row.entityId, 8)}
          </button>
        ) : (
          '—'
        ),
      hideBelow: 'lg',
    },
    {
      id: 'hash',
      header: 'Hash',
      cell: (row) => <code className="font-mono text-xs text-fg-muted">{shortHash(row.hash, 10)}</code>,
      hideBelow: 'lg',
    },
    {
      id: 'payload',
      header: <span className="sr-only">Payload</span>,
      align: 'right',
      cell: (row) => (
        <Button
          size="sm"
          variant="ghost"
          aria-expanded={expanded === row.id}
          onClick={() => setExpanded(expanded === row.id ? null : row.id)}
        >
          {expanded === row.id ? 'Hide' : 'Payload'}
        </Button>
      ),
    },
  ]

  const expandedEvent = data?.items.find((e) => e.id === expanded)

  return (
    <>
      <PageHeader
        title="Audit"
        description="Every change is appended to a hash chain, so a tampered record breaks verification."
        actions={
          <Button icon={<ShieldCheck />} onClick={verify} loading={verifying}>
            Verify chain
          </Button>
        }
      />

      {verifyError && (
        <Alert tone="warning" title="Could not verify" className="mb-4">
          {verifyError}
        </Alert>
      )}

      {verification && (
        <Alert
          tone={verification.isValid ? 'success' : 'danger'}
          title={verification.isValid ? 'Chain verified' : 'Chain is broken'}
          className="mb-4"
        >
          {verification.isValid
            ? `All ${verification.eventsChecked} events hash correctly against their predecessor.`
            : `Verification failed at index ${verification.firstBrokenIndex}. ${verification.reason ?? ''}`}
        </Alert>
      )}

      {error && (
        <Alert tone="danger" title="Audit events could not be loaded" className="mb-4">
          {errorMessage(error)}
        </Alert>
      )}

      <Card flush>
        <div className="grid gap-3 border-b border-border p-4 sm:grid-cols-2">
          <Field label="Entity type">
            <Select
              value={entityType}
              placeholder="All types"
              onChange={(e) => setParam('entityType', e.target.value)}
              options={ENTITY_TYPES.map((t) => ({ value: t, label: t }))}
            />
          </Field>
          <Field label="Entity id" hint="Paste a GUID to follow one record through the log.">
            <Input value={entityId} placeholder="GUID" onChange={(e) => setParam('entityId', e.target.value)} />
          </Field>
        </div>

        <DataTable
          columns={columns}
          rows={data?.items}
          getRowId={(row) => row.id}
          isLoading={isLoading}
          caption="Audit events, newest first"
          emptyTitle="No audit events"
          emptyDescription="Events appear as soon as somebody creates, calculates, locks or downloads something."
        />

        {expandedEvent && (
          <div className="border-t border-border bg-surface-muted p-4">
            <p className="text-xs font-medium uppercase tracking-wide text-fg-subtle">
              Payload · event #{expandedEvent.sequence}
            </p>
            <pre className="mt-2 overflow-x-auto rounded-md bg-surface p-3 font-mono text-xs text-fg">
              {formatPayload(expandedEvent.payload)}
            </pre>
            <dl className="mt-3 grid gap-2 text-xs sm:grid-cols-2">
              <div>
                <dt className="text-fg-subtle">Previous hash</dt>
                <dd className="font-mono break-all">{expandedEvent.previousHash}</dd>
              </div>
              <div>
                <dt className="text-fg-subtle">Hash</dt>
                <dd className="font-mono break-all">{expandedEvent.hash}</dd>
              </div>
            </dl>
          </div>
        )}

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
    </>
  )
}
