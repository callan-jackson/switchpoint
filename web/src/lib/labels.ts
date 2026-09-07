import type {
  AnalysisKind,
  AnalysisStatus,
  CashflowAssetKind,
  CashflowIncomeKind,
  ChargeBasis,
  ContributionPayer,
  CrystallisationStrategy,
  DataQuality,
  DrawdownRule,
  EmploymentStatus,
  Frequency,
  FundChargeKind,
  FundType,
  FundUniverse,
  IndexBasis,
  IndexationBasis,
  MaritalStatus,
  ProviderKind,
  ReportFormat,
  ReportKind,
  SchemeFundingStatus,
  SchemeType,
  Sex,
  SwitchVerdict,
  TaxRegime,
  TieredChargeMode,
  UserRole,
} from '@/api/types'

/**
 * Display labels for contract enums. Wire values are camelCase (verified against the API);
 * anything missing falls back to `humanise` from `@/lib/format`.
 */
export const schemeTypeLabels: Record<SchemeType, string> = {
  personalPension: 'Personal pension',
  stakeholderPension: 'Stakeholder pension',
  sipp: 'SIPP',
  occupationalMoneyPurchase: 'Occupational money purchase',
  definedBenefit: 'Defined benefit',
  section32: 'Section 32 buy-out',
  retirementAnnuityContract: 'Retirement annuity contract',
  isa: 'ISA',
  generalInvestmentAccount: 'General investment account',
  onshoreBond: 'Onshore bond',
  offshoreBond: 'Offshore bond',
  drawdownPlan: 'Drawdown plan',
}

export const analysisKindLabels: Record<AnalysisKind, string> = {
  pensionSwitch: 'Pension switch',
  dbTransfer: 'DB transfer',
  cashflow: 'Cashflow',
}

export const analysisStatusLabels: Record<AnalysisStatus, string> = {
  draft: 'Draft',
  calculated: 'Calculated',
  locked: 'Locked',
}

export const verdictLabels: Record<SwitchVerdict, string> = {
  switchCandidate: 'Switch candidate',
  consider: 'Consider',
  retain: 'Retain',
  refer: 'Refer',
}

export const dataQualityLabels: Record<DataQuality, string> = {
  verified: 'Verified',
  indicative: 'Indicative',
  placeholder: 'Placeholder',
}

export const cashflowAssetKindLabels: Record<CashflowAssetKind, string> = {
  uncrystallisedPension: 'Uncrystallised pension',
  drawdown: 'Drawdown',
  isa: 'ISA',
  generalInvestmentAccount: 'GIA',
  cash: 'Cash',
  property: 'Property',
  onshoreBond: 'Onshore bond',
}

export const cashflowIncomeKindLabels: Record<CashflowIncomeKind, string> = {
  employment: 'Employment',
  selfEmployment: 'Self-employment',
  rental: 'Rental',
  definedBenefitPension: 'DB pension',
  annuity: 'Annuity',
  statePension: 'State pension',
  other: 'Other',
}

export const frequencyLabels: Record<Frequency, string> = {
  single: 'One-off',
  annually: 'Annually',
  quarterly: 'Quarterly',
  monthly: 'Monthly',
}

export const contributionPayerLabels: Record<ContributionPayer, string> = {
  member: 'Member',
  employer: 'Employer',
  thirdParty: 'Third party',
}

export const tieredChargeModeLabels: Record<TieredChargeMode, string> = {
  marginal: 'Marginal (each band charged on its slice)',
  wholeOfFund: 'Whole of fund (one rate on the total)',
}

export const fundChargeKindLabels: Record<FundChargeKind, string> = {
  none: 'None',
  explicit: 'Explicit OCF',
  fromHoldings: 'Weighted from holdings',
}

export const indexationBasisLabels: Record<IndexationBasis, string> = {
  none: 'None',
  cpi: 'CPI',
  fixed: 'Fixed',
}

export const indexBasisLabels: Record<IndexBasis, string> = {
  none: 'None',
  fixed: 'Fixed',
  cpi: 'CPI',
  rpi: 'RPI',
  lpiCpi: 'LPI (CPI)',
  lpiRpi: 'LPI (RPI)',
  section148: 'Section 148 orders',
}

export const fundingStatusLabels: Record<SchemeFundingStatus, string> = {
  fullyFunded: 'Fully funded',
  deficit: 'In deficit',
  pensionProtectionFund: 'PPF assessment',
}

export const maritalStatusLabels: Record<MaritalStatus, string> = {
  single: 'Single',
  married: 'Married',
  civilPartnership: 'Civil partnership',
  divorced: 'Divorced',
  widowed: 'Widowed',
  cohabiting: 'Cohabiting',
}

export const employmentStatusLabels: Record<EmploymentStatus, string> = {
  employed: 'Employed',
  selfEmployed: 'Self-employed',
  retired: 'Retired',
  notWorking: 'Not working',
  director: 'Director',
}

export const taxRegimeLabels: Record<TaxRegime, string> = {
  restOfUk: 'Rest of UK',
  scotland: 'Scotland',
}

export const sexLabels: Record<Sex, string> = { male: 'Male', female: 'Female' }

export const userRoleLabels: Record<UserRole, string> = {
  adviser: 'Adviser',
  paraplanner: 'Paraplanner',
  compliance: 'Compliance',
  firmAdmin: 'Firm admin',
  platformAdmin: 'Platform admin',
}

export const providerKindLabels: Record<ProviderKind, string> = {
  platform: 'Platform',
  insurer: 'Insurer',
  sippOperator: 'SIPP operator',
  fundManager: 'Fund manager',
  mps: 'Model portfolio service',
}

export const fundTypeLabels: Record<FundType, string> = {
  oeic: 'OEIC',
  unitTrust: 'Unit trust',
  etf: 'ETF',
  investmentTrust: 'Investment trust',
  modelPortfolio: 'Model portfolio',
  cash: 'Cash',
}

export const fundUniverseLabels: Record<FundUniverse, string> = {
  wholeOfMarket: 'Whole of market',
  restricted: 'Restricted',
}

export const crystallisationLabels: Record<CrystallisationStrategy, string> = {
  pclsUpFront: 'PCLS up front',
  phasedUfpls: 'Phased UFPLS',
  phasedDrawdown: 'Phased drawdown',
}

export const drawdownRuleLabels: Record<DrawdownRule, string> = {
  gapFill: 'Fill the income gap',
  fixedAmount: 'Fixed amount',
  percentOfPot: 'Percent of pot',
}

export const chargeBasisLabels: Record<ChargeBasis, string> = {
  nonContingent: 'Non-contingent (abridged/full advice fee payable either way)',
  contingent: 'Contingent (carve-out required)',
}

export const reportKindLabels: Record<ReportKind, string> = {
  suitability: 'Suitability report',
  pensionSwitch: 'Pension switch',
  dbTransfer: 'DB transfer',
  cashflow: 'Cashflow',
  fundComparison: 'Fund comparison',
}

export const reportFormatLabels: Record<ReportFormat, string> = {
  pdf: 'PDF',
  docx: 'Word',
  json: 'JSON',
}

/** Route to open a persisted analysis of the given kind. */
export function analysisPath(kind: AnalysisKind, id: string): string {
  switch (kind) {
    case 'pensionSwitch':
      return `/pension-switch/${id}`
    case 'dbTransfer':
      return `/db-transfer/${id}`
    case 'cashflow':
      return `/cashflow/${id}`
  }
}

/** DC scheme types that can be ceded in a pension switch. */
export const DC_SCHEME_TYPES: SchemeType[] = [
  'personalPension',
  'stakeholderPension',
  'sipp',
  'occupationalMoneyPurchase',
  'section32',
  'retirementAnnuityContract',
  'drawdownPlan',
]

/** Turn a label record into `<Select>` options. */
export function optionsFrom<T extends string>(labels: Record<T, string>): { value: T; label: string }[] {
  return (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }))
}
