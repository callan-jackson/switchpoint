/**
 * Hand-written DTO shapes mirroring docs/api/CONTRACT.md (v1). Names and fields match the
 * contract one-to-one so that page code reads the same as the API documentation.
 *
 * Conventions:
 * - identifiers are GUID strings; money is a GBP number;
 * - a field whose name ends in `Pct` is a PERCENTAGE (0.25 means 0.25%). The API converts to
 *   fractions at its boundary, the UI never does arithmetic on them beyond display;
 * - dates are ISO 'yyyy-MM-dd'; timestamps suffixed `Utc` are ISO 8601 UTC strings.
 *
 * When `npm run gen:api` is wired to docs/api/openapi.json this file should re-export from
 * `./generated/schema` instead; keep the exported names stable so pages do not change.
 */

// ---------------------------------------------------------------------------
// Shared envelopes
// ---------------------------------------------------------------------------

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

/** RFC 9457 problem details as emitted by ASP.NET Core. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  /** Validation failures keyed by field name (ASP.NET `ValidationProblemDetails`). */
  errors?: Record<string, string[]>
}

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

export type UserRole = 'adviser' | 'paraplanner' | 'compliance' | 'firmAdmin' | 'platformAdmin'

export interface UserDto {
  id: string
  displayName: string
  email: string
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
  user: UserDto
}

// ---------------------------------------------------------------------------
// Clients
// ---------------------------------------------------------------------------

export type Sex = 'male' | 'female'
export type TaxRegime = 'restOfUk' | 'scotland'
export type HealthStatus = 'standard' | 'enhanced'
export type MaritalStatus =
  | 'single'
  | 'married'
  | 'civilPartnership'
  | 'divorced'
  | 'widowed'
  | 'cohabiting'
export type EmploymentStatus = 'employed' | 'selfEmployed' | 'retired' | 'notWorking' | 'director'
export type ExternalSource = 'manual' | 'intelliflo' | 'xplan' | 'truePotential' | 'origo'

export interface AddressDto {
  line1?: string
  line2?: string
  town?: string
  county?: string
  postcode?: string
  country?: string
}

export interface StatePensionDto {
  forecastWeeklyAmount?: number
  qualifyingYears?: number
}

export interface ExternalReferenceDto {
  source: ExternalSource
  externalId?: string
}

export interface ClientSummary {
  id: string
  fullName: string
  dateOfBirth: string
  age: number
  email?: string
  riskProfile: number
  schemeCount: number
  totalPensionValue: number
  updatedAtUtc: string
}

export interface ClientWrite {
  title?: string
  firstName: string
  lastName: string
  dateOfBirth: string
  sex: Sex
  email?: string
  phone?: string
  address?: AddressDto
  maritalStatus: MaritalStatus
  employmentStatus: EmploymentStatus
  annualSalary: number
  targetRetirementAge: number
  taxRegime: TaxRegime
  /** 1 (lowest) to 7 (highest). */
  riskProfile: number
  health: HealthStatus
  isSmoker: boolean
  statePension: StatePensionDto
  /** Write-only; stored masked. */
  nationalInsuranceNumber?: string
}

export interface ClientDetail extends Omit<ClientWrite, 'nationalInsuranceNumber'> {
  id: string
  fullName: string
  age: number
  nationalInsuranceNumberMasked?: string
  externalReference: ExternalReferenceDto
  schemes: SchemeDto[]
  createdAtUtc: string
  updatedAtUtc: string
}

// ---------------------------------------------------------------------------
// Schemes
// ---------------------------------------------------------------------------

export type SchemeType =
  | 'personalPension'
  | 'stakeholderPension'
  | 'sipp'
  | 'occupationalMoneyPurchase'
  | 'definedBenefit'
  | 'section32'
  | 'retirementAnnuityContract'
  | 'isa'
  | 'generalInvestmentAccount'
  | 'onshoreBond'
  | 'offshoreBond'
  | 'drawdownPlan'

export type ContributionPayer = 'member' | 'employer' | 'thirdParty'
export type Frequency = 'single' | 'annually' | 'quarterly' | 'monthly'

export interface ContributionDto {
  payer: ContributionPayer
  amount: number
  frequency: Frequency
  escalationPct: number
  isGrossOfTaxRelief: boolean
  startMonth?: number
  endMonth?: number
}

export interface HoldingDto {
  name: string
  weightPct: number
  isin?: string
  fundId?: string
  ocfPct?: number
}

export interface GuaranteesDto {
  guaranteedAnnuityRatePct?: number
  guaranteedGrowthRatePct?: number
  protectedTaxFreeCashPct?: number
  protectedPensionAge?: number
  withProfits: boolean
  marketValueReductionPct: number
  terminalBonus: number
  loyaltyBonusPct: number
}

export type IndexBasis = 'none' | 'fixed' | 'cpi' | 'rpi' | 'lpiCpi' | 'lpiRpi' | 'section148'

export interface IndexRuleDto {
  basis: IndexBasis
  ratePct: number
  capPct?: number
  floorPct?: number
}

export interface DbTrancheDto {
  name: string
  accruedAnnualPension: number
  revaluation: IndexRuleDto
  escalation: IndexRuleDto
  isGmp: boolean
}

export type SchemeFundingStatus = 'fullyFunded' | 'deficit' | 'pensionProtectionFund'

export interface DefinedBenefitDto {
  dateOfLeaving: string
  normalRetirementAge: number
  cetvGuaranteeExpiry: string
  tranches: DbTrancheDto[]
  spousePensionPct: number
  guaranteePeriodYears: number
  /** £ lump sum per £1 of pension given up. */
  pclsCommutationFactor: number
  maxPclsPct: number
  earlyRetirementReductionPct: number
  earliestUnreducedAge?: number
  bridgingPensionAnnual: number
  fundingStatus: SchemeFundingStatus
}

export interface SchemeWrite {
  type: SchemeType
  providerId?: string
  productName: string
  policyNumber?: string
  currentValue: number
  transferValue: number
  valuationDate: string
  startDate?: string
  charges: ChargeScheduleDto
  guarantees: GuaranteesDto
  selectedRetirementAge?: number
  inDrawdown: boolean
  contributions: ContributionDto[]
  holdings: HoldingDto[]
  definedBenefit?: DefinedBenefitDto
}

export interface SchemeDto extends SchemeWrite {
  id: string
  clientId: string
  providerName?: string
  netTransferValue: number
  weightedOcfPct?: number
  createdAtUtc: string
  updatedAtUtc: string
}

/** A scheme supplied inline to a stateless calculation (no persisted id). */
export type InlineSchemeWrite = SchemeWrite & { name: string }

// ---------------------------------------------------------------------------
// Charge model (shared by schemes and products)
// ---------------------------------------------------------------------------

export type TieredChargeMode = 'marginal' | 'wholeOfFund'

export interface TierBandDto {
  /** Upper bound of the band in GBP; omitted = unbounded. */
  upTo?: number
  annualRatePct: number
}

export interface TieredChargeDto {
  mode: TieredChargeMode
  bands: TierBandDto[]
}

export type IndexationBasis = 'none' | 'cpi' | 'fixed'

export interface IndexationDto {
  basis: IndexationBasis
  ratePct: number
}

export type FixedChargeScope = 'wrapper' | 'drawdown' | 'sipp'

export interface FixedChargeDto {
  amount: number
  frequency: Frequency
  indexation: IndexationDto
  appliesTo: FixedChargeScope
  description?: string
}

export type FundChargeKind = 'none' | 'explicit' | 'fromHoldings'

export interface FundChargeDto {
  kind: FundChargeKind
  ocfPct?: number
}

export interface AdviserChargesDto {
  initialPct: number
  initialAmount: number
  ongoingPct: number
  ongoingAmount: number
}

export interface DealingChargesDto {
  fundDealAmount: number
  etfDealAmount: number
  expectedFundDealsPerYear: number
  expectedEtfDealsPerYear: number
}

export interface SwitchChargeDto {
  amountPerSwitch: number
  expectedSwitchesPerYear: number
}

export interface ExitPenaltyBandDto {
  untilYearsFromStart?: number
  ratePct: number
  amount: number
}

export interface ExitPenaltyDto {
  bands: ExitPenaltyBandDto[]
}

export interface LargeFundDiscountDto {
  threshold: number
  rebateRatePct: number
}

export interface ChargeScheduleDto {
  platformCharge?: TieredChargeDto
  productCharge?: TieredChargeDto
  fixedCharges: FixedChargeDto[]
  fundCharge: FundChargeDto
  transactionCostsPct: number
  adviserCharges: AdviserChargesDto
  dealingCharges: DealingChargesDto
  switchCharge: SwitchChargeDto
  exitPenalty: ExitPenaltyDto
  bidOfferSpreadPct: number
  allocationRatePct: number
  largeFundDiscounts: LargeFundDiscountDto[]
}

// ---------------------------------------------------------------------------
// Market catalogue
// ---------------------------------------------------------------------------

export type ProviderKind = 'platform' | 'insurer' | 'sippOperator' | 'fundManager' | 'mps'
export type DataQuality = 'verified' | 'indicative' | 'placeholder'
export type FundUniverse = 'wholeOfMarket' | 'restricted'
/**
 * `ProductSummary.wrapperTypes` is a plain `string[]` on the wire, so — unlike every other
 * enum — it arrives PascalCase ('Sipp', 'GeneralInvestmentAccount'). Verified against the API.
 */
export type WrapperType =
  | 'Sipp'
  | 'PersonalPension'
  | 'Isa'
  | 'GeneralInvestmentAccount'
  | 'Drawdown'
  | 'Bond'

export interface ProviderDto {
  id: string
  name: string
  kind: ProviderKind
  fcaFirmReferenceNumber?: string
  website?: string
}

export interface ProductSummary {
  id: string
  providerId: string
  providerName: string
  name: string
  wrapperTypes: string[]
  minimumInvestment: number
  allowsFamilyLinking: boolean
  fundUniverse: FundUniverse
  dataQuality: DataQuality
  effectiveChargePctAt100k: number
  effectiveChargePctAt500k: number
  currentChargeVersion: number
  asAt: string
  sourceUrl?: string
}

export interface ProductChargeVersionDto {
  version: number
  effectiveFrom: string
  effectiveTo?: string
  asAt: string
  sourceUrl?: string
  dataQuality: DataQuality
  charges: ChargeScheduleDto
}

export interface ProductDetail extends ProductSummary {
  chargeVersions: ProductChargeVersionDto[]
}

export type FundType = 'oeic' | 'unitTrust' | 'etf' | 'investmentTrust' | 'modelPortfolio' | 'cash'

export interface AssetAllocationDto {
  equityPct: number
  fixedInterestPct: number
  propertyPct: number
  cashPct: number
  alternativesPct: number
}

export interface FundStatisticsDto {
  return1YPct?: number
  return3YPct?: number
  return5YPct?: number
  volatility3YPct?: number
  sharpe3Y?: number
  maxDrawdown3YPct?: number
  yieldPct?: number
  morningstarRating?: number
  medalistRating?: string
}

export interface FundDto {
  id: string
  isin: string
  sedol?: string
  name: string
  shareClass?: string
  managerName: string
  type: FundType
  iaSector?: string
  morningstarCategory?: string
  ocfPct: number
  transactionCostsPct: number
  assetAllocation: AssetAllocationDto
  srri?: number
  statistics: FundStatisticsDto
  price?: number
  priceDate?: string
  factsheetUrl?: string
  sourceUrl?: string
  asAt?: string
}

export interface ModelPortfolioHoldingDto {
  fundId: string
  isin: string
  name: string
  weightPct: number
  ocfPct: number
}

export interface ModelPortfolioDto {
  id: string
  providerId: string
  providerName: string
  name: string
  riskLevel: number
  mpsFeePct: number
  blendedOcfPct: number
  totalInvestmentChargePct: number
  holdings: ModelPortfolioHoldingDto[]
}

// ---------------------------------------------------------------------------
// Assumption sets
// ---------------------------------------------------------------------------

export type ProjectionBasis = 'nominal' | 'real'

export interface MarketInputsDto {
  giltYieldUpTo5Pct: number
  giltYield5To10Pct: number
  giltYield10To15Pct: number
  giltYieldOver15Pct: number
  tvcAnnuityRateRpiLinkedPct: number
  tvcAnnuityRateLevelPct: number
  cobs13YPct: number
  asAt: string
}

export interface AssumptionSetDto {
  id: string
  firmId?: string
  name: string
  isFcaStandard: boolean
  version: number
  growthLowerPct: number
  growthIntermediatePct: number
  growthHigherPct: number
  inflationPct: number
  earningsGrowthPct: number
  rpiInflationPct: number
  chargeInflationPct: number
  preRetirementProductChargePct: number
  annuityExpenseLoadingPct: number
  spouseAgeGapYears: number
  mortalityBasis: string
  statePensionIncreasePct: number
  taxYear: string
  projectionBasis: ProjectionBasis
  marketInputs: MarketInputsDto
}

export type AssumptionSetWrite = Omit<AssumptionSetDto, 'id' | 'firmId' | 'isFcaStandard' | 'version'>

export interface AssumptionSetCopyRequest {
  name: string
}

// ---------------------------------------------------------------------------
// Stateless calculations
// ---------------------------------------------------------------------------

export type CedingSchemeRef = { schemeId: string } | { inline: InlineSchemeWrite }

/**
 * One overrides shape is shared by every calculation and analysis (`AssumptionOverrides` in the
 * generated schema); each engine reads only the fields it needs.
 */
export interface AssumptionOverrides {
  growthLowerPct?: number
  growthIntermediatePct?: number
  growthHigherPct?: number
  inflationPct?: number
  earningsGrowthPct?: number
  statePensionIncreasePct?: number
}

export type PensionSwitchOverrides = AssumptionOverrides

export interface PensionSwitchCalcRequest {
  /** When given, the server loads schemes by id; otherwise inline schemes are used. */
  clientId?: string
  cedingSchemes: CedingSchemeRef[]
  proposedProductId: string
  proposedProductChargeVersion?: number
  proposedHoldings: HoldingDto[]
  proposedModelPortfolioId?: string
  proposedAdviserCharges: AdviserChargesDto
  retirementAge: number
  assumptionSetId?: string
  overrides?: AssumptionOverrides
  /** Default true: ceding contributions continue into the new product. */
  redirectContributions: boolean
  /** Client age used when no `clientId` is given (inline schemes); the API defaults to 55. */
  clientAge?: number
}

export interface CriticalYieldAtRateDto {
  growthPct: number
  existingValueAtRetirement: number
  receivingValueAtRetirement: number
  criticalYieldPct: number
  criticalYieldRealPct: number
  headroomPct: number
  projectedGain: number
  breakEvenYear?: number
  converged: boolean
}

export interface EffectOfChargesRowDto {
  year: number
  paymentsToDate: number
  beforeCharges: number
  planAndInvestmentChargesOnly: number
  afterAllCharges: number
  effectOfDeductionsToDate: number
}

export interface TotalChargesDto {
  platform: number
  product: number
  fund: number
  transaction: number
  adviserInitial: number
  adviserOngoing: number
  fixed: number
  dealing: number
  switch: number
  discounts: number
  allocationAndSpread: number
  exitPenalty: number
  total: number
}

export interface RiyDto {
  growthPct: number
  productRiyPct: number
  totalRiyPct: number
  rateAfterProductChargesPct: number
  rateAfterAllChargesPct: number
  valueBeforeCharges: number
  valueAfterProductCharges: number
  valueAfterAllCharges: number
  productSentence: string
  totalSentence: string
  effectOfCharges: EffectOfChargesRowDto[]
  totalCharges: TotalChargesDto
}

export type SwitchVerdict = 'switchCandidate' | 'consider' | 'retain' | 'refer'

export interface PensionSwitchSchemeResultDto {
  name: string
  currentValue: number
  netTransferValue: number
  projectedValueIfRetained: number
  riyIfRetained: RiyDto
  riyIfSwitched: RiyDto
  criticalYieldAlonePct: number
  guaranteesFlagged: boolean
  verdict: SwitchVerdict
}

export interface PensionSwitchChartPointDto {
  year: number
  existingValue: number
  receivingValue: number
}

export interface AssumptionSetRefDto {
  id: string
  name: string
  version: number
}

export interface PensionSwitchResultDto {
  totalNetTransferValue: number
  initialAdviserCharge: number
  anyGuaranteesFlagged: boolean
  warnings: string[]
  lower: CriticalYieldAtRateDto
  intermediate: CriticalYieldAtRateDto
  higher: CriticalYieldAtRateDto
  receivingRiy: RiyDto
  schemes: PensionSwitchSchemeResultDto[]
  /** Intermediate rate, yearly, real terms. */
  chart: PensionSwitchChartPointDto[]
  engineVersion: string
  calculatedAtUtc: string
  assumptionSet: AssumptionSetRefDto
}

export interface RiyCalcRequest {
  startValue: number
  months: number
  growthPct: number
  charges: ChargeScheduleDto
  weightedOcfPct?: number
  contributions: ContributionDto[]
}

export interface DbTransferCalcRequest {
  clientId?: string
  dbSchemeId?: string
  inline?: InlineSchemeWrite
  /** Used with an inline scheme when no `clientId` is supplied. */
  clientSex?: Sex
  clientDateOfBirth?: string
  proposedProductId: string
  proposedHoldings: HoldingDto[]
  proposedAdviserCharges: AdviserChargesDto
  aptaGrowthPct: number
  planEndAge: number
  initialAdviceFee: number
  workplaceDefaultChargePct?: number
  assumptionSetId?: string
  overrides?: AssumptionOverrides
  transferDate?: string
}

export interface TvcTrancheDto {
  name: string
  accruedAnnualPension: number
  revaluationRatePct: number
  yearsRevalued: number
  pensionAtRetirement: number
  escalationInPaymentPct: number
  annuityInterestRatePct: number
  annuityPricePerPound: number
  annuityCost: number
  isGmp: boolean
}

export interface TvcDto {
  cashEquivalentTransferValue: number
  estimatedReplacementCost: number
  difference: number
  retirementAgeUsed: number
  termYears: number
  giltYieldUsedPct: number
  discountRateUsedPct: number
  annuityCostAtRetirement: number
  pensionAtRetirement: number
  wording: string
  notes: string[]
  tranches: TvcTrancheDto[]
}

export interface DbCriticalYieldsDto {
  typeAAnnuityMatchPct: number
  typeBPclsAndReducedPensionPct: number
  drawdownHurdleRatePct: number
  schemePcls: number
  residualPensionAfterPcls: number
  converged: boolean
}

export interface IncomeComparisonRowDto {
  age: number
  schemeIncomeNominal: number
  schemeIncomeReal: number
  drawdownIncomeReal: number
  residualFundReal: number
  schemeDeathBenefitReal: number
}

export interface StressTestDto {
  name: string
  sustainableRealIncome: number
  change: number
}

export interface DbTransferSummaryDto {
  initialAdviceFee: number
  revaluedMonthlyIncome: number
  paybackMonths: number
  firstYearChargesProposed: number
  ongoingAnnualChargesProposed: number
  firstYearChargesCeding: number
  firstYearChargesWorkplaceDefault?: number
}

export interface DbTransferResultDto {
  tvc: TvcDto
  criticalYields: DbCriticalYieldsDto
  sustainableRealIncomeFromTransfer: number
  incomeComparison: IncomeComparisonRowDto[]
  stressTests: StressTestDto[]
  summary: DbTransferSummaryDto
  lifeExpectancyAtRetirement: number
  warnings: string[]
  engineVersion: string
  calculatedAtUtc: string
  assumptionSet: AssumptionSetRefDto
}

export interface PersonDto {
  name: string
  dateOfBirth: string
  sex: Sex
  taxRegime: TaxRegime
  retirementAge: number
  statePensionForecastWeekly?: number
  statePensionQualifyingYears?: number
  mpaaTriggered: boolean
}

export type CashflowIncomeKind =
  | 'employment'
  | 'selfEmployment'
  | 'rental'
  | 'definedBenefitPension'
  | 'annuity'
  | 'statePension'
  | 'other'

export interface CashflowIncomeDto {
  name: string
  kind: CashflowIncomeKind
  annualAmount: number
  fromAge: number
  toAge?: number
  growthPct: number
  isTaxable: boolean
  personIndex: number
}

export interface CashflowExpenseDto {
  name: string
  annualAmount: number
  fromAge: number
  toAge?: number
}

export type CashflowAssetKind =
  | 'uncrystallisedPension'
  | 'drawdown'
  | 'isa'
  | 'generalInvestmentAccount'
  | 'cash'
  | 'property'
  | 'onshoreBond'

export interface CashflowAssetDto {
  name: string
  kind: CashflowAssetKind
  value: number
  growthPct: number
  charges: ChargeScheduleDto
  schemeId?: string
  costBasis: number
  annualContribution: number
  employerContribution: number
  salarySacrifice: boolean
  personIndex: number
  allocation?: AssetAllocationDto
}

export interface CashflowEventDto {
  name: string
  atAge: number
  amount: number
}

export type CrystallisationStrategy = 'pclsUpFront' | 'phasedUfpls' | 'phasedDrawdown'
export type DrawdownRule = 'gapFill' | 'fixedAmount' | 'percentOfPot'

export interface CashflowStrategyDto {
  withdrawalOrder: string[]
  crystallisation: CrystallisationStrategy
  drawdownRule: DrawdownRule
  drawdownParameter: number
  reinvestSurplusIntoIsa: boolean
  annuityPurchaseAge?: number
}

export type CashflowOverrides = AssumptionOverrides

export interface CashflowCalcRequest {
  clientId?: string
  person: PersonDto
  partner?: PersonDto
  planEndAge: number
  incomes: CashflowIncomeDto[]
  expenses: CashflowExpenseDto[]
  assets: CashflowAssetDto[]
  events: CashflowEventDto[]
  strategy: CashflowStrategyDto
  assumptionSetId?: string
  overrides?: CashflowOverrides
}

export interface StochasticCalcRequest extends CashflowCalcRequest {
  /** `ulong` on the wire: a number, even though the response echoes it as a string. */
  seed?: number
  paths?: number
}

export interface CashflowAssetRowDto {
  name: string
  kind: CashflowAssetKind
  value: number
  valueReal: number
}

export interface CashflowRowDto {
  year: number
  age: number
  partnerAge?: number
  employmentIncome: number
  statePensionIncome: number
  dbPensionIncome: number
  otherIncome: number
  pensionWithdrawalsTaxable: number
  taxFreeCash: number
  isaWithdrawals: number
  giaWithdrawals: number
  cashWithdrawals: number
  incomeTax: number
  nationalInsurance: number
  capitalGainsTax: number
  netIncome: number
  netIncomeReal: number
  expenses: number
  surplus: number
  shortfall: number
  contributions: number
  assets: CashflowAssetRowDto[]
  totalAssets: number
  totalAssetsReal: number
  warnings: string[]
}

export interface CashflowResultDto {
  rows: CashflowRowDto[]
  firstShortfallAge?: number
  totalIncomeTax: number
  totalShortfall: number
  legacyAtEnd: number
  legacyAtEndReal: number
  lumpSumAllowanceUsed: number
  succeeds: boolean
  sustainableSpend: number
  engineVersion: string
  calculatedAtUtc: string
  assumptionSet: AssumptionSetRefDto
}

export interface PercentileRowDto {
  year: number
  age: number
  p5: number
  p10: number
  p25: number
  p50: number
  p75: number
  p90: number
  p95: number
}

export interface ConservativenessDto {
  deterministicAssetsAtEnd: number
  medianAssetsAtEnd: number
  medianIsNoLessConservative: boolean
}

export interface StochasticResultDto {
  seed: string
  paths: number
  probabilityOfSuccess: number
  totalAssetsReal: PercentileRowDto[]
  netIncomeReal: PercentileRowDto[]
  medianShortfallAge?: number
  worstDecileShortfallAge?: number
  conservativeness: ConservativenessDto
  meanLegacyReal: number
  engineVersion: string
  calculatedAtUtc: string
}

export interface TaxCalcRequest {
  taxYear?: string
  regime: TaxRegime
  earnedIncome: number
  pensionIncome: number
  otherIncome: number
  savingsInterest: number
  dividends: number
  grossPensionContributions: number
  reliefAtSourceContributions: number
  subjectToNi: boolean
}

export interface TaxLineDto {
  category: string
  band: string
  amount: number
  ratePct: number
  tax: number
}

export interface TaxComputationDto {
  taxYear: string
  regime: TaxRegime
  adjustedNetIncome: number
  personalAllowance: number
  taxableIncome: number
  incomeTax: number
  nationalInsurance: number
  totalDeductions: number
  marginalRatePct: number
  lines: TaxLineDto[]
}

// ---------------------------------------------------------------------------
// Persisted analyses
// ---------------------------------------------------------------------------

export type AnalysisKind = 'pensionSwitch' | 'dbTransfer' | 'cashflow'
export type AnalysisStatus = 'draft' | 'calculated' | 'locked'

export interface AnalysisSummary {
  id: string
  /** Owning client; present on the wire although CONTRACT.md omits it. */
  clientId: string
  kind: AnalysisKind
  title: string
  status: AnalysisStatus
  version: number
  calculatedAtUtc?: string
  updatedAtUtc: string
  createdBy: string
}

/** Audit and versioning fields shared by every persisted analysis. */
export interface AnalysisEnvelope {
  id: string
  firmId: string
  status: AnalysisStatus
  version: number
  resultHash?: string
  calculatedAtUtc?: string
  engineVersion?: string
  createdAtUtc: string
  updatedAtUtc: string
  createdBy: string
}

export interface PensionSwitchAnalysisWrite {
  clientId: string
  title: string
  retirementAge: number
  cedingSchemeIds: string[]
  proposedProductId?: string
  proposedProductChargeVersion?: number
  proposedHoldings: HoldingDto[]
  proposedModelPortfolioId?: string
  proposedAdviserCharges: AdviserChargesDto
  assumptionSetId: string
  overrides?: AssumptionOverrides
  rationale?: string
}

export interface PensionSwitchAnalysisDto extends PensionSwitchAnalysisWrite, AnalysisEnvelope {
  result?: PensionSwitchResultDto
}

export type ChargeBasis = 'nonContingent' | 'contingent'

export interface DbTransferAnalysisWrite {
  clientId: string
  dbSchemeId: string
  transferDate: string
  planEndAge: number
  proposedProductId?: string
  proposedProductChargeVersion?: number
  proposedHoldings: HoldingDto[]
  proposedAdviserCharges: AdviserChargesDto
  aptaGrowthPct?: number
  chargeBasis: ChargeBasis
  contingentChargingCarveOut?: string
  workplaceSchemeProductId?: string
  /** Present in the generated schema although CONTRACT.md omits it. */
  initialAdviceFee: number
  workplaceDefaultChargePct?: number
  assumptionSetId: string
  overrides?: AssumptionOverrides
}

export interface DbTransferAnalysisDto extends DbTransferAnalysisWrite, AnalysisEnvelope {
  result?: DbTransferResultDto
}

export interface CashflowPlanWrite {
  clientId: string
  partnerClientId?: string
  title: string
  planEndAge: number
  incomes: CashflowIncomeDto[]
  expenses: CashflowExpenseDto[]
  assets: CashflowAssetDto[]
  events: CashflowEventDto[]
  strategy: CashflowStrategyDto
  stochasticSeed?: number
  stochasticPaths: number
  assumptionSetId: string
  overrides?: CashflowOverrides
}

export interface CashflowPlanDto extends CashflowPlanWrite, AnalysisEnvelope {
  result?: CashflowResultDto
  stochasticResult?: StochasticResultDto
}

// ---------------------------------------------------------------------------
// Reports
// ---------------------------------------------------------------------------

export type ReportKind =
  | 'suitability'
  | 'pensionSwitch'
  | 'dbTransfer'
  | 'cashflow'
  | 'fundComparison'
export type ReportFormat = 'pdf' | 'docx' | 'json'

export interface ReportCreateRequest {
  analysisId: string
  kind: ReportKind
  format: ReportFormat
}

export interface ReportDto {
  id: string
  clientId: string
  analysisId: string
  analysisVersion: number
  analysisResultHash: string
  kind: ReportKind
  format: ReportFormat
  templateVersion: string
  generatedBy: string
  sha256: string
  sizeBytes: number
  generatedAtUtc: string
  downloadUrl: string
}

// ---------------------------------------------------------------------------
// Audit, integrations, health
// ---------------------------------------------------------------------------

export interface AuditEventDto {
  id: string
  sequence: number
  userId?: string
  userName?: string
  occurredAtUtc: string
  entityType: string
  entityId?: string
  action: string
  /** JSON document as a string (not an object) — verified against the API. */
  payload: string
  previousHash: string
  hash: string
}

export interface AuditVerifyResult {
  isValid: boolean
  firstBrokenIndex: number
  reason?: string
  eventsChecked: number
}

export type IntegrationConnector = 'Intelliflo' | 'Xplan' | 'TruePotential' | 'OrigoHub' | 'Morningstar'
export type IntegrationMode = 'Live' | 'Sandbox' | 'Disabled'

export interface IntegrationDto {
  connector: IntegrationConnector
  configured: boolean
  mode: IntegrationMode
  lastSyncUtc?: string
}

export interface IntegrationImportRequest {
  externalClientId?: string
}

export interface IntegrationImportResult {
  imported: number
  updated: number
  skipped: number
  messages: string[]
}

export interface MorningstarSyncResult {
  fundsUpdated: number
  messages: string[]
}

/**
 * `GET /healthz` sits at the site root (outside `/api/v1`), is anonymous and answers
 * `text/plain` with a single word — verified against the API.
 */
export type HealthStatusText = 'Healthy' | 'Degraded' | 'Unhealthy'

// ---------------------------------------------------------------------------
// Dashboard
// ---------------------------------------------------------------------------

/** `GET /dashboard/summary`: headline counts for the signed-in firm plus recent activity. */
export interface DashboardSummary {
  clients: number
  analysesInProgress: number
  reportsThisMonth: number
  fundsInCatalogue: number
  recentAnalyses: AnalysisSummary[]
}
