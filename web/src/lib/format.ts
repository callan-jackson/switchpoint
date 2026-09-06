import { differenceInYears, format as formatDate, isValid, parseISO } from 'date-fns'

const EM_DASH = '—'

export interface MoneyOptions {
  /** Decimal places; default 0 for headline figures, use 2 for statements. */
  dp?: number
  /** Show an explicit + for positive values (deltas). */
  signed?: boolean
  /** Abbreviate to k / m / bn for tiles (e.g. £1.2m). */
  compact?: boolean
}

const gbpCache = new Map<string, Intl.NumberFormat>()

function gbpFormatter(dp: number, signed: boolean, compact: boolean): Intl.NumberFormat {
  const key = `${dp}|${signed}|${compact}`
  let f = gbpCache.get(key)
  if (!f) {
    f = new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: 'GBP',
      minimumFractionDigits: compact ? 0 : dp,
      maximumFractionDigits: compact ? 1 : dp,
      signDisplay: signed ? 'exceptZero' : 'auto',
      ...(compact ? { notation: 'compact', compactDisplay: 'short' } : {}),
    })
    gbpCache.set(key, f)
  }
  return f
}

/** Format a GBP amount: gbp(1234.5) -> '£1,235'; gbp(1234.5, { dp: 2 }) -> '£1,234.50'. */
export function gbp(value: number | null | undefined, options: MoneyOptions = {}): string {
  if (value === null || value === undefined || Number.isNaN(value)) return EM_DASH
  const { dp = 0, signed = false, compact = false } = options
  const text = gbpFormatter(dp, signed, compact).format(value)
  // Intl uses 'B' for billions in en-GB compact notation; UK finance says 'bn'.
  return compact ? text.replace(/B$/, 'bn').replace(/M$/, 'm').replace(/K$/, 'k') : text
}

/** Format a FRACTION as a percentage: pct(0.0525) -> '5.25%'; pct(0.0525, 1) -> '5.3%'. */
export function pct(fraction: number | null | undefined, dp = 2, signed = false): string {
  if (fraction === null || fraction === undefined || Number.isNaN(fraction)) return EM_DASH
  return new Intl.NumberFormat('en-GB', {
    style: 'percent',
    minimumFractionDigits: dp,
    maximumFractionDigits: dp,
    signDisplay: signed ? 'exceptZero' : 'auto',
  }).format(fraction)
}

/** Plain number with thousands separators: num(1234567.891, 1) -> '1,234,567.9'. */
export function num(value: number | null | undefined, dp = 0): string {
  if (value === null || value === undefined || Number.isNaN(value)) return EM_DASH
  return new Intl.NumberFormat('en-GB', {
    minimumFractionDigits: dp,
    maximumFractionDigits: dp,
  }).format(value)
}

function toDate(value: string | Date | null | undefined): Date | null {
  if (!value) return null
  const d = value instanceof Date ? value : parseISO(value)
  return isValid(d) ? d : null
}

/** ISO date or timestamp -> 'dd MMM yyyy' (e.g. '05 Apr 2026'). Invalid input -> em dash. */
export function date(value: string | Date | null | undefined): string {
  const d = toDate(value)
  return d ? formatDate(d, 'dd MMM yyyy') : EM_DASH
}

/** ISO timestamp -> 'dd MMM yyyy, HH:mm' in the viewer's local time. */
export function dateTime(value: string | Date | null | undefined): string {
  const d = toDate(value)
  return d ? formatDate(d, 'dd MMM yyyy, HH:mm') : EM_DASH
}

/** Completed years between a date of birth and `asOf` (default today). Invalid -> null. */
export function ageFromDob(dob: string | Date | null | undefined, asOf: Date = new Date()): number | null {
  const d = toDate(dob)
  if (!d || d > asOf) return null
  return differenceInYears(asOf, d)
}

/** Years as '12 yrs' / '1 yr'. */
export function years(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return EM_DASH
  return `${num(value)} ${value === 1 ? 'yr' : 'yrs'}`
}

/** 'Jane Ann Smith' -> 'JS'. */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return ''
  const first = parts[0][0] ?? ''
  const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : ''
  return (first + last).toUpperCase()
}

/** Parse user-typed money text ('£12,500.50', ' 12 500 ', '(250)') into a number; null if empty/invalid. */
export function parseMoney(text: string): number | null {
  const trimmed = text.trim()
  if (trimmed === '') return null
  const negative = /^\(.*\)$/.test(trimmed) || trimmed.startsWith('-')
  const cleaned = trimmed.replace(/[£,\s()]/g, '').replace(/^-/, '')
  if (cleaned === '' || !/^\d*\.?\d*$/.test(cleaned) || cleaned === '.') return null
  const n = Number(cleaned)
  if (!Number.isFinite(n)) return null
  return negative ? -n : n
}

/** Parse user-typed percentage text ('5.25', '5.25 %') into a FRACTION (0.0525); null if empty/invalid. */
export function parsePercent(text: string, dp = 2): number | null {
  const trimmed = text.trim().replace(/%$/, '').trim()
  if (trimmed === '') return null
  const negative = trimmed.startsWith('-')
  const cleaned = trimmed.replace(/^-/, '').replace(/[,\s]/g, '')
  if (cleaned === '' || !/^\d*\.?\d*$/.test(cleaned) || cleaned === '.') return null
  const n = Number(cleaned)
  if (!Number.isFinite(n)) return null
  const fraction = roundTo(n / 100, dp + 2)
  return negative ? -fraction : fraction
}

export function roundTo(value: number, dp: number): number {
  const factor = 10 ** dp
  return Math.round((value + Number.EPSILON) * factor) / factor
}
