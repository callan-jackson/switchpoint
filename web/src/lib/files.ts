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

/** File extension for a report format. */
export function extensionFor(format: 'Pdf' | 'Docx' | 'Json'): string {
  return format === 'Pdf' ? 'pdf' : format === 'Docx' ? 'docx' : 'json'
}
