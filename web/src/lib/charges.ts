import type {
  ChargeScheduleDto,
  FixedChargeDto,
  Frequency,
  TieredChargeDto,
  AdviserChargesDto,
} from '@/api/types'

/**
 * Client-side charge arithmetic used by the Products & Charges explorer and the fund compare
 * basket. This mirrors the Domain `ChargeSchedule.AnnualChargeFor` rules closely enough for an
 * indicative "what would this cost at £X" figure; the API remains the source of truth for any
 * number that reaches a report.
 */

export function paymentsPerYear(frequency: Frequency): number {
  switch (frequency) {
    case 'Monthly':
      return 12
    case 'Quarterly':
      return 4
    case 'Annually':
      return 1
    case 'Single':
      return 0
  }
}

/** Annual £ charge for a tiered percentage charge at a given fund value. */
export function tieredAnnualCharge(tiered: TieredChargeDto | undefined, value: number): number {
  if (!tiered || tiered.bands.length === 0 || value <= 0) return 0
  const bands = [...tiered.bands]
  if (tiered.mode === 'WholeOfFund') {
    const band = bands.find((b) => b.upTo === undefined || b.upTo === null || value <= b.upTo)
    const rate = (band ?? bands[bands.length - 1]).annualRatePct
    return (value * rate) / 100
  }
  let remaining = value
  let lower = 0
  let total = 0
  for (const band of bands) {
    if (remaining <= 0) break
    const upper = band.upTo === undefined || band.upTo === null ? Infinity : band.upTo
    const slice = Math.max(0, Math.min(remaining, upper - lower))
    total += (slice * band.annualRatePct) / 100
    remaining -= slice
    lower = upper
  }
  return total
}

/** Effective annual rate (percentage number) of a tiered charge at a value. */
export function tieredEffectivePct(tiered: TieredChargeDto | undefined, value: number): number {
  if (value <= 0) return 0
  return (tieredAnnualCharge(tiered, value) / value) * 100
}

export function fixedChargesAnnual(fixed: FixedChargeDto[]): number {
  return fixed.reduce((sum, f) => sum + f.amount * paymentsPerYear(f.frequency), 0)
}

export function adviserOngoingAnnual(adviser: AdviserChargesDto, value: number): number {
  return (value * adviser.ongoingPct) / 100 + adviser.ongoingAmount
}

export interface ChargeBreakdown {
  value: number
  platform: number
  product: number
  fixed: number
  fund: number
  transaction: number
  adviserOngoing: number
  dealing: number
  switch: number
  discounts: number
  total: number
  /** Total as a percentage number of the value. */
  totalPct: number
  /** Product + platform + fixed only (excludes fund, adviser, activity). */
  productOnlyPct: number
}

/**
 * Indicative first-year charges at a fund value. `weightedOcfPct` overrides the schedule's
 * explicit OCF when the fund charge basis is FromHoldings.
 */
export function annualChargeBreakdown(
  schedule: ChargeScheduleDto,
  value: number,
  weightedOcfPct?: number,
): ChargeBreakdown {
  const platform = tieredAnnualCharge(schedule.platformCharge, value)
  const product = tieredAnnualCharge(schedule.productCharge, value)
  const fixed = fixedChargesAnnual(schedule.fixedCharges)
  const ocfPct =
    schedule.fundCharge.kind === 'Explicit'
      ? (schedule.fundCharge.ocfPct ?? 0)
      : schedule.fundCharge.kind === 'FromHoldings'
        ? (weightedOcfPct ?? 0)
        : 0
  const fund = (value * ocfPct) / 100
  const transaction = (value * (schedule.transactionCostsPct ?? 0)) / 100
  const adviserOngoing = adviserOngoingAnnual(schedule.adviserCharges, value)
  const dealing =
    schedule.dealingCharges.fundDealAmount * schedule.dealingCharges.expectedFundDealsPerYear +
    schedule.dealingCharges.etfDealAmount * schedule.dealingCharges.expectedEtfDealsPerYear
  const switchCost = schedule.switchCharge.amountPerSwitch * schedule.switchCharge.expectedSwitchesPerYear
  const discounts = schedule.largeFundDiscounts
    .filter((d) => value >= d.threshold)
    .reduce((best, d) => Math.max(best, d.rebateRatePct), 0)
  const discountAmount = (value * discounts) / 100
  const total =
    platform + product + fixed + fund + transaction + adviserOngoing + dealing + switchCost - discountAmount
  return {
    value,
    platform,
    product,
    fixed,
    fund,
    transaction,
    adviserOngoing,
    dealing,
    switch: switchCost,
    discounts: -discountAmount,
    total,
    totalPct: value > 0 ? (total / value) * 100 : 0,
    productOnlyPct: value > 0 ? ((platform + product + fixed - discountAmount) / value) * 100 : 0,
  }
}

/** Weighted OCF (percentage number) of a set of holdings; weights are percentage numbers. */
export function weightedOcfPct(holdings: { weightPct: number; ocfPct?: number }[]): number {
  const totalWeight = holdings.reduce((s, h) => s + (h.weightPct || 0), 0)
  if (totalWeight <= 0) return 0
  return holdings.reduce((s, h) => s + (h.weightPct || 0) * (h.ocfPct ?? 0), 0) / totalWeight
}

/** Sum of holding weights, rounded to 2 dp to absorb floating point noise. */
export function totalWeightPct(holdings: { weightPct: number | null | undefined }[]): number {
  return Math.round(holdings.reduce((s, h) => s + (h.weightPct ?? 0), 0) * 100) / 100
}

/** A blank schedule with every collection present, ready for the editor or an API call. */
export function emptyChargeSchedule(): ChargeScheduleDto {
  return {
    platformCharge: undefined,
    productCharge: undefined,
    fixedCharges: [],
    fundCharge: { kind: 'FromHoldings' },
    transactionCostsPct: 0,
    adviserCharges: { initialPct: 0, initialAmount: 0, ongoingPct: 0, ongoingAmount: 0 },
    dealingCharges: {
      fundDealAmount: 0,
      etfDealAmount: 0,
      expectedFundDealsPerYear: 0,
      expectedEtfDealsPerYear: 0,
    },
    switchCharge: { amountPerSwitch: 0, expectedSwitchesPerYear: 0 },
    exitPenalty: { bands: [] },
    bidOfferSpreadPct: 0,
    allocationRatePct: 100,
    largeFundDiscounts: [],
  }
}

/** A schedule with a single flat platform charge and explicit OCF (cashflow asset shortcut). */
export function flatChargeSchedule(platformPct: number, ocfPct: number): ChargeScheduleDto {
  const s = emptyChargeSchedule()
  s.platformCharge = { mode: 'Marginal', bands: [{ annualRatePct: platformPct }] }
  s.fundCharge = { kind: 'Explicit', ocfPct }
  return s
}
