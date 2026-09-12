# SwitchPoint API contract (v1)

This is the agreed shape between the Application layer, the API host and the React client.
Base path `/api/v1`. JSON, camelCase. Dates are ISO `yyyy-MM-dd`; timestamps are ISO UTC and always
carry the `Z` suffix. Money is a decimal number in GBP. **Rates in DTOs are percentages when the field
name ends in `Pct`** (0.25 means 0.25%, and a 100% holding weight is `100`, not `1`); the API converts
to fractions (0.0025m) at the boundary.  Ids are GUIDs.

**Enums are camelCase on the wire** — `"draft"`, `"pdf"`, `"pensionSwitch"`, `"adviser"`. Reads are
case-insensitive, so `"Pdf"` is accepted on input, but every response uses the camelCase form.

Errors are RFC 9457 problem details (`application/problem+json`). FluentValidation failures return
**400** with an `errors` map of field → messages, and those keys are **PascalCase** (`"FirstName"`)
even though the rest of the body is camelCase. **422** is reserved for domain and numerical failures
(`DomainException`, `RootNotBracketedException`, `RootNotConvergedException`).

Authentication: `Authorization: Bearer <JWT>`; claims `sub`, `email`, `name`, `role`, `firm_id`,
`firm_name`. Roles: Adviser, Paraplanner, Compliance, FirmAdmin, PlatformAdmin. All resources are
scoped to the caller's firm; cross-firm access returns 404.

## Auth
| Method | Path | Body → Response |
|---|---|---|
| POST | /auth/login | `{email, password}` → `LoginResponse {accessToken, expiresAtUtc, user: UserDto}` |
| GET | /auth/me | → `UserDto {id, displayName, email, role, firmId, firmName}` |

Seeded demo users (development and demo deployments): `adviser@demo.switchpoint.local`,
`paraplanner@demo.switchpoint.local`, `compliance@demo.switchpoint.local`, all with password
`Demo!Pass123`, firm "Demo Financial Planning Ltd" (FRN 000000).

## Clients and schemes
| Method | Path | Body → Response |
|---|---|---|
| GET | /clients?search=&page=1&pageSize=25 | → `PagedResult<ClientSummary>` |
| POST | /clients | `ClientWrite` → `ClientDetail` (201) |
| GET | /clients/{id} | → `ClientDetail` |
| PUT | /clients/{id} | `ClientWrite` → `ClientDetail` |
| DELETE | /clients/{id} | → 204 |
| GET | /clients/{id}/schemes | → `SchemeDto[]` |
| POST | /clients/{id}/schemes | `SchemeWrite` → `SchemeDto` (201) |
| PUT | /clients/{id}/schemes/{schemeId} | `SchemeWrite` → `SchemeDto` |
| DELETE | /clients/{id}/schemes/{schemeId} | → 204 |
| GET | /clients/{id}/analyses | → `AnalysisSummary[]` (all kinds) |
| GET | /clients/{id}/reports | → `ReportDto[]` |

```ts
PagedResult<T> { items: T[]; page: number; pageSize: number; total: number }
ClientSummary { id; fullName; dateOfBirth; age; email?; riskProfile; schemeCount; totalPensionValue; updatedAtUtc }
ClientWrite {
  title?; firstName; lastName; dateOfBirth; sex: 'Male'|'Female'; email?; phone?;
  address?: { line1?; line2?; town?; county?; postcode?; country? };
  maritalStatus: 'Single'|'Married'|'CivilPartnership'|'Divorced'|'Widowed'|'Cohabiting';
  employmentStatus: 'Employed'|'SelfEmployed'|'Retired'|'NotWorking'|'Director';
  annualSalary; targetRetirementAge; taxRegime: 'RestOfUk'|'Scotland'; riskProfile (1-7);
  health: 'Standard'|'Enhanced'; isSmoker;
  statePension: { forecastWeeklyAmount?; qualifyingYears? };
  nationalInsuranceNumber?  // write-only; stored masked
}
ClientDetail extends ClientWrite (minus nationalInsuranceNumber) {
  id; fullName; age; nationalInsuranceNumberMasked?; externalReference: { source; externalId? };
  schemes: SchemeDto[]; createdAtUtc; updatedAtUtc
}
SchemeWrite {
  type: 'PersonalPension'|'StakeholderPension'|'Sipp'|'OccupationalMoneyPurchase'|'DefinedBenefit'|'Section32'|
        'RetirementAnnuityContract'|'Isa'|'GeneralInvestmentAccount'|'OnshoreBond'|'OffshoreBond'|'DrawdownPlan';
  providerId?; productName; policyNumber?; currentValue; transferValue; valuationDate; startDate?;
  charges: ChargeScheduleDto; guarantees: GuaranteesDto; selectedRetirementAge?; inDrawdown;
  contributions: ContributionDto[]; holdings: HoldingDto[]; definedBenefit?: DefinedBenefitDto
}
SchemeDto extends SchemeWrite { id; clientId; providerName?; netTransferValue; weightedOcfPct?; createdAtUtc; updatedAtUtc }
ContributionDto { payer: 'Member'|'Employer'|'ThirdParty'; amount; frequency: 'Single'|'Annually'|'Quarterly'|'Monthly';
                  escalationPct; isGrossOfTaxRelief; startMonth?; endMonth? }
HoldingDto { name; weightPct; isin?; fundId?; ocfPct? }
GuaranteesDto { guaranteedAnnuityRatePct?; guaranteedGrowthRatePct?; protectedTaxFreeCashPct?; protectedPensionAge?;
                withProfits; marketValueReductionPct; terminalBonus; loyaltyBonusPct }
DefinedBenefitDto {
  dateOfLeaving; normalRetirementAge; cetvGuaranteeExpiry;
  tranches: { name; accruedAnnualPension; revaluation: IndexRuleDto; escalation: IndexRuleDto; isGmp }[];
  spousePensionPct; guaranteePeriodYears; pclsCommutationFactor; maxPclsPct; earlyRetirementReductionPct;
  earliestUnreducedAge?; bridgingPensionAnnual; fundingStatus: 'FullyFunded'|'Deficit'|'PensionProtectionFund'
}
IndexRuleDto { basis: 'None'|'Fixed'|'Cpi'|'Rpi'|'LpiCpi'|'LpiRpi'|'Section148'; ratePct; capPct?; floorPct? }
ChargeScheduleDto {
  platformCharge?: TieredChargeDto; productCharge?: TieredChargeDto;
  fixedCharges: { amount; frequency; indexation: { basis: 'None'|'Cpi'|'Fixed'; ratePct }; appliesTo: 'Wrapper'|'Drawdown'|'Sipp'; description? }[];
  fundCharge: { kind: 'None'|'Explicit'|'FromHoldings'; ocfPct? };
  transactionCostsPct;
  adviserCharges: { initialPct; initialAmount; ongoingPct; ongoingAmount };
  dealingCharges: { fundDealAmount; etfDealAmount; expectedFundDealsPerYear; expectedEtfDealsPerYear };
  switchCharge: { amountPerSwitch; expectedSwitchesPerYear };
  exitPenalty: { bands: { untilYearsFromStart?; ratePct; amount }[] };
  bidOfferSpreadPct; allocationRatePct; largeFundDiscounts: { threshold; rebateRatePct }[]
}
TieredChargeDto { mode: 'Marginal'|'WholeOfFund'; bands: { upTo?: number; annualRatePct }[] }
```

## Market catalogue
| Method | Path | → Response |
|---|---|---|
| GET | /providers | `ProviderDto[] {id, name, kind, fcaFirmReferenceNumber?, website?}` |
| GET | /products?wrapper=Sipp&search= | `ProductSummary[]` |
| GET | /products/{id} | `ProductDetail` |
| GET | /funds?search=&sector=&maxOcfPct=&page=&pageSize= | `PagedResult<FundDto>` |
| GET | /funds/{isin} | `FundDto` |
| GET | /model-portfolios | `ModelPortfolioDto[]` |

```ts
ProductSummary { id; providerId; providerName; name; wrapperTypes: string[]; minimumInvestment; allowsFamilyLinking;
                 fundUniverse; dataQuality; effectiveChargePctAt100k; effectiveChargePctAt500k; currentChargeVersion; asAt; sourceUrl? }
ProductDetail extends ProductSummary { chargeVersions: { version; effectiveFrom; effectiveTo?; asAt; sourceUrl?; dataQuality; charges: ChargeScheduleDto }[] }
FundDto { id; isin; sedol?; name; shareClass?; managerName; type; iaSector?; morningstarCategory?; ocfPct; transactionCostsPct;
          assetAllocation: { equityPct; fixedInterestPct; propertyPct; cashPct; alternativesPct }; srri?;
          statistics: { return1YPct?; return3YPct?; return5YPct?; volatility3YPct?; sharpe3Y?; maxDrawdown3YPct?; yieldPct?; morningstarRating?; medalistRating? };
          price?; priceDate?; factsheetUrl?; sourceUrl?; asAt? }
ModelPortfolioDto { id; providerId; providerName; name; riskLevel; mpsFeePct; blendedOcfPct; totalInvestmentChargePct; holdings: { fundId; isin; name; weightPct; ocfPct }[] }
```

## Assumption sets
| Method | Path | → Response |
|---|---|---|
| GET | /assumption-sets | `AssumptionSetDto[]` |
| GET | /assumption-sets/{id} | `AssumptionSetDto` |
| POST | /assumption-sets/{id}/copy | `{name}` → `AssumptionSetDto` (firm copy) |
| PUT | /assumption-sets/{id} | `AssumptionSetWrite` → `AssumptionSetDto` (firm sets only) |

```ts
AssumptionSetDto { id; firmId?; name; isFcaStandard; version; growthLowerPct; growthIntermediatePct; growthHigherPct; inflationPct;
  earningsGrowthPct; rpiInflationPct; chargeInflationPct; preRetirementProductChargePct; annuityExpenseLoadingPct; spouseAgeGapYears;
  mortalityBasis; statePensionIncreasePct; taxYear; projectionBasis: 'Nominal'|'Real';
  marketInputs: { giltYieldUpTo5Pct; giltYield5To10Pct; giltYield10To15Pct; giltYieldOver15Pct; tvcAnnuityRateRpiLinkedPct; tvcAnnuityRateLevelPct; cobs13YPct; asAt } }
AssumptionSetWrite = AssumptionSetDto minus {id, firmId, isFcaStandard, version}
```

## Stateless calculations (live preview; not persisted; audited as "Preview")
| Method | Path | Body → Response |
|---|---|---|
| POST | /calculations/pension-switch | `PensionSwitchCalcRequest` → `PensionSwitchResultDto` |
| POST | /calculations/db-transfer | `DbTransferCalcRequest` → `DbTransferResultDto` |
| POST | /calculations/cashflow | `CashflowCalcRequest` → `CashflowResultDto` |
| POST | /calculations/cashflow/stochastic | `CashflowCalcRequest & {seed?, paths?}` → `StochasticResultDto` |
| POST | /calculations/riy | `{startValue, months, growthPct, charges: ChargeScheduleDto, weightedOcfPct?, contributions: ContributionDto[]}` → `RiyDto` |
| POST | /calculations/tax | `{taxYear?; regime; earnedIncome; pensionIncome; otherIncome; savingsInterest; dividends; grossPensionContributions; reliefAtSourceContributions; subjectToNi}` → `TaxComputationDto` |

```ts
PensionSwitchCalcRequest {
  clientId?;                          // when given, the server loads schemes by id; otherwise inline schemes are used
  cedingSchemes: ({ schemeId } | { inline: SchemeWrite & { name } })[];
  proposedProductId; proposedProductChargeVersion?; proposedHoldings: HoldingDto[]; proposedModelPortfolioId?;
  proposedAdviserCharges: { initialPct; initialAmount; ongoingPct; ongoingAmount };
  retirementAge; assumptionSetId?; overrides?: { growthIntermediatePct?; inflationPct?; growthLowerPct?; growthHigherPct? };
  redirectContributions: boolean      // default true: ceding contributions continue into the new product
}
PensionSwitchResultDto {
  totalNetTransferValue; initialAdviserCharge; anyGuaranteesFlagged; warnings: string[];
  lower: CriticalYieldAtRateDto; intermediate: CriticalYieldAtRateDto; higher: CriticalYieldAtRateDto;
  receivingRiy: RiyDto;
  schemes: { name; currentValue; netTransferValue; projectedValueIfRetained; riyIfRetained: RiyDto; riyIfSwitched: RiyDto;
             criticalYieldAlonePct; guaranteesFlagged; verdict: 'SwitchCandidate'|'Consider'|'Retain'|'Refer' }[];
  chart: { year; existingValue; receivingValue }[]   // intermediate rate, yearly, real terms
  engineVersion; calculatedAtUtc; assumptionSet: { id; name; version }
}
CriticalYieldAtRateDto { growthPct; existingValueAtRetirement; receivingValueAtRetirement; criticalYieldPct; criticalYieldRealPct; headroomPct; projectedGain; breakEvenYear?; converged }
RiyDto { growthPct; productRiyPct; totalRiyPct; rateAfterProductChargesPct; rateAfterAllChargesPct; valueBeforeCharges; valueAfterProductCharges; valueAfterAllCharges;
         productSentence; totalSentence; effectOfCharges: { year; paymentsToDate; beforeCharges; planAndInvestmentChargesOnly; afterAllCharges; effectOfDeductionsToDate }[];
         totalCharges: { platform; product; fund; transaction; adviserInitial; adviserOngoing; fixed; dealing; switch; discounts; allocationAndSpread; exitPenalty; total } }
DbTransferCalcRequest { clientId?; dbSchemeId? | inline: SchemeWrite & { name }; proposedProductId; proposedHoldings: HoldingDto[];
  proposedAdviserCharges; aptaGrowthPct; planEndAge; initialAdviceFee; workplaceDefaultChargePct?; assumptionSetId?; transferDate? }
DbTransferResultDto {
  tvc: { cashEquivalentTransferValue; estimatedReplacementCost; difference; retirementAgeUsed; termYears; giltYieldUsedPct; discountRateUsedPct;
         annuityCostAtRetirement; pensionAtRetirement; wording; notes: string[];
         tranches: { name; accruedAnnualPension; revaluationRatePct; yearsRevalued; pensionAtRetirement; escalationInPaymentPct; annuityInterestRatePct; annuityPricePerPound; annuityCost; isGmp }[] };
  criticalYields: { typeAAnnuityMatchPct; typeBPclsAndReducedPensionPct; drawdownHurdleRatePct; schemePcls; residualPensionAfterPcls; converged };
  sustainableRealIncomeFromTransfer;
  incomeComparison: { age; schemeIncomeNominal; schemeIncomeReal; drawdownIncomeReal; residualFundReal; schemeDeathBenefitReal }[];
  stressTests: { name; sustainableRealIncome; change }[];
  summary: { initialAdviceFee; revaluedMonthlyIncome; paybackMonths; firstYearChargesProposed; ongoingAnnualChargesProposed; firstYearChargesCeding; firstYearChargesWorkplaceDefault? };
  lifeExpectancyAtRetirement; warnings: string[]; engineVersion; calculatedAtUtc
}
CashflowCalcRequest {
  clientId?; person: PersonDto; partner?: PersonDto; planEndAge;
  incomes: { name; kind: 'Employment'|'SelfEmployment'|'Rental'|'DefinedBenefitPension'|'Annuity'|'StatePension'|'Other'; annualAmount; fromAge; toAge?; growthPct; isTaxable; personIndex }[];
  expenses: { name; annualAmount; fromAge; toAge? }[];
  assets: { name; kind: 'UncrystallisedPension'|'Drawdown'|'Isa'|'GeneralInvestmentAccount'|'Cash'|'Property'|'OnshoreBond'; value; growthPct; charges: ChargeScheduleDto;
            schemeId?; costBasis; annualContribution; employerContribution; salarySacrifice; personIndex; allocation?: { equityPct; fixedInterestPct; propertyPct; cashPct; alternativesPct } }[];
  events: { name; atAge; amount }[];
  strategy: { withdrawalOrder: string[]; crystallisation: 'PclsUpFront'|'PhasedUfpls'|'PhasedDrawdown'; drawdownRule: 'GapFill'|'FixedAmount'|'PercentOfPot'; drawdownParameter; reinvestSurplusIntoIsa; annuityPurchaseAge? };
  assumptionSetId?; overrides?: { inflationPct?; earningsGrowthPct?; statePensionIncreasePct? }
}
PersonDto { name; dateOfBirth; sex; taxRegime; retirementAge; statePensionForecastWeekly?; statePensionQualifyingYears?; mpaaTriggered }
CashflowResultDto { rows: CashflowRowDto[]; firstShortfallAge?; totalIncomeTax; totalShortfall; legacyAtEnd; legacyAtEndReal; lumpSumAllowanceUsed; succeeds; sustainableSpend; engineVersion; calculatedAtUtc }
CashflowRowDto { year; age; partnerAge?; employmentIncome; statePensionIncome; dbPensionIncome; otherIncome; pensionWithdrawalsTaxable; taxFreeCash; isaWithdrawals; giaWithdrawals; cashWithdrawals;
  incomeTax; nationalInsurance; capitalGainsTax; netIncome; netIncomeReal; expenses; surplus; shortfall; contributions; assets: { name; kind; value; valueReal }[]; totalAssets; totalAssetsReal; warnings: string[] }
StochasticResultDto { seed: string; paths; probabilityOfSuccess; totalAssetsReal: PercentileRowDto[]; netIncomeReal: PercentileRowDto[]; medianShortfallAge?; worstDecileShortfallAge?;
  conservativeness: { deterministicAssetsAtEnd; medianAssetsAtEnd; medianIsNoLessConservative }; meanLegacyReal; engineVersion }
PercentileRowDto { year; age; p5; p10; p25; p50; p75; p90; p95 }
TaxComputationDto { taxYear; regime; adjustedNetIncome; personalAllowance; taxableIncome; incomeTax; nationalInsurance; totalDeductions; marginalRatePct;
  lines: { category; band; amount; ratePct; tax }[] }
```

## Persisted analyses
| Method | Path | Body → Response |
|---|---|---|
| POST | /analyses/pension-switch | `PensionSwitchAnalysisWrite` → `PensionSwitchAnalysisDto` (201) |
| GET / PUT | /analyses/pension-switch/{id} | → `PensionSwitchAnalysisDto` |
| POST | /analyses/pension-switch/{id}/calculate | → `PensionSwitchAnalysisDto` (result populated, version +1) |
| POST | /analyses/pension-switch/{id}/lock | → `PensionSwitchAnalysisDto` |
| DELETE | /analyses/pension-switch/{id} | → 204 (draft only) |
| same five | /analyses/db-transfer[...] | `DbTransferAnalysisWrite` / `DbTransferAnalysisDto` |
| same five | /analyses/cashflow[...] | `CashflowPlanWrite` / `CashflowPlanDto` (+ `POST /{id}/calculate/stochastic`) |

```ts
AnalysisSummary { id; kind: 'pensionSwitch'|'dbTransfer'|'cashflow'; title; status: 'draft'|'calculated'|'locked'; version; calculatedAtUtc?; updatedAtUtc; createdBy; clientId }
PensionSwitchAnalysisWrite { clientId; title; retirementAge; cedingSchemeIds: string[]; proposedProductId?; proposedProductChargeVersion?; proposedHoldings: HoldingDto[];
  proposedModelPortfolioId?; proposedAdviserCharges; assumptionSetId; overrides?; rationale? }
PensionSwitchAnalysisDto extends PensionSwitchAnalysisWrite { id; firmId; status; version; resultHash?; calculatedAtUtc?; engineVersion?; result?: PensionSwitchResultDto; createdAtUtc; updatedAtUtc; createdBy }
DbTransferAnalysisWrite { clientId; dbSchemeId; transferDate; planEndAge; proposedProductId?; proposedProductChargeVersion?; proposedHoldings; proposedAdviserCharges; aptaGrowthPct?;
  chargeBasis: 'nonContingent'|'contingent'; contingentChargingCarveOut?; workplaceSchemeProductId?; assumptionSetId; overrides?; initialAdviceFee; workplaceDefaultChargePct? }
DbTransferAnalysisDto extends DbTransferAnalysisWrite { id; firmId; status; version; resultHash?; calculatedAtUtc?; engineVersion?; result?: DbTransferResultDto; ... }
CashflowPlanWrite { clientId; partnerClientId?; title; planEndAge; incomes; expenses; assets; events; strategy; stochasticSeed?; stochasticPaths; assumptionSetId; overrides? }
CashflowPlanDto extends CashflowPlanWrite { id; firmId; status; version; resultHash?; calculatedAtUtc?; engineVersion?; result?: CashflowResultDto; stochasticResult?: StochasticResultDto; ... }
```

## Reports
| Method | Path | Body → Response |
|---|---|---|
| POST | /reports | `{analysisId; kind: 'suitability'|'pensionSwitch'|'dbTransfer'|'cashflow'|'fundComparison'; format: 'pdf'|'docx'|'json'}` → `ReportDto` (201; locks the analysis). The kind must suit the analysis: a `dbTransfer` report on a pension-switch analysis returns 400. Reports may be issued repeatedly after locking. |
| GET | /reports/{id} | → `ReportDto` |
| GET | /reports/{id}/download | → file (`application/pdf` etc.) |

```ts
ReportDto { id; clientId; analysisId; analysisVersion; analysisResultHash; kind; format; templateVersion; generatedBy; sha256; sizeBytes; generatedAtUtc; downloadUrl }
```

## Audit, integrations, health
| Method | Path | → Response |
|---|---|---|
| GET | /audit?entityId=&entityType=&page=&pageSize= | `PagedResult<AuditEventDto {id, sequence, userId?, occurredAtUtc, entityType, entityId?, action, payload, previousHash, hash}>` |
| GET | /audit/verify | `{isValid; firstBrokenIndex; reason?; eventsChecked}` (Compliance or FirmAdmin only; 403 for an adviser) |
| GET | /integrations | `{connector: 'Intelliflo'|'Xplan'|'TruePotential'|'OrigoHub'|'Morningstar'; configured; mode: 'Live'|'Sandbox'|'Disabled'; lastSyncUtc?}[]` |
| POST | /integrations/{connector}/import | `{externalClientId?}` → `{imported; updated; skipped; messages: string[]}` |
| POST | /integrations/morningstar/sync | → `{fundsUpdated; messages}` |
| GET | /healthz | 200 `text/plain` `Healthy` (anonymous) |

There is no collection endpoint for analyses. List them per client with `GET /clients/{id}/analyses`,
or take the firm-wide recent set from `GET /dashboard/summary`.

## Seed data files (consumed by the Infrastructure seeder)
- `data/providers.json`: `{ providers: [{ name, kind, fcaFirmReferenceNumber?, website?, products: [{ name, wrapperTypes: string[], minimumInvestment, allowsFamilyLinking, fundUniverse, effectiveFrom, asAt, sourceUrl, dataQuality, charges: ChargeScheduleDto }] }] }`
- `data/funds.json`: `{ funds: FundDto-like[] }` (same field names as `FundDto` without ids)
- `data/model-portfolios.json`: `{ modelPortfolios: [{ providerName, name, riskLevel, mpsFeePct, holdings: [{ isin, weightPct }] }] }`
- `data/assumption-sets.json`: FCA-standard sets (2026/27) with market inputs and an as-at date.
- `data/capital-market-assumptions.json`: asset classes with `expectedReturnPct`, `volatilityPct`, and a `correlations` matrix.
