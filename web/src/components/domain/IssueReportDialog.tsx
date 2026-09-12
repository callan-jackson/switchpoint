import { useState } from 'react'
import { FileText } from 'lucide-react'
import { Dialog } from '@/components/ui/Dialog'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { Field } from '@/components/form/Field'
import { Select } from '@/components/form/Input'
import type { ReportFormat, ReportKind } from '@/api/types'

export interface IssueReportDialogProps {
  open: boolean
  onClose: () => void
  /** Report kinds valid for this analysis, first one selected by default. */
  kinds: { value: ReportKind; label: string; hint: string }[]
  /** True when the analysis is already locked, so issuing another changes nothing. */
  alreadyLocked: boolean
  busy?: boolean
  onIssue: (kind: ReportKind, format: ReportFormat) => void
}

const formats: { value: ReportFormat; label: string; hint: string }[] = [
  { value: 'pdf', label: 'PDF', hint: 'The finished document, ready to send to the client.' },
  { value: 'docx', label: 'Word', hint: 'The same document, editable before it goes out.' },
  { value: 'json', label: 'JSON', hint: 'Every figure behind the report, for checking or archiving.' },
]

/**
 * Issuing a report locks the analysis permanently, and until now the only mention of that was a toast
 * afterwards. An adviser should be told before they click, not after — and should choose a format rather
 * than being handed whatever the page hard-coded.
 */
export function IssueReportDialog({ open, onClose, kinds, alreadyLocked, busy, onIssue }: IssueReportDialogProps) {
  const [kind, setKind] = useState<ReportKind>(kinds[0]?.value ?? 'suitability')
  const [format, setFormat] = useState<ReportFormat>('pdf')

  const selectedKind = kinds.find((k) => k.value === kind)
  const selectedFormat = formats.find((f) => f.value === format)

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Issue a report"
      description="The document is generated from the stored result and stamped with its hash."
      size="md"
      locked={busy}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button icon={<FileText />} loading={busy} onClick={() => onIssue(kind, format)}>
            {alreadyLocked ? 'Issue report' : 'Lock and issue'}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-4">
        {!alreadyLocked && (
          <Alert tone="warning" title="This locks the analysis">
            Once a report is issued the inputs and results are fixed: further edits and deletion are refused.
            To explore a different option afterwards, copy this analysis into a new one. Carry on only if the
            figures are final.
          </Alert>
        )}

        <Field label="Report" hint={selectedKind?.hint}>
          <Select value={kind} onChange={(e) => setKind(e.target.value as ReportKind)}>
            {kinds.map((k) => (
              <option key={k.value} value={k.value}>
                {k.label}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Format" hint={selectedFormat?.hint}>
          <Select value={format} onChange={(e) => setFormat(e.target.value as ReportFormat)}>
            {formats.map((f) => (
              <option key={f.value} value={f.value}>
                {f.label}
              </option>
            ))}
          </Select>
        </Field>
      </div>
    </Dialog>
  )
}
