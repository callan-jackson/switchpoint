import type {
  AnalysisSummary,
  AssumptionSetDto,
  AuditEventDto,
  ChargeScheduleDto,
  ClientDetail,
  ClientSummary,
  FundDto,
  ModelPortfolioDto,
  ProductDetail,
  ProviderDto,
  ReportDto,
  SchemeDto,
} from '@/api/types'

/**
 * Sample data for the mock server. It mirrors the shapes and the demo firm seeded by the API
 * (`Demo Financial Planning Ltd`), with realistic UK providers, charges and fund data, so the
 * pages look and behave the same with `VITE_USE_MOCKS=true` as they do against a live API.
 *
 * Charges here are indicative and illustrative only — they are not scraped rate cards.
 */

export const FIRM_ID = '11111111-1111-1111-1111-111111111111'
export const FIRM_NAME = 'Demo Financial Planning Ltd'

export const users = {
  adviser: {
    id: '18b94d45-95c4-4b4b-bf71-3e3e23131ce6',
    displayName: 'Alex Adviser',
    email: 'adviser@demo.switchpoint.local',
    role: 'adviser',
    firmId: FIRM_ID,
    firmName: FIRM_NAME,
  },
  paraplanner: {
    id: '2b4d8bb2-1f1f-4a5a-9c4c-6ad1f7c1a001',
    displayName: 'Pat Paraplanner',
    email: 'paraplanner@demo.switchpoint.local',
    role: 'paraplanner',
    firmId: FIRM_ID,
    firmName: FIRM_NAME,
  },
  compliance: {
    id: 'fbdb1ddb-98b6-4b73-8c2f-a783fbb720d5',
    displayName: 'Chris Compliance',
    email: 'compliance@demo.switchpoint.local',
    role: 'compliance',
    firmId: FIRM_ID,
    firmName: FIRM_NAME,
  },
} as const

export const DEMO_PASSWORD = 'Demo!Pass123'

// ---------------------------------------------------------------------------
// Charge schedules
// ---------------------------------------------------------------------------

export function emptySchedule(): ChargeScheduleDto {
  return {
    fixedCharges: [],
    fundCharge: { kind: 'fromHoldings' },
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

const investcentreCharges: ChargeScheduleDto = {
  ...emptySchedule(),
  platformCharge: {
    mode: 'marginal',
    bands: [{ upTo: 250_000, annualRatePct: 0.2 }, { upTo: 1_000_000, annualRatePct: 0.15 }, { annualRatePct: 0.1 }],
  },
  dealingCharges: {
    fundDealAmount: 0,
    etfDealAmount: 3.95,
    expectedFundDealsPerYear: 0,
    expectedEtfDealsPerYear: 4,
  },
}

const abrdnCharges: ChargeScheduleDto = {
  ...emptySchedule(),
  platformCharge: {
    mode: 'marginal',
    bands: [{ upTo: 500_000, annualRatePct: 0.35 }, { upTo: 1_000_000, annualRatePct: 0.2 }, { annualRatePct: 0.1 }],
  },
}

const legacyWithProfitsCharges: ChargeScheduleDto = {
  ...emptySchedule(),
  productCharge: { mode: 'wholeOfFund', bands: [{ annualRatePct: 1.05 }] },
  fixedCharges: [
    {
      amount: 3.5,
      frequency: 'monthly',
      indexation: { basis: 'cpi', ratePct: 2 },
      appliesTo: 'wrapper',
      description: 'Policy fee',
    },
  ],
  allocationRatePct: 98,
  exitPenalty: {
    bands: [
      { untilYearsFromStart: 3, ratePct: 3, amount: 0 },
      { untilYearsFromStart: 5, ratePct: 1.5, amount: 0 },
    ],
  },
}

const gppCharges: ChargeScheduleDto = {
  ...emptySchedule(),
  productCharge: { mode: 'marginal', bands: [{ annualRatePct: 0.45 }] },
}

// ---------------------------------------------------------------------------
// Providers, products, funds
// ---------------------------------------------------------------------------

export const providers: ProviderDto[] = [
  { id: 'p-abrdn', name: 'abrdn', kind: 'platform', fcaFirmReferenceNumber: '133456', website: 'https://www.abrdn.com/adviser' },
  { id: 'p-ajbell', name: 'AJ Bell Investcentre', kind: 'platform', fcaFirmReferenceNumber: '108413', website: 'https://www.investcentre.co.uk' },
  { id: 'p-aviva', name: 'Aviva', kind: 'platform', fcaFirmReferenceNumber: '119178', website: 'https://www.aviva.co.uk' },
  { id: 'p-fidelity', name: 'Fidelity Adviser Solutions', kind: 'platform', fcaFirmReferenceNumber: '122169', website: 'https://adviserservices.fidelity.co.uk' },
  { id: 'p-quilter', name: 'Quilter', kind: 'platform', fcaFirmReferenceNumber: '165359', website: 'https://www.quilter.com' },
  { id: 'p-transact', name: 'Transact', kind: 'platform', fcaFirmReferenceNumber: '190856', website: 'https://www.transact-online.co.uk' },
  { id: 'p-legacy', name: 'Legacy Life Assurance', kind: 'insurer', fcaFirmReferenceNumber: '110000' },
]

function product(
  id: string,
  providerId: string,
  providerName: string,
  name: string,
  charges: ChargeScheduleDto,
  at100k: number,
  at500k: number,
  extra: Partial<ProductDetail> = {},
): ProductDetail {
  return {
    id,
    providerId,
    providerName,
    name,
    wrapperTypes: ['Sipp', 'Isa', 'GeneralInvestmentAccount', 'Drawdown'],
    minimumInvestment: 1000,
    allowsFamilyLinking: true,
    fundUniverse: 'wholeOfMarket',
    dataQuality: 'indicative',
    effectiveChargePctAt100k: at100k,
    effectiveChargePctAt500k: at500k,
    currentChargeVersion: 1,
    asAt: '2026-09-01',
    sourceUrl: providers.find((p) => p.id === providerId)?.website,
    chargeVersions: [
      {
        version: 1,
        effectiveFrom: '2026-01-01',
        asAt: '2026-09-01',
        sourceUrl: providers.find((p) => p.id === providerId)?.website,
        dataQuality: 'indicative',
        charges,
      },
    ],
    ...extra,
  }
}

export const products: ProductDetail[] = [
  product('prod-ajbell-sipp', 'p-ajbell', 'AJ Bell Investcentre', 'Investcentre SIPP', investcentreCharges, 0.2, 0.175),
  product('prod-abrdn-wrap', 'p-abrdn', 'abrdn', 'abrdn Wrap SIPP', abrdnCharges, 0.35, 0.31),
  product('prod-aviva-pp', 'p-aviva', 'Aviva', 'Aviva Platform Pension Portfolio', {
    ...emptySchedule(),
    platformCharge: { mode: 'marginal', bands: [{ upTo: 400_000, annualRatePct: 0.3 }, { annualRatePct: 0.15 }] },
  }, 0.3, 0.24),
  product('prod-fidelity-pension', 'p-fidelity', 'Fidelity Adviser Solutions', 'Fidelity Pension', {
    ...emptySchedule(),
    platformCharge: { mode: 'marginal', bands: [{ upTo: 250_000, annualRatePct: 0.25 }, { annualRatePct: 0.1 }] },
    fixedCharges: [
      { amount: 45, frequency: 'annually', indexation: { basis: 'none', ratePct: 0 }, appliesTo: 'sipp', description: 'SIPP wrapper fee' },
    ],
  }, 0.295, 0.184),
  product('prod-quilter-cra', 'p-quilter', 'Quilter', 'Quilter Collective Retirement Account', {
    ...emptySchedule(),
    platformCharge: { mode: 'marginal', bands: [{ upTo: 250_000, annualRatePct: 0.35 }, { annualRatePct: 0.2 }] },
  }, 0.35, 0.26),
  product('prod-transact-pp', 'p-transact', 'Transact', 'Transact Personal Pension', {
    ...emptySchedule(),
    platformCharge: { mode: 'marginal', bands: [{ upTo: 600_000, annualRatePct: 0.3 }, { annualRatePct: 0.2 }] },
    fixedCharges: [
      { amount: 80, frequency: 'annually', indexation: { basis: 'cpi', ratePct: 2 }, appliesTo: 'wrapper', description: 'Annual wrapper fee' },
    ],
  }, 0.38, 0.316),
  product('prod-legacy-pp', 'p-legacy', 'Legacy Life Assurance', 'Pre-2001 Personal Pension (with-profits)', legacyWithProfitsCharges, 1.11, 1.06, {
    wrapperTypes: ['PersonalPension'],
    fundUniverse: 'restricted',
    dataQuality: 'placeholder',
    minimumInvestment: 0,
    allowsFamilyLinking: false,
  }),
]

function fund(
  id: string,
  isin: string,
  name: string,
  managerName: string,
  ocfPct: number,
  equity: number,
  fixedInterest: number,
  extra: Partial<FundDto> = {},
): FundDto {
  const cash = Math.max(0, 100 - equity - fixedInterest)
  return {
    id,
    isin,
    name,
    managerName,
    shareClass: 'Acc',
    type: 'oeic',
    iaSector: 'Global',
    ocfPct,
    transactionCostsPct: 0.05,
    assetAllocation: {
      equityPct: equity,
      fixedInterestPct: fixedInterest,
      propertyPct: 0,
      cashPct: cash,
      alternativesPct: 0,
    },
    srri: equity > 80 ? 6 : equity > 55 ? 5 : 4,
    statistics: {},
    asAt: '2026-09-01',
    ...extra,
  }
}

export const funds: FundDto[] = [
  fund('f-1', 'GB00B41YBW71', 'Fundsmith Equity T Acc', 'Fundsmith', 0.94, 98, 0, {
    srri: 5,
    statistics: { return1YPct: 9.4, return3YPct: 21.7, return5YPct: 48.2, volatility3YPct: 12.8, sharpe3Y: 0.51, maxDrawdown3YPct: -16.4, morningstarRating: 4, medalistRating: 'Silver' },
  }),
  fund('f-2', 'GB00B41XG308', 'Vanguard LifeStrategy 100% Equity', 'Vanguard', 0.22, 99, 0, {
    statistics: { return1YPct: 11.2, return3YPct: 26.4, return5YPct: 55.1, volatility3YPct: 11.9, sharpe3Y: 0.62, morningstarRating: 4 },
  }),
  fund('f-3', 'GB00B3ZHN960', 'Vanguard LifeStrategy 40% Equity', 'Vanguard', 0.22, 40, 58, {
    iaSector: 'Mixed Investment 20-60% Shares',
    statistics: { return1YPct: 5.1, return3YPct: 9.8, return5YPct: 15.2, volatility3YPct: 6.4, morningstarRating: 3 },
  }),
  fund('f-4', 'GB00B3TYHH97', 'Vanguard LifeStrategy 60% Equity', 'Vanguard', 0.22, 60, 38, {
    iaSector: 'Mixed Investment 40-85% Shares',
    statistics: { return1YPct: 7.3, return3YPct: 14.9, return5YPct: 26.8, volatility3YPct: 8.1, morningstarRating: 4 },
  }),
  fund('f-5', 'GB00B4PQW151', 'Vanguard LifeStrategy 80% Equity', 'Vanguard', 0.22, 80, 19, {
    iaSector: 'Mixed Investment 40-85% Shares',
    statistics: { return1YPct: 9.1, return3YPct: 20.2, return5YPct: 38.4, volatility3YPct: 9.8, morningstarRating: 4 },
  }),
  fund('f-6', 'IE00B3XXRP09', 'Vanguard S&P 500 UCITS ETF', 'Vanguard', 0.07, 100, 0, {
    type: 'etf',
    iaSector: 'North America',
    statistics: { return1YPct: 13.8, return3YPct: 34.1, return5YPct: 82.7, volatility3YPct: 14.2, sharpe3Y: 0.74, morningstarRating: 5, medalistRating: 'Gold' },
  }),
  fund('f-7', 'IE00B4L5Y983', 'iShares Core MSCI World UCITS ETF', 'BlackRock', 0.2, 100, 0, {
    type: 'etf',
    statistics: { return1YPct: 12.1, return3YPct: 29.6, return5YPct: 68.3, volatility3YPct: 12.9, morningstarRating: 5, medalistRating: 'Gold' },
  }),
  fund('f-8', 'GB00BD3RZ368', 'Royal London Short Duration Gilts M Acc', 'Royal London', 0.15, 0, 98, {
    iaSector: 'UK Gilts',
    srri: 2,
    statistics: { return1YPct: 3.9, return3YPct: 6.1, volatility3YPct: 3.2, yieldPct: 4.1, morningstarRating: 3 },
  }),
  fund('f-9', 'GB00B7VVXV22', 'HSBC FTSE All-World Index C Acc', 'HSBC', 0.13, 99, 0, {
    statistics: { return1YPct: 11.7, return3YPct: 28.2, return5YPct: 63.9, morningstarRating: 4, medalistRating: 'Silver' },
  }),
  fund('f-10', 'GB00B8BYZP81', 'L&G Global Property Index I Acc', 'Legal & General', 0.28, 0, 0, {
    iaSector: 'Property Other',
    assetAllocation: { equityPct: 5, fixedInterestPct: 0, propertyPct: 93, cashPct: 2, alternativesPct: 0 },
    srri: 5,
    statistics: { return1YPct: 2.4, return3YPct: -4.9, volatility3YPct: 15.1, yieldPct: 3.6, morningstarRating: 2 },
  }),
]

export const modelPortfolios: ModelPortfolioDto[] = [
  {
    id: 'mps-bal',
    providerId: 'p-abrdn',
    providerName: 'abrdn',
    name: 'Sustainable Balanced (risk 5)',
    riskLevel: 5,
    mpsFeePct: 0.15,
    blendedOcfPct: 0.19,
    totalInvestmentChargePct: 0.34,
    holdings: [
      { fundId: 'f-4', isin: 'GB00B3TYHH97', name: 'Vanguard LifeStrategy 60% Equity', weightPct: 45, ocfPct: 0.22 },
      { fundId: 'f-7', isin: 'IE00B4L5Y983', name: 'iShares Core MSCI World UCITS ETF', weightPct: 35, ocfPct: 0.2 },
      { fundId: 'f-8', isin: 'GB00BD3RZ368', name: 'Royal London Short Duration Gilts M Acc', weightPct: 20, ocfPct: 0.15 },
    ],
  },
  {
    id: 'mps-growth',
    providerId: 'p-abrdn',
    providerName: 'abrdn',
    name: 'Global Growth (risk 6)',
    riskLevel: 6,
    mpsFeePct: 0.15,
    blendedOcfPct: 0.16,
    totalInvestmentChargePct: 0.31,
    holdings: [
      { fundId: 'f-6', isin: 'IE00B3XXRP09', name: 'Vanguard S&P 500 UCITS ETF', weightPct: 40, ocfPct: 0.07 },
      { fundId: 'f-9', isin: 'GB00B7VVXV22', name: 'HSBC FTSE All-World Index C Acc', weightPct: 45, ocfPct: 0.13 },
      { fundId: 'f-8', isin: 'GB00BD3RZ368', name: 'Royal London Short Duration Gilts M Acc', weightPct: 15, ocfPct: 0.15 },
    ],
  },
]

// ---------------------------------------------------------------------------
// Assumption sets
// ---------------------------------------------------------------------------

export const assumptionSets: AssumptionSetDto[] = [
  {
    id: 'as-fca',
    name: 'FCA standard 2026/27',
    isFcaStandard: true,
    version: 1,
    growthLowerPct: 2,
    growthIntermediatePct: 5,
    growthHigherPct: 8,
    inflationPct: 2,
    earningsGrowthPct: 3.5,
    rpiInflationPct: 3,
    chargeInflationPct: 2,
    preRetirementProductChargePct: 0.4,
    annuityExpenseLoadingPct: 4,
    spouseAgeGapYears: 3,
    mortalityBasis: 'onsNationalLifeTables2020_22',
    statePensionIncreasePct: 3.5,
    taxYear: '2026/27',
    projectionBasis: 'real',
    marketInputs: {
      giltYieldUpTo5Pct: 4.2,
      giltYield5To10Pct: 4.4,
      giltYield10To15Pct: 4.6,
      giltYieldOver15Pct: 4.7,
      tvcAnnuityRateRpiLinkedPct: 0.8,
      tvcAnnuityRateLevelPct: 4,
      cobs13YPct: 1,
      asAt: '2026-08-15',
    },
  },
  {
    id: 'as-firm',
    firmId: FIRM_ID,
    name: 'Cautious firm default',
    isFcaStandard: false,
    version: 1,
    growthLowerPct: 1,
    growthIntermediatePct: 4,
    growthHigherPct: 7,
    inflationPct: 2,
    earningsGrowthPct: 3,
    rpiInflationPct: 3,
    chargeInflationPct: 2,
    preRetirementProductChargePct: 0.4,
    annuityExpenseLoadingPct: 4,
    spouseAgeGapYears: 3,
    mortalityBasis: 'onsNationalLifeTables2020_22',
    statePensionIncreasePct: 3,
    taxYear: '2026/27',
    projectionBasis: 'real',
    marketInputs: {
      giltYieldUpTo5Pct: 4.2,
      giltYield5To10Pct: 4.4,
      giltYield10To15Pct: 4.6,
      giltYieldOver15Pct: 4.7,
      tvcAnnuityRateRpiLinkedPct: 0.8,
      tvcAnnuityRateLevelPct: 4,
      cobs13YPct: 1,
      asAt: '2026-08-15',
    },
  },
]

// ---------------------------------------------------------------------------
// Clients and schemes
// ---------------------------------------------------------------------------

const NOW = '2026-09-07T07:34:57Z'

function scheme(partial: Partial<SchemeDto> & Pick<SchemeDto, 'id' | 'clientId' | 'type' | 'productName' | 'currentValue'>): SchemeDto {
  return {
    transferValue: partial.currentValue,
    netTransferValue: partial.currentValue,
    valuationDate: '2026-08-31',
    charges: emptySchedule(),
    guarantees: {
      withProfits: false,
      marketValueReductionPct: 0,
      terminalBonus: 0,
      loyaltyBonusPct: 0,
    },
    inDrawdown: false,
    contributions: [],
    holdings: [],
    createdAtUtc: NOW,
    updatedAtUtc: NOW,
    ...partial,
  }
}

export const clients: ClientDetail[] = [
  {
    id: 'c-sarah',
    fullName: 'Ms Sarah Mitchell',
    age: 55,
    title: 'Ms',
    firstName: 'Sarah',
    lastName: 'Mitchell',
    dateOfBirth: '1971-03-14',
    sex: 'female',
    email: 'sarah.mitchell@example.com',
    phone: '07700 900123',
    address: { line1: '14 Beacon Road', town: 'Crowborough', county: 'East Sussex', postcode: 'TN6 1AB', country: 'United Kingdom' },
    maritalStatus: 'married',
    employmentStatus: 'employed',
    annualSalary: 58_000,
    targetRetirementAge: 67,
    taxRegime: 'restOfUk',
    riskProfile: 5,
    health: 'standard',
    isSmoker: false,
    statePension: { forecastWeeklyAmount: 241.3, qualifyingYears: 34 },
    nationalInsuranceNumberMasked: '******56A',
    externalReference: { source: 'manual' },
    createdAtUtc: NOW,
    updatedAtUtc: NOW,
    schemes: [
      scheme({
        id: 's-sarah-gpp',
        clientId: 'c-sarah',
        type: 'occupationalMoneyPurchase',
        providerId: 'p-aviva',
        providerName: 'Aviva',
        productName: 'Employer Group Personal Pension',
        policyNumber: 'GPP-88213',
        currentValue: 96_500,
        startDate: '2015-04-06',
        charges: gppCharges,
        weightedOcfPct: 0.22,
        contributions: [
          { payer: 'member', amount: 290, frequency: 'monthly', escalationPct: 3, isGrossOfTaxRelief: true },
          { payer: 'employer', amount: 435, frequency: 'monthly', escalationPct: 3, isGrossOfTaxRelief: true },
        ],
        holdings: [{ name: 'Vanguard LifeStrategy 60% Equity', weightPct: 100, isin: 'GB00B3TYHH97', ocfPct: 0.22 }],
      }),
      scheme({
        id: 's-sarah-legacy',
        clientId: 'c-sarah',
        type: 'personalPension',
        providerId: 'p-legacy',
        providerName: 'Legacy Life Assurance',
        productName: 'Legacy Personal Pension (with-profits)',
        policyNumber: 'LP-4471902',
        currentValue: 142_000,
        transferValue: 138_000,
        netTransferValue: 138_000,
        startDate: '1998-06-01',
        charges: legacyWithProfitsCharges,
        weightedOcfPct: 0.75,
        guarantees: {
          guaranteedAnnuityRatePct: 8.5,
          protectedTaxFreeCashPct: 32,
          withProfits: true,
          marketValueReductionPct: 2.8,
          terminalBonus: 6_400,
          loyaltyBonusPct: 0.5,
        },
        contributions: [
          { payer: 'member', amount: 187.5, frequency: 'monthly', escalationPct: 3, isGrossOfTaxRelief: true },
        ],
        holdings: [{ name: 'With-Profits Fund (Series 2)', weightPct: 100, ocfPct: 0.75 }],
      }),
    ],
  },
  {
    id: 'c-david',
    fullName: 'Mr David Okafor',
    age: 57,
    title: 'Mr',
    firstName: 'David',
    lastName: 'Okafor',
    dateOfBirth: '1969-06-15',
    sex: 'male',
    email: 'david.okafor@example.com',
    phone: '07700 900456',
    address: { line1: '3 Wealden Rise', town: 'Sevenoaks', county: 'Kent', postcode: 'TN13 2FG', country: 'United Kingdom' },
    maritalStatus: 'married',
    employmentStatus: 'employed',
    annualSalary: 92_000,
    targetRetirementAge: 65,
    taxRegime: 'restOfUk',
    riskProfile: 4,
    health: 'standard',
    isSmoker: false,
    statePension: { forecastWeeklyAmount: 241.3, qualifyingYears: 38 },
    nationalInsuranceNumberMasked: '******21B',
    externalReference: { source: 'intelliflo', externalId: 'IO-99213' },
    createdAtUtc: NOW,
    updatedAtUtc: NOW,
    schemes: [
      scheme({
        id: 's-david-sipp',
        clientId: 'c-david',
        type: 'sipp',
        providerId: 'p-ajbell',
        providerName: 'AJ Bell Investcentre',
        productName: 'AJ Bell Investcentre SIPP',
        policyNumber: 'SIPP-220145',
        currentValue: 210_000,
        startDate: '2011-09-01',
        charges: investcentreCharges,
        weightedOcfPct: 0.19,
        holdings: [
          { name: 'Vanguard LifeStrategy 60% Equity', weightPct: 60, isin: 'GB00B3TYHH97', ocfPct: 0.22 },
          { name: 'Vanguard S&P 500 UCITS ETF', weightPct: 25, isin: 'IE00B3XXRP09', ocfPct: 0.07 },
          { name: 'Royal London Short Duration Gilts M Acc', weightPct: 15, isin: 'GB00BD3RZ368', ocfPct: 0.15 },
        ],
      }),
      scheme({
        id: 's-david-db',
        clientId: 'c-david',
        type: 'definedBenefit',
        productName: 'Wealden Engineering Pension Scheme',
        policyNumber: 'WEPS-2211',
        currentValue: 486_000,
        transferValue: 486_000,
        netTransferValue: 486_000,
        selectedRetirementAge: 65,
        definedBenefit: {
          dateOfLeaving: '2008-04-05',
          normalRetirementAge: 65,
          cetvGuaranteeExpiry: '2026-12-01',
          spousePensionPct: 50,
          guaranteePeriodYears: 5,
          pclsCommutationFactor: 20,
          maxPclsPct: 25,
          earlyRetirementReductionPct: 4,
          earliestUnreducedAge: 65,
          bridgingPensionAnnual: 0,
          fundingStatus: 'fullyFunded',
          tranches: [
            {
              name: 'Pre-97 GMP',
              accruedAnnualPension: 1_850,
              revaluation: { basis: 'fixed', ratePct: 4.75 },
              escalation: { basis: 'none', ratePct: 0 },
              isGmp: true,
            },
            {
              name: 'Pre-97 excess',
              accruedAnnualPension: 3_200,
              revaluation: { basis: 'lpiRpi', ratePct: 3, capPct: 5 },
              escalation: { basis: 'none', ratePct: 0 },
              isGmp: false,
            },
            {
              name: 'Post-97',
              accruedAnnualPension: 9_400,
              revaluation: { basis: 'lpiCpi', ratePct: 2.5, capPct: 5 },
              escalation: { basis: 'lpiRpi', ratePct: 3, capPct: 5 },
              isGmp: false,
            },
          ],
        },
      }),
    ],
  },
  {
    id: 'c-priya',
    fullName: 'Dr Priya Shah',
    age: 44,
    title: 'Dr',
    firstName: 'Priya',
    lastName: 'Shah',
    dateOfBirth: '1982-09-02',
    sex: 'female',
    maritalStatus: 'single',
    employmentStatus: 'selfEmployed',
    annualSalary: 118_000,
    targetRetirementAge: 60,
    taxRegime: 'scotland',
    riskProfile: 6,
    health: 'standard',
    isSmoker: false,
    statePension: { forecastWeeklyAmount: 221.2, qualifyingYears: 22 },
    externalReference: { source: 'manual' },
    createdAtUtc: NOW,
    updatedAtUtc: NOW,
    schemes: [
      scheme({
        id: 's-priya-sipp',
        clientId: 'c-priya',
        type: 'sipp',
        providerId: 'p-transact',
        providerName: 'Transact',
        productName: 'Workplace SIPP',
        currentValue: 265_000,
        charges: emptySchedule(),
        weightedOcfPct: 0.2,
        holdings: [{ name: 'iShares Core MSCI World UCITS ETF', weightPct: 100, isin: 'IE00B4L5Y983', ocfPct: 0.2 }],
        contributions: [{ payer: 'member', amount: 2_000, frequency: 'monthly', escalationPct: 0, isGrossOfTaxRelief: true }],
      }),
      scheme({
        id: 's-priya-isa',
        clientId: 'c-priya',
        type: 'isa',
        providerId: 'p-transact',
        providerName: 'Transact',
        productName: 'Stocks & Shares ISA',
        currentValue: 61_000,
        weightedOcfPct: 0.13,
        holdings: [{ name: 'HSBC FTSE All-World Index C Acc', weightPct: 100, isin: 'GB00B7VVXV22', ocfPct: 0.13 }],
      }),
    ],
  },
]

export function clientSummary(client: ClientDetail): ClientSummary {
  return {
    id: client.id,
    fullName: client.fullName,
    dateOfBirth: client.dateOfBirth,
    age: client.age,
    email: client.email,
    riskProfile: client.riskProfile,
    schemeCount: client.schemes.length,
    totalPensionValue: client.schemes
      .filter((s) => s.type !== 'isa' && s.type !== 'generalInvestmentAccount')
      .reduce((sum, s) => sum + s.currentValue, 0),
    updatedAtUtc: client.updatedAtUtc,
  }
}

// ---------------------------------------------------------------------------
// Analyses, reports, audit
// ---------------------------------------------------------------------------

export const analyses: AnalysisSummary[] = [
  {
    id: 'an-switch-1',
    clientId: 'c-sarah',
    kind: 'pensionSwitch',
    title: 'Sarah Mitchell — consolidation to Investcentre SIPP',
    status: 'calculated',
    version: 2,
    calculatedAtUtc: '2026-09-05T14:22:00Z',
    updatedAtUtc: '2026-09-05T14:22:00Z',
    createdBy: users.adviser.id,
  },
  {
    id: 'an-db-1',
    clientId: 'c-david',
    kind: 'dbTransfer',
    title: 'David Okafor — Wealden Engineering CETV review',
    status: 'locked',
    version: 3,
    calculatedAtUtc: '2026-09-03T09:10:00Z',
    updatedAtUtc: '2026-09-03T09:40:00Z',
    createdBy: users.adviser.id,
  },
  {
    id: 'an-cf-1',
    clientId: 'c-priya',
    kind: 'cashflow',
    title: 'Priya Shah — retire at 60 plan',
    status: 'draft',
    version: 0,
    updatedAtUtc: '2026-09-06T16:05:00Z',
    createdBy: users.paraplanner.id,
  },
]

export const reports: ReportDto[] = [
  {
    id: 'rep-1',
    clientId: 'c-david',
    analysisId: 'an-db-1',
    analysisVersion: 3,
    analysisResultHash: 'b35017a60a1c6b03f98bdbdba999982209e2ff36d84749e769bf22634cfe385c',
    kind: 'dbTransfer',
    format: 'json',
    templateVersion: '2026.09.1',
    generatedBy: users.adviser.id,
    sha256: 'f627314dd8e7ee3bb7421ecfbce8941556f0ab033cee939c29945ba9df537d94',
    sizeBytes: 16_272,
    generatedAtUtc: '2026-09-03T09:41:12Z',
    downloadUrl: '/api/v1/reports/rep-1/download',
  },
]

export const auditEvents: AuditEventDto[] = Array.from({ length: 24 }, (_, i) => {
  const seq = 24 - i
  const kinds = [
    { entityType: 'PensionSwitchAnalysis', action: 'Calculated', entityId: 'an-switch-1' },
    { entityType: 'Client', action: 'Updated', entityId: 'c-sarah' },
    { entityType: 'Report', action: 'Generated', entityId: 'rep-1' },
    { entityType: 'DbTransferAnalysis', action: 'Locked', entityId: 'an-db-1' },
    { entityType: 'Scheme', action: 'Created', entityId: 's-david-db' },
    { entityType: 'Calculation', action: 'Preview', entityId: undefined },
  ]
  const k = kinds[i % kinds.length]
  return {
    id: `ev-${seq}`,
    sequence: seq,
    userId: seq % 3 === 0 ? users.paraplanner.id : users.adviser.id,
    userName: seq % 3 === 0 ? users.paraplanner.displayName : users.adviser.displayName,
    occurredAtUtc: new Date(Date.UTC(2026, 8, 6, 8, 0, 0) + seq * 37 * 60_000).toISOString(),
    entityType: k.entityType,
    entityId: k.entityId,
    action: k.action,
    payload: JSON.stringify({ id: k.entityId ?? null, version: (seq % 4) + 1 }),
    previousHash: `${(seq - 1).toString(16).padStart(4, '0')}`.repeat(16).slice(0, 64),
    hash: `${seq.toString(16).padStart(4, '0')}`.repeat(16).slice(0, 64),
  }
})
