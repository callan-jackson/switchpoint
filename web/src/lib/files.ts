import type { ReportFormat } from '@/api/types'

/** Hand a Blob to the browser as a download. No-op outside a DOM (tests). */
export function saveBlob(blob: Blob, filename: string): void {
  if (typeof document === 'undefined' || typeof URL.createObjectURL !== 'function') return
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.rel = 'noopener'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** File extension for a report format (wire values are camelCase). */
export function extensionFor(format: ReportFormat): string {
  return format
}

/** Filename the API would use for a report download. */
export function reportFilename(report: {
  kind: string
  analysisId: string
  analysisVersion: number
  format: ReportFormat
}): string {
  const kind = report.kind.charAt(0).toUpperCase() + report.kind.slice(1)
  return `${kind}-${report.analysisId.replace(/-/g, '')}-v${report.analysisVersion}.${extensionFor(report.format)}`
}
