import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import type { AnalysisStatus, DataQuality, SwitchVerdict } from '@/api/types'
import { analysisStatusLabels, dataQualityLabels, verdictLabels, verdictReasons } from '@/lib/labels'

export type BadgeTone = 'neutral' | 'primary' | 'accent' | 'success' | 'warning' | 'danger' | 'info'

const toneClasses: Record<BadgeTone, string> = {
  neutral: 'bg-slate-100 text-slate-700 ring-slate-200',
  primary: 'bg-primary-50 text-primary-700 ring-primary-100',
  accent: 'bg-accent-50 text-accent-700 ring-accent-100',
  success: 'bg-success-50 text-success-700 ring-success-100',
  warning: 'bg-warning-50 text-warning-700 ring-warning-100',
  danger: 'bg-danger-50 text-danger-700 ring-danger-100',
  info: 'bg-sky-50 text-sky-700 ring-sky-100',
}

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone
  size?: 'sm' | 'md'
  dot?: boolean
}

export function Badge({ tone = 'neutral', size = 'md', dot, className, children, ...rest }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full font-medium whitespace-nowrap ring-1 ring-inset',
        size === 'sm' ? 'px-1.5 py-0 text-[11px]' : 'px-2 py-0.5 text-xs',
        toneClasses[tone],
        className,
      )}
      {...rest}
    >
      {dot && <span className="size-1.5 rounded-full bg-current" aria-hidden="true" />}
      {children}
    </span>
  )
}

const statusTone: Record<AnalysisStatus, BadgeTone> = {
  draft: 'neutral',
  calculated: 'info',
  locked: 'success',
}

export function StatusBadge({ status }: { status: AnalysisStatus }) {
  return (
    <Badge tone={statusTone[status]} dot>
      {analysisStatusLabels[status]}
    </Badge>
  )
}

// These are outcomes, not marks out of ten. Mapping `retain` to danger and `switchCandidate` to success
// read as "leaving the client's money alone is a failure and moving it is a win", which is the opposite of
// the posture a switching tool should take and the opposite of what the FSA's 2009 review was about. So:
// refer is the one that needs a human (a guarantee is at stake) and is the most prominent; switchCandidate
// is merely actionable; retain is the quiet no-op; consider sits between.
const verdictTone: Record<SwitchVerdict, BadgeTone> = {
  switchCandidate: 'info',
  consider: 'warning',
  retain: 'neutral',
  refer: 'accent',
}

export function VerdictBadge({ verdict, withReason }: { verdict: SwitchVerdict; withReason?: boolean }) {
  const badge = (
    <Badge tone={verdictTone[verdict]} title={withReason ? undefined : verdictReasons[verdict]}>
      {verdictLabels[verdict]}
    </Badge>
  )
  if (!withReason) {
    return badge
  }

  return (
    <div className="flex flex-col items-start gap-1">
      {badge}
      <span className="text-muted max-w-[42ch] text-xs leading-snug">{verdictReasons[verdict]}</span>
    </div>
  )
}

const qualityTone: Record<DataQuality, BadgeTone> = {
  verified: 'success',
  indicative: 'warning',
  placeholder: 'danger',
}

export function DataQualityBadge({ quality }: { quality: DataQuality }) {
  return (
    <Badge tone={qualityTone[quality]} size="sm">
      {dataQualityLabels[quality]}
    </Badge>
  )
}
