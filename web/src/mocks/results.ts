import type {
  CashflowCalcRequest,
  CashflowResultDto,
  CashflowRowDto,
  CriticalYieldAtRateDto,
  DbTransferCalcRequest,
  DbTransferResultDto,
  PensionSwitchCalcRequest,
  PensionSwitchResultDto,
  PercentileRowDto,
  RiyDto,
  StochasticResultDto,
  SwitchVerdict,
} from '@/api/types'
import { assumptionSets, clients, products } from './data'

/**
 * Deterministic stand-ins for the calculation engines. The arithmetic is a plausible sketch, not
 * the engine: it exists so the preview panels, charts and tables have realistic, self-consistent
 * numbers to render when the app runs on mocks.
 */

const ENGINE = '1.0.0-mock'

function assumptionRef(id: string | undefined) {
  const set = assumptionSets.find((s) => s.id === id) ?? assumptionSets[0]
  return { id: set.id, name: set.name, version: set.version }
}

function riy(growthPct: number, start: number, years: number, totalChargePct: number, productChargePct: number): RiyDto {
  const before = start * (1 + growthPct / 100) ** years
  const afterProduct = start * (1 + (growthPct - productChargePct) / 100) ** years
  const afterAll = start * (1 + (growthPct - totalChargePct) / 100) ** years
  const rows = Array.from({ length: Math.min(years, 10) }, (_, i) => {
    const y = i + 1
    return {
      year: y,
      paymentsToDate: 0,
      beforeCharges: start * (1 + growthPct / 100) ** y,
      planAndInvestmentChargesOnly: start * (1 + (growthPct - productChargePct) / 100) ** y,
      afterAllCharges: start * (1 + (growthPct - totalChargePct) / 100) ** y,
      effectOfDeductionsToDate: start * (1 + growthPct / 100) ** y - start * (1 + (growthPct - totalChargePct) / 100) ** y,
    }
  })
  const round1 = (n: number) => Math.round(n * 10) / 10
  return {
    growthPct,
    productRiyPct: productChargePct,
    totalRiyPct: totalChargePct,
    rateAfterProductChargesPct: growthPct - productChargePct,
    rateAfterAllChargesPct: growthPct - totalChargePct,
    valueBeforeCharges: before,
    valueAfterProductCharges: afterProduct,
    valueAfterAllCharges: afterAll,
    productSentence: `Product charges reduce investment growth after price inflation from ${round1(growthPct)}% to ${round1(growthPct - productChargePct)}%.`,
    totalSentence: `All charges reduce investment growth after price inflation from ${round1(growthPct)}% to ${round1(growthPct - totalChargePct)}%.`,
    effectOfCharges: rows,
    totalCharges: {
      platform: before * 0.03,
      product: 0,
      fund: before * 0.06,
      transaction: 0,
      adviserInitial: start * 0.02,
      adviserOngoing: before * 0.05,
      fixed: 0,
      dealing: 0,
      switch: 0,
      discounts: 0,
      allocationAndSpread: 0,
      exitPenalty: 0,
      total: before - afterAll,
    },
  }
}

function yieldAt(growthPct: number, existing: number, receiving: number, inflationPct: number): CriticalYieldAtRateDto {
  const criticalYieldPct = growthPct + (existing - receiving) / Math.max(existing, 1) * 8
  return {
    growthPct,
    existingValueAtRetirement: existing,
    receivingValueAtRetirement: receiving,
    criticalYieldPct,
    criticalYieldRealPct: criticalYieldPct - inflationPct,
    headroomPct: growthPct - criticalYieldPct,
    projectedGain: receiving - existing,
    breakEvenYear: receiving > existing ? 4 : undefined,
    converged: true,
  }
}

export function pensionSwitchResult(request: PensionSwitchCalcRequest): PensionSwitchResultDto {
  const client = clients.find((c) => c.id === request.clientId)
  const schemes = (request.cedingSchemes ?? [])
    .map((ref) => ('schemeId' in ref ? client?.schemes.find((s) => s.id === ref.schemeId) : undefined))
    .filter((s): s is NonNullable<typeof s> => !!s)

  const set = assumptionSets.find((s) => s.id === request.assumptionSetId) ?? assumptionSets[0]
  const growth = request.overrides?.growthIntermediatePct ?? set.growthIntermediatePct
  const inflation = request.overrides?.inflationPct ?? set.inflationPct
  const years = Math.max(1, request.retirementAge - (client?.age ?? request.clientAge ?? 55))

  const totalTransfer = schemes.reduce((s, x) => s + x.transferValue, 0)
  const product = products.find((p) => p.id === request.proposedProductId)
  const proposedOcf =
    request.proposedHoldings.reduce((s, h) => s + h.weightPct * (h.ocfPct ?? 0), 0) /
    Math.max(1, request.proposedHoldings.reduce((s, h) => s + h.weightPct, 0))
  const platformPct = product?.effectiveChargePctAt100k ?? 0.3
  const receivingTotalPct = platformPct + proposedOcf + request.proposedAdviserCharges.ongoingPct
  const initialAdviserCharge =
    (totalTransfer * request.proposedAdviserCharges.initialPct) / 100 + request.proposedAdviserCharges.initialAmount
  const net = totalTransfer - initialAdviserCharge

  const receivingRiy = riy(growth, net, years, receivingTotalPct, platformPct + proposedOcf)
  const existingAt = (g: number) =>
    schemes.reduce((sum, s) => {
      const cost = (s.weightedOcfPct ?? 0.4) + 0.45
      return sum + s.transferValue * (1 + (g - cost) / 100) ** years
    }, 0)
  const receivingAt = (g: number) => net * (1 + (g - receivingTotalPct) / 100) ** years

  const anyGuarantees = schemes.some(
    (s) => s.guarantees.guaranteedAnnuityRatePct != null || s.guarantees.withProfits,
  )

  const warnings: string[] = []
  for (const s of schemes) {
    if (s.guarantees.guaranteedAnnuityRatePct != null || s.guarantees.withProfits) {
      warnings.push(
        `'${s.productName}' has guarantees or protected features (FSA switching outcome 2); it is referred for adviser judgement.`,
      )
    }
  }
  if (receivingAt(growth) < existingAt(growth)) {
    warnings.push(
      'At the intermediate rate the receiving product must outperform the existing arrangements to match them (negative headroom); a switch on cost grounds needs a documented non-cost reason (FSA switching outcome 1).',
    )
  }

  const chart = Array.from({ length: years + 1 }, (_, y) => ({
    year: y,
    existingValue: schemes.reduce((sum, s) => sum + s.transferValue * (1 + (growth - ((s.weightedOcfPct ?? 0.4) + 0.45) - inflation) / 100) ** y, 0),
    receivingValue: net * (1 + (growth - receivingTotalPct - inflation) / 100) ** y,
  }))

  return {
    totalNetTransferValue: totalTransfer,
    initialAdviserCharge,
    anyGuaranteesFlagged: anyGuarantees,
    warnings,
    lower: yieldAt(set.growthLowerPct, existingAt(set.growthLowerPct), receivingAt(set.growthLowerPct), inflation),
    intermediate: yieldAt(growth, existingAt(growth), receivingAt(growth), inflation),
    higher: yieldAt(set.growthHigherPct, existingAt(set.growthHigherPct), receivingAt(set.growthHigherPct), inflation),
    receivingRiy,
    schemes: schemes.map((s) => {
      const cedingCost = (s.weightedOcfPct ?? 0.4) + 0.45
      const retained = s.transferValue * (1 + (growth - cedingCost) / 100) ** years
      const switched = (s.transferValue * (1 - request.proposedAdviserCharges.initialPct / 100)) * (1 + (growth - receivingTotalPct) / 100) ** years
      const flagged = s.guarantees.guaranteedAnnuityRatePct != null || s.guarantees.withProfits
      const verdict: SwitchVerdict = flagged
        ? 'refer'
        : switched > retained * 1.03
          ? 'switchCandidate'
          : switched > retained
            ? 'consider'
            : 'retain'
      return {
        name: s.productName,
        currentValue: s.currentValue,
        netTransferValue: s.transferValue,
        projectedValueIfRetained: retained,
        riyIfRetained: riy(growth, s.transferValue, years, cedingCost, cedingCost),
        riyIfSwitched: riy(growth, s.transferValue, years, receivingTotalPct, platformPct + proposedOcf),
        criticalYieldAlonePct: growth + (retained - switched) / Math.max(retained, 1) * 8,
        guaranteesFlagged: flagged,
        verdict,
      }
    }),
    chart,
    engineVersion: ENGINE,
    calculatedAtUtc: new Date().toISOString(),
    assumptionSet: assumptionRef(request.assumptionSetId),
  }
}

export function dbTransferResult(request: DbTransferCalcRequest): DbTransferResultDto {
  const client = clients.find((c) => c.id === request.clientId)
  const dbScheme = client?.schemes.find((s) => s.id === request.dbSchemeId) ?? clients[1].schemes[1]
  const db = dbScheme.definedBenefit!
  const cetv = dbScheme.transferValue
  const nra = db.normalRetirementAge
  const years = Math.max(1, nra - (client?.age ?? 57))
  const set = assumptionSets.find((s) => s.id === request.assumptionSetId) ?? assumptionSets[0]

  const tranches = db.tranches.map((t) => {
    const rate = t.revaluation.ratePct
    const pensionAtRetirement = t.accruedAnnualPension * (1 + rate / 100) ** 18
    const pricePerPound = 15.4 + t.escalation.ratePct * 1.9
    return {
      name: t.name,
      accruedAnnualPension: t.accruedAnnualPension,
      revaluationRatePct: rate,
      yearsRevalued: 18,
      pensionAtRetirement,
      escalationInPaymentPct: t.escalation.ratePct,
      annuityInterestRatePct: set.marketInputs.tvcAnnuityRateLevelPct,
      annuityPricePerPound: pricePerPound,
      annuityCost: pensionAtRetirement * pricePerPound,
      isGmp: t.isGmp,
    }
  })

  const pensionAtRetirement = tranches.reduce((s, t) => s + t.pensionAtRetirement, 0)
  const annuityCostAtRetirement = tranches.reduce((s, t) => s + t.annuityCost, 0)
  const discountRate = set.marketInputs.tvcAnnuityRateLevelPct
  const replacementCost = annuityCostAtRetirement / (1 + discountRate / 100) ** years
  const sustainable = (cetv - request.initialAdviceFee) * 0.042

  return {
    tvc: {
      cashEquivalentTransferValue: cetv,
      estimatedReplacementCost: replacementCost,
      difference: cetv - replacementCost,
      retirementAgeUsed: nra,
      termYears: years,
      giltYieldUsedPct: set.marketInputs.giltYield5To10Pct,
      discountRateUsedPct: discountRate,
      annuityCostAtRetirement,
      pensionAtRetirement,
      wording: `You have been offered a cash equivalent transfer value of £${Math.round(cetv).toLocaleString('en-GB')} in exchange for you giving up any future claims to a pension from the scheme. Will I be better or worse off by transferring? It could cost you £${Math.round(replacementCost).toLocaleString('en-GB')} to obtain a comparable level of income from an insurer. This means the same retirement income could cost you £${Math.round(replacementCost - cetv).toLocaleString('en-GB')} more by transferring.`,
      notes: [
        "The estimated replacement cost is based on the income the scheme would pay at its normal retirement age (or the earliest age an unreduced pension is available), including a spouse's pension, for an average healthy person, using today's costs.",
        'The estimated replacement value takes into account investment returns after product charges that you might obtain from risk-free investments.',
        'Charges have been taken into account. The transfer value shown is after any adviser charge that would be taken from it.',
      ],
      tranches,
    },
    criticalYields: {
      typeAAnnuityMatchPct: 4.46,
      typeBPclsAndReducedPensionPct: 4.33,
      drawdownHurdleRatePct: 5.5,
      schemePcls: pensionAtRetirement * db.pclsCommutationFactor * 0.25,
      residualPensionAfterPcls: pensionAtRetirement * 0.75,
      converged: true,
    },
    sustainableRealIncomeFromTransfer: sustainable,
    incomeComparison: Array.from({ length: (request.planEndAge - nra) / 5 + 1 }, (_, i) => {
      const age = nra + i * 5
      const t = i * 5
      return {
        age,
        schemeIncomeNominal: pensionAtRetirement * 1.025 ** t,
        schemeIncomeReal: (pensionAtRetirement * 1.025 ** t) / 1.02 ** t,
        drawdownIncomeReal: sustainable,
        residualFundReal: Math.max(0, (cetv - request.initialAdviceFee) * (1 + (request.aptaGrowthPct - 2 - 4.2) / 100) ** t),
        schemeDeathBenefitReal: (pensionAtRetirement * 0.5 * 12) / 1.02 ** t,
      }
    }),
    stressTests: [
      { name: 'Base case', sustainableRealIncome: sustainable, change: 0 },
      { name: 'Growth 2% lower', sustainableRealIncome: sustainable * 0.75, change: sustainable * -0.25 },
      { name: 'Inflation 1% higher', sustainableRealIncome: sustainable * 0.88, change: sustainable * -0.12 },
      { name: 'Live 5 years longer', sustainableRealIncome: sustainable * 0.9, change: sustainable * -0.1 },
      { name: '20% fall in year 1', sustainableRealIncome: sustainable * 0.81, change: sustainable * -0.19 },
    ],
    summary: {
      initialAdviceFee: request.initialAdviceFee,
      revaluedMonthlyIncome: pensionAtRetirement / 12,
      paybackMonths: 5,
      firstYearChargesProposed: request.initialAdviceFee + cetv * 0.012,
      ongoingAnnualChargesProposed: cetv * 0.012,
      firstYearChargesCeding: 0,
      firstYearChargesWorkplaceDefault: request.workplaceDefaultChargePct
        ? (cetv * request.workplaceDefaultChargePct) / 100
        : undefined,
    },
    lifeExpectancyAtRetirement: 19.6,
    warnings: [
      'Transferring out of a defined benefit scheme is unlikely to be in most clients’ best interests (COBS 19.1.6G).',
      'The CETV guarantee expires on ' + db.cetvGuaranteeExpiry + '.',
    ],
    engineVersion: ENGINE,
    calculatedAtUtc: new Date().toISOString(),
    assumptionSet: assumptionRef(request.assumptionSetId),
  }
}

export function cashflowResult(request: CashflowCalcRequest): CashflowResultDto {
  const set = assumptionSets.find((s) => s.id === request.assumptionSetId) ?? assumptionSets[0]
  const inflation = request.overrides?.inflationPct ?? set.inflationPct
  const startAge = Math.min(...request.assets.map(() => request.person.retirementAge - 12), request.person.retirementAge)
  const rows: CashflowRowDto[] = []
  const balances = request.assets.map((a) => a.value)

  for (let i = 0; request.person.retirementAge - 12 + i <= request.planEndAge; i++) {
    const age = Math.max(startAge, request.person.retirementAge - 12) + i
    const retired = age >= request.person.retirementAge
    const real = 1 / (1 + inflation / 100) ** i

    const employmentIncome = request.incomes
      .filter((inc) => inc.kind === 'employment' && age >= inc.fromAge && (inc.toAge === undefined || age <= inc.toAge))
      .reduce((s, inc) => s + inc.annualAmount * (1 + inc.growthPct / 100) ** i, 0)
    const statePensionIncome = age >= 67 ? (request.person.statePensionForecastWeekly ?? 221) * 52 * (1 + set.statePensionIncreasePct / 100) ** i : 0
    const expenses = request.expenses
      .filter((e) => age >= e.fromAge && (e.toAge === undefined || age <= e.toAge))
      .reduce((s, e) => s + e.annualAmount * (1 + inflation / 100) ** i, 0)

    let gross = employmentIncome + statePensionIncome
    let withdrawals = 0
    if (retired && gross < expenses) {
      withdrawals = expenses - gross
      let need = withdrawals
      for (let a = 0; a < balances.length && need > 0; a++) {
        const take = Math.min(balances[a], need)
        balances[a] -= take
        need -= take
      }
      gross += withdrawals
    }
    for (let a = 0; a < balances.length; a++) {
      balances[a] = balances[a] * (1 + request.assets[a].growthPct / 100) + (retired ? 0 : request.assets[a].annualContribution + request.assets[a].employerContribution)
    }

    const taxable = Math.max(0, gross - 12_570)
    const incomeTax = taxable * (gross > 50_270 ? 0.3 : 0.2)
    const netIncome = gross - incomeTax
    const shortfall = Math.max(0, expenses - netIncome)
    const totalAssets = balances.reduce((s, b) => s + b, 0)

    rows.push({
      year: i + 1,
      age,
      employmentIncome,
      statePensionIncome,
      dbPensionIncome: 0,
      otherIncome: 0,
      pensionWithdrawalsTaxable: withdrawals,
      taxFreeCash: 0,
      isaWithdrawals: 0,
      giaWithdrawals: 0,
      cashWithdrawals: 0,
      incomeTax,
      nationalInsurance: retired ? 0 : employmentIncome * 0.06,
      capitalGainsTax: 0,
      netIncome,
      netIncomeReal: netIncome * real,
      expenses,
      surplus: Math.max(0, netIncome - expenses),
      shortfall,
      contributions: retired ? 0 : request.assets.reduce((s, a) => s + a.annualContribution + a.employerContribution, 0),
      assets: request.assets.map((a, idx) => ({
        name: a.name,
        kind: a.kind,
        value: balances[idx],
        valueReal: balances[idx] * real,
      })),
      totalAssets,
      totalAssetsReal: totalAssets * real,
      warnings: [],
    })
  }

  const firstShortfall = rows.find((r) => r.shortfall > 0)
  const last = rows[rows.length - 1]
  return {
    rows,
    firstShortfallAge: firstShortfall?.age,
    totalIncomeTax: rows.reduce((s, r) => s + r.incomeTax, 0),
    totalShortfall: rows.reduce((s, r) => s + r.shortfall, 0),
    legacyAtEnd: last?.totalAssets ?? 0,
    legacyAtEndReal: last?.totalAssetsReal ?? 0,
    lumpSumAllowanceUsed: 0,
    succeeds: !firstShortfall,
    sustainableSpend: (rows[0]?.expenses ?? 0) * 1.1,
    engineVersion: ENGINE,
    calculatedAtUtc: new Date().toISOString(),
    assumptionSet: assumptionRef(request.assumptionSetId),
  }
}

export function stochasticResult(request: CashflowCalcRequest & { seed?: number; paths?: number }): StochasticResultDto {
  const deterministic = cashflowResult(request)
  const spread = (row: { totalAssetsReal: number }, k: number) => Math.max(0, row.totalAssetsReal * k)
  const percentiles = (values: { age: number; year: number; totalAssetsReal: number }[]): PercentileRowDto[] =>
    values.map((r) => ({
      year: r.year,
      age: r.age,
      p5: spread(r, 0.42),
      p10: spread(r, 0.52),
      p25: spread(r, 0.73),
      p50: spread(r, 0.95),
      p75: spread(r, 1.24),
      p90: spread(r, 1.58),
      p95: spread(r, 1.82),
    }))

  return {
    seed: String(request.seed ?? 20_260_907),
    paths: request.paths ?? 1_000,
    probabilityOfSuccess: deterministic.succeeds ? 0.78 : 0.51,
    totalAssetsReal: percentiles(deterministic.rows),
    netIncomeReal: deterministic.rows.map((r) => ({
      year: r.year,
      age: r.age,
      p5: r.netIncomeReal * 0.82,
      p10: r.netIncomeReal * 0.88,
      p25: r.netIncomeReal * 0.95,
      p50: r.netIncomeReal,
      p75: r.netIncomeReal * 1.03,
      p90: r.netIncomeReal * 1.06,
      p95: r.netIncomeReal * 1.09,
    })),
    medianShortfallAge: deterministic.firstShortfallAge,
    worstDecileShortfallAge: (deterministic.firstShortfallAge ?? 88) - 2,
    conservativeness: {
      deterministicAssetsAtEnd: deterministic.legacyAtEndReal,
      medianAssetsAtEnd: deterministic.legacyAtEndReal * 0.95,
      medianIsNoLessConservative: true,
    },
    meanLegacyReal: deterministic.legacyAtEndReal * 1.12,
    engineVersion: ENGINE,
    calculatedAtUtc: new Date().toISOString(),
  }
}
