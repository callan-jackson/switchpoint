/**
 * Hand-written DTO shapes mirroring docs/ARCHITECTURE.md (sections 2 and 5).
 *
 * Conventions (same as the API):
 * - identifiers are GUID strings, money is GBP as a number, rates are FRACTIONS (0.05 = 5%);
 * - percentages appear only in fields suffixed `Pct`;
 * - dates are ISO strings: `DateOnly` as 'yyyy-MM-dd', timestamps suffixed `Utc` as ISO 8601.
 *
 * When `npm run gen:api` becomes available this file should re-export from `./generated/schema`.
 */

// ---------------------------------------------------------------------------
// Shared envelopes
// ---------------------------------------------------------------------------

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

/** RFC 9457 problem details as emitted by ASP.NET Core. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  /** Validation failures keyed by field (ASP.NET `ValidationProblemDetails`). */
  errors?: Record<string, string[]>
}

export interface PageQuery {
  page?: number
  pageSize?: number
  search?: string
  sort?: string
}

// ---------------------------------------------------------------------------
// Auth and tenancy
// ---------------------------------------------------------------------------

export type UserRole = 'Adviser' | 'Paraplanner' | 'Compliance' | 'FirmAdmin' | 'PlatformAdmin'

export interface AuthUser {
  id: string
  displayName: string
  role: UserRole
  firmId: string
  firmName: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  expiresAtUtc: string
  user: AuthUser
}

// ---------------------------------------------------------------------------
// Clients and schemes
// ---------------------------------------------------------------------------

export type Sex = 'Male' | 'Female'
export type TaxRegime = 'RestOfUk' | 'Scotland'
export type HealthStatus = 'Standard' | 'Enhanced'
export type ExternalSource = 'Manual' | 'Intelliflo' | 'Xplan' | 'TruePotential' | 'Origo'

export interface ClientSummary {
  id: string
  title?: string
  firstName: string
  lastName: string
  dateOfBirth: string
  adviserName: string
  riskProfile: number
  schemeCount: number
  /** Sum of current values across the client's schemes. */
  totalPensionValue: number
  openAnalyses: number
  lastActivityUtc: string
  externalSource: ExternalSource
}

export interface StatePensionForecast {
  weeklyAmount: number
  qualifyingYears: number
}

export interface ClientDetail extends ClientSummary {
  sex: Sex
  /** Masked at rest; only the last three characters are ever returned. */
  nationalInsuranceNumberMasked: string
  email?: string
  phone?: string
  addressLine1?: string
  addressLine2?: string
  town?: string
  postcode?: string
  maritalStatus?: string
  employmentStatus?: string
  annualSalary?: number
  targetRetirementAge: number
  taxRegime: TaxRegime
  health: HealthStatus
  smoker: boolean
  statePension?: StatePensionForecast
  externalRef?: string
  schemes: Scheme[]
  createdAtUtc: string
  updatedAtUtc: string
}

export type SchemeType =
  | 'PersonalPension'
  | 'StakeholderPension'
  | 'Sipp'
  | 'OccupationalMoneyPurchase'
  | 'DefinedBenefit'
  | 'Section32'
  | 'RetirementAnnuityContract'
  | 'Isa'
  | 'GeneralInvestmentAccount'
  | 'OnshoreBond'
  | 'OffshoreBond'
  | 'DrawdownPlan'

export type ContributionPayer = 'Member' | 'Employer' | 'ThirdParty'
export type ContributionFrequency = 'Monthly' | 'Quarterly' | 'Annually' | 'Single'

export interface Contribution {
  payer: ContributionPayer
  amount: number
  frequency: ContributionFrequency
  escalationRate: number
  isGrossOfTaxRelief: boolean
}

export interface Holding {
  fundId?: string
  isin?: string
  name: string
  /** Fraction of the scheme; holdings sum to 1. */
  weight: number
}

export interface SchemeGuarantees {
  guaranteedAnnuityRate?: number
  guaranteedGrowthRate?: number
  protectedTaxFreeCashPct?: number
  protectedPensionAge?: number
  withProfits: boolean
  marketValueReductionPct?: number
  loyaltyBonusPct?: number
}

export interface Scheme {
  id: string
  clientId: string
  schemeType: SchemeType
  providerId?: string
  providerName?: string
  productName: string
  policyNumber?: string
  currentValue: number
  transferValue: number
  valuationDate: string
  contributions: Contribution[]
  charges: ChargeSchedule
  holdings: Holding[]
  guarantees: SchemeGuarantees
  exitPenalty?: ExitPenaltySchedule
  selectedRetirementAge: number
  /** True when any guarantee flag is set; such schemes are always 'Refer' in a switch. */
  hasSafeguardedBenefits: boolean
}

// ---------------------------------------------------------------------------
// Charge model (architecture 2.4)
// ---------------------------------------------------------------------------

export type TierMode = 'Marginal' | 'WholeOfFund'

export interface TierBand {
  /** Upper bound of the band in GBP; null = unbounded. */
  upTo: number | null
  annualRate: number
}

export interface TieredCharge {
  mode: TierMode
  bands: TierBand[]
}

export type Indexation = 'None' | 'Cpi' | 'Fixed'

export interface FixedCharge {
  amount: number
  frequency: ContributionFrequency
  indexation: Indexation
  indexationRate?: number
  appliesTo: 'Wrapper' | 'Drawdown' | 'Sipp'
}

export type FundChargeBasis =
  | { kind: 'Explicit'; ocf: number }
  | { kind: 'FromHoldings' }
  | { kind: 'None' }

export interface AdviserCharge {
  initialPct: number
  initialAmount: number
  ongoingPct: number
  ongoingAmount: number
}

export interface DealingCharges {
  fundDealAmount: number
  etfDealAmount: number
  expectedDealsPerYear: number
}

export interface ExitPenaltyStep {
  untilYearsFromStart: number
  pct: number
  amount: number
}

export type ExitPenaltySchedule = ExitPenaltyStep[]

export interface LargeFundDiscount {
  threshold: number
  rebatePct: number
}

export interface ChargeSchedule {
  platformCharge?: TieredCharge
  productCharge?: TieredCharge
  fixedCharges: FixedCharge[]
  fundCharge: FundChargeBasis
  transactionCosts?: number
  adviserCharges: AdviserCharge
  dealingCharges?: DealingCharges
  switchCharge?: number
  expectedSwitchesPerYear?: number
  exitPenalty?: ExitPenaltySchedule
  bidOfferSpreadPct?: number
  allocationRatePct?: number
  largeFundDiscounts?: LargeFundDiscount[]
}

// ---------------------------------------------------------------------------
// Market catalogue
// ---------------------------------------------------------------------------

export type ProviderKind = 'Platform' | 'Insurer' | 'SippOperator' | 'FundManager' | 'Mps'
export type DataQuality = 'Verified' | 'Indicative' | 'Placeholder'
export type WrapperType = 'Sipp' | 'PersonalPension' | 'Isa' | 'Gia' | 'Bond' | 'Drawdown'

export interface Provider {
  id: string
  name: string
  fcaFirmReferenceNumber?: string
  kind: ProviderKind
  website?: string
}

export interface Product {
  id: string
  providerId: string
  providerName: string
  name: string
  wrapperTypes: WrapperType[]
  minimumInvestment: number
  minimumRegularContribution: number
  chargeSchedule: ChargeSchedule
  allowsFamilyLinking: boolean
  availableFundUniverse: 'WholeOfMarket' | 'Restricted'
  effectiveFrom: string
  effectiveTo?: string
  version: number
  sourceUrl?: string
  asAt: string
  dataQuality: DataQuality
}

export type FundType =
  | 'Oeic'
  | 'UnitTrust'
  | 'Etf'
  | 'InvestmentTrust'
  | 'ModelPortfolio'
  | 'Cash'

export interface AssetAllocation {
  equity: number
  fixedInterest: number
  property: number
  cash: number
  alternatives: number
}

export interface Fund {
  id: string
  isin: string
  sedol?: string
  name: string
  shareClass?: string
  managerName: string
  fundType: FundType
  iaSector?: string
  morningstarCategory?: string
  ocf: number
  transactionCosts: number
  assetAllocation: AssetAllocation
  srri: number
  volatility3Y?: number
  sharpe3Y?: number
  return1Y?: number
  return3Y?: number
  return5Y?: number
  yield?: number
  maxDrawdown3Y?: number
  morningstarRating?: number
  medalistRating?: string
  priceDate?: string
  price?: number
  factsheetUrl?: string
  sourceUrl?: string
  asAt: string
}

// ---------------------------------------------------------------------------
// Assumptions
// ---------------------------------------------------------------------------

export type ProjectionBasis = 'Nominal' | 'Real'

export interface AssumptionSet {
  id: string
  firmId?: string
  name: string
  isFcaStandard: boolean
  version: number
  growthLower: number
  growthIntermediate: number
  growthHigher: number
  inflation: number
  earningsGrowth: number
  chargeInflation: number
  preRetirementDiscountRate: number
  annuityInterestRate: number
  annuityExpenseLoading: number
  mortalityBasis: string
  statePensionIncrease: number
  taxYear: string
  projectionBasis: ProjectionBasis
  asAt: string
}

// ---------------------------------------------------------------------------
// Pension switch analysis
// ---------------------------------------------------------------------------

export type AnalysisStatus = 'Draft' | 'Calculated' | 'Locked'

export interface AssumptionOverride {
  key: string
  value: number
  reason?: string
}

export interface PensionSwitchRequest {
  clientId: string
  cedingSchemeIds: string[]
  proposedProductId: string
  proposedProductVersion?: number
  proposedHoldings: Holding[]
  proposedAdviserCharges: AdviserCharge
  retirementAge: number
  assumptionSetId: string
  overrides?: AssumptionOverride[]
}

export type SwitchVerdict = 'Retain' | 'Consider' | 'SwitchCandidate' | 'Refer'

export interface RiyFigures {
  /** COBS 13 Annex 4 3.1R: product, platform and fund charges only. */
  productRiy: number
  /** COBS 13 Annex 4 3.2R: every charge including adviser charges. */
  totalRiy: number
  /** Growth rate used for the projection (the intermediate rate). */
  growthRate: number
}

export interface ProjectionYear {
  year: number
  age: number
  contributionsToDate: number
  valueBeforeCharges: number
  valueAfterProductCharges: number
  valueAfterAllCharges: number
  /** Real (today's money) value after all charges. */
  valueAfterAllChargesReal: number
  chargesToDate: number
}

export interface CriticalYieldFigures {
  /** Growth the receiving product must earn before charges to match the ceding scheme(s). */
  criticalYield: number
  /** Assumed growth minus critical yield; positive means the switch is expected to be ahead. */
  headroom: number
  projectedGain: number
  breakEvenYear?: number
  converged: boolean
  iterations: number
}

export interface CedingSchemeResult {
  schemeId: string
  schemeName: string
  transferValue: number
  exitPenalty: number
  projectedValueAtRetirement: number
  riy: RiyFigures
  verdict: SwitchVerdict
  guaranteeFlags: string[]
  projections: ProjectionYear[]
}

export interface PensionSwitchResult {
  analysisId?: string
  status: AnalysisStatus
  calculatedAtUtc: string
  assumptionSetName: string
  assumptionSetVersion: number
  engineVersion: string
  retirementAge: number
  /** Critical yield at the lower, intermediate and higher COBS 13 rates. */
  criticalYield: {
    lower: CriticalYieldFigures
    intermediate: CriticalYieldFigures
    higher: CriticalYieldFigures
  }
  existingValueAtRetirement: number
  proposedValueAtRetirement: number
  proposed: {
    productId: string
    productName: string
    startValue: number
    riy: RiyFigures
    projections: ProjectionYear[]
  }
  cedingSchemes: CedingSchemeResult[]
  warnings: string[]
}

// ---------------------------------------------------------------------------
// DB transfer analysis
// ---------------------------------------------------------------------------

export interface DbTransferRequest {
  clientId: string
  dbSchemeId: string
  proposedProductId: string
  proposedHoldings: Holding[]
  transferDate: string
  assumptionSetId: string
  adviserChargeBasis: 'Fixed' | 'Contingent'
}

export interface IncomeComparisonRow {
  age: number
  schemePensionReal: number
  drawdownIncomeReal: number
  residualFundReal: number
  schemeDeathBenefitReal: number
}

export interface DbTransferResult {
  analysisId?: string
  status: AnalysisStatus
  calculatedAtUtc: string
  cetv: number
  cetvGuaranteeExpiryDate?: string
  revaluedPensionAtNra: number
  normalRetirementAge: number
  /** Transfer Value Comparator: estimated cost of buying the scheme benefits from an insurer. */
  tvc: number
  tvcShortfall: number
  giltYieldUsed: number
  criticalYields: {
    typeA: CriticalYieldFigures
    typeB: CriticalYieldFigures
    drawdownHurdle: CriticalYieldFigures
  }
  pclsOption: {
    schemePcls: number
    residualPension: number
    commutationFactor: number
  }
  incomeComparison: IncomeComparisonRow[]
  stressTests: Array<{ name: string; firstShortfallAge?: number; fundAtNra: number }>
  adviceFeePaybackMonths: number
  warnings: string[]
}

// ---------------------------------------------------------------------------
// Cashflow
// ---------------------------------------------------------------------------

export interface CashflowRow {
  year: number
  taxYear: string
  age: number
  partnerAge?: number
  grossIncome: number
  incomeBySource: Record<string, number>
  incomeTax: number
  nationalInsurance: number
  netIncome: number
  expenses: number
  surplus: number
  shortfall: number
  assetValues: Record<string, number>
  totalAssets: number
  remainingLumpSumAllowance: number
  warnings: string[]
}

export type Percentile = 5 | 10 | 25 | 50 | 75 | 90 | 95

export interface PercentileRow {
  year: number
  age: number
  totalAssets: Record<Percentile, number>
  netIncome: Record<Percentile, number>
}

export interface CashflowResult {
  planId?: string
  calculatedAtUtc: string
  planEndAge: number
  rows: CashflowRow[]
  firstShortfallAge?: number
  totalTaxPaid: number
  sustainableRealIncome: number
  legacyAtPlanEnd: number
  stochastic?: {
    seed: number
    paths: number
    successProbability: number
    percentiles: PercentileRow[]
    worstDecileShortfallAge?: number
    conservativenessCheckPassed: boolean
  }
}

// ---------------------------------------------------------------------------
// Reports and audit
// ---------------------------------------------------------------------------

export type ReportKind = 'Suitability' | 'PensionSwitch' | 'DbTransfer' | 'Cashflow' | 'FundComparison'
export type ReportFormat = 'Pdf' | 'Docx' | 'Json'

export interface Report {
  id: string
  analysisId: string
  clientId: string
  clientName: string
  kind: ReportKind
  format: ReportFormat
  generatedAtUtc: string
  generatedBy: string
  sha256: string
  templateVersion: string
  sizeBytes: number
}

export interface AuditEvent {
  id: string
  firmId: string
  userId: string
  userDisplayName: string
  occurredAtUtc: string
  entityType: string
  entityId: string
  action: string
  payload: Record<string, unknown>
  previousHash: string
  hash: string
}

export interface AuditVerification {
  verified: boolean
  eventsChecked: number
  firstBrokenEventId?: string
  checkedAtUtc: string
}
