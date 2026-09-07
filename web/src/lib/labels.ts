import type {
  AnalysisKind,
  AnalysisStatus,
  CashflowAssetKind,
  CashflowIncomeKind,
  DataQuality,
  SchemeType,
  SwitchVerdict,
} from '@/api/types'

/** Display labels for contract enums. Anything missing falls back to `humanise`. */
export const schemeTypeLabels: Record<SchemeType, string> = {
  PersonalPension: 'Personal pension',
  StakeholderPension: 'Stakeholder pension',
  Sipp: 'SIPP',
  OccupationalMoneyPurchase: 'Occupational money purchase',
  DefinedBenefit: 'Defined benefit',
  Section32: 'Section 32 buy-out',
  RetirementAnnuityContract: 'Retirement annuity contract',
  Isa: 'ISA',
  GeneralInvestmentAccount: 'General investment account',
  OnshoreBond: 'Onshore bond',
  OffshoreBond: 'Offshore bond',
  DrawdownPlan: 'Drawdown plan',
}

export const analysisKindLabels: Record<AnalysisKind, string> = {
  PensionSwitch: 'Pension switch',
  DbTransfer: 'DB transfer',
  Cashflow: 'Cashflow',
}

export const analysisStatusLabels: Record<AnalysisStatus, string> = {
  Draft: 'Draft',
  Calculated: 'Calculated',
  Locked: 'Locked',
}

export const verdictLabels: Record<SwitchVerdict, string> = {
  SwitchCandidate: 'Switch candidate',
  Consider: 'Consider',
  Retain: 'Retain',
  Refer: 'Refer',
}

export const dataQualityLabels: Record<DataQuality, string> = {
  Verified: 'Verified',
  Indicative: 'Indicative',
  Placeholder: 'Placeholder',
}

export const cashflowAssetKindLabels: Record<CashflowAssetKind, string> = {
  UncrystallisedPension: 'Uncrystallised pension',
  Drawdown: 'Drawdown',
  Isa: 'ISA',
  GeneralInvestmentAccount: 'GIA',
  Cash: 'Cash',
  Property: 'Property',
  OnshoreBond: 'Onshore bond',
}

export const cashflowIncomeKindLabels: Record<CashflowIncomeKind, string> = {
  Employment: 'Employment',
  SelfEmployment: 'Self-employment',
  Rental: 'Rental',
  DefinedBenefitPension: 'DB pension',
  Annuity: 'Annuity',
  StatePension: 'State pension',
  Other: 'Other',
}

/** Route to open a persisted analysis of the given kind. */
export function analysisPath(kind: AnalysisKind, id: string): string {
  switch (kind) {
    case 'PensionSwitch':
      return `/pension-switch/${id}`
    case 'DbTransfer':
      return `/db-transfer/${id}`
    case 'Cashflow':
      return `/cashflow/${id}`
  }
}

/** DC scheme types that can be ceded in a pension switch. */
export const DC_SCHEME_TYPES: SchemeType[] = [
  'PersonalPension',
  'StakeholderPension',
  'Sipp',
  'OccupationalMoneyPurchase',
  'Section32',
  'RetirementAnnuityContract',
  'DrawdownPlan',
]
