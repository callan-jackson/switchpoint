import { del, download, get, post, put } from './client'
import type {
  AnalysisSummary,
  AssumptionSetCopyRequest,
  AssumptionSetDto,
  AssumptionSetWrite,
  AuditEventDto,
  AuditVerifyResult,
  CashflowCalcRequest,
  CashflowPlanDto,
  CashflowPlanWrite,
  CashflowResultDto,
  ClientDetail,
  ClientSummary,
  ClientWrite,
  DashboardSummary,
  DbTransferAnalysisDto,
  DbTransferAnalysisWrite,
  DbTransferCalcRequest,
  DbTransferResultDto,
  FundDto,
  HealthResponse,
  IntegrationConnector,
  IntegrationDto,
  IntegrationImportRequest,
  IntegrationImportResult,
  LoginRequest,
  LoginResponse,
  ModelPortfolioDto,
  MorningstarSyncResult,
  PagedResult,
  PensionSwitchAnalysisDto,
  PensionSwitchAnalysisWrite,
  PensionSwitchCalcRequest,
  PensionSwitchResultDto,
  ProductDetail,
  ProductSummary,
  ProviderDto,
  ReportCreateRequest,
  ReportDto,
  RiyCalcRequest,
  RiyDto,
  SchemeDto,
  SchemeWrite,
  StochasticCalcRequest,
  StochasticResultDto,
  TaxCalcRequest,
  TaxComputationDto,
  UserDto,
} from './types'

/**
 * One function per endpoint in docs/api/CONTRACT.md. Every function accepts an optional
 * `AbortSignal` as its last argument so live previews can cancel superseded requests.
 * Query hooks in `queries.ts` wrap the common ones; pages call these directly for the rest.
 */

// ---------------------------------------------------------------------------
// Query parameter shapes
// ---------------------------------------------------------------------------

export interface ClientListQuery {
  search?: string
  page?: number
  pageSize?: number
}

export interface ProductListQuery {
  wrapper?: string
  search?: string
}

export interface FundListQuery {
  search?: string
  sector?: string
  maxOcfPct?: number
  page?: number
  pageSize?: number
}

export interface AuditListQuery {
  entityId?: string
  entityType?: string
  page?: number
  pageSize?: number
}

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

export const auth = {
  login: (body: LoginRequest, signal?: AbortSignal) =>
    post<LoginResponse>('/auth/login', body, { signal }),
  me: (signal?: AbortSignal) => get<UserDto>('/auth/me', { signal }),
}

// ---------------------------------------------------------------------------
// Clients and schemes
// ---------------------------------------------------------------------------

export const clients = {
  list: (query: ClientListQuery = {}, signal?: AbortSignal) =>
    get<PagedResult<ClientSummary>>('/clients', { query: { ...query }, signal }),
  create: (body: ClientWrite) => post<ClientDetail>('/clients', body),
  get: (id: string, signal?: AbortSignal) => get<ClientDetail>(`/clients/${id}`, { signal }),
  update: (id: string, body: ClientWrite) => put<ClientDetail>(`/clients/${id}`, body),
  remove: (id: string) => del(`/clients/${id}`),
  analyses: (id: string, signal?: AbortSignal) =>
    get<AnalysisSummary[]>(`/clients/${id}/analyses`, { signal }),
  reports: (id: string, signal?: AbortSignal) =>
    get<ReportDto[]>(`/clients/${id}/reports`, { signal }),
}

export const schemes = {
  list: (clientId: string, signal?: AbortSignal) =>
    get<SchemeDto[]>(`/clients/${clientId}/schemes`, { signal }),
  create: (clientId: string, body: SchemeWrite) =>
    post<SchemeDto>(`/clients/${clientId}/schemes`, body),
  update: (clientId: string, schemeId: string, body: SchemeWrite) =>
    put<SchemeDto>(`/clients/${clientId}/schemes/${schemeId}`, body),
  remove: (clientId: string, schemeId: string) =>
    del(`/clients/${clientId}/schemes/${schemeId}`),
}

// ---------------------------------------------------------------------------
// Market catalogue
// ---------------------------------------------------------------------------

export const catalogue = {
  providers: (signal?: AbortSignal) => get<ProviderDto[]>('/providers', { signal }),
  products: (query: ProductListQuery = {}, signal?: AbortSignal) =>
    get<ProductSummary[]>('/products', { query: { ...query }, signal }),
  product: (id: string, signal?: AbortSignal) => get<ProductDetail>(`/products/${id}`, { signal }),
  funds: (query: FundListQuery = {}, signal?: AbortSignal) =>
    get<PagedResult<FundDto>>('/funds', { query: { ...query }, signal }),
  fund: (isin: string, signal?: AbortSignal) => get<FundDto>(`/funds/${isin}`, { signal }),
  modelPortfolios: (signal?: AbortSignal) =>
    get<ModelPortfolioDto[]>('/model-portfolios', { signal }),
}

// ---------------------------------------------------------------------------
// Assumption sets
// ---------------------------------------------------------------------------

export const assumptionSets = {
  list: (signal?: AbortSignal) => get<AssumptionSetDto[]>('/assumption-sets', { signal }),
  get: (id: string, signal?: AbortSignal) =>
    get<AssumptionSetDto>(`/assumption-sets/${id}`, { signal }),
  copy: (id: string, body: AssumptionSetCopyRequest) =>
    post<AssumptionSetDto>(`/assumption-sets/${id}/copy`, body),
  update: (id: string, body: AssumptionSetWrite) =>
    put<AssumptionSetDto>(`/assumption-sets/${id}`, body),
}

// ---------------------------------------------------------------------------
// Stateless calculations (live preview)
// ---------------------------------------------------------------------------

export const calculations = {
  pensionSwitch: (body: PensionSwitchCalcRequest, signal?: AbortSignal) =>
    post<PensionSwitchResultDto>('/calculations/pension-switch', body, { signal }),
  dbTransfer: (body: DbTransferCalcRequest, signal?: AbortSignal) =>
    post<DbTransferResultDto>('/calculations/db-transfer', body, { signal }),
  cashflow: (body: CashflowCalcRequest, signal?: AbortSignal) =>
    post<CashflowResultDto>('/calculations/cashflow', body, { signal }),
  cashflowStochastic: (body: StochasticCalcRequest, signal?: AbortSignal) =>
    post<StochasticResultDto>('/calculations/cashflow/stochastic', body, { signal }),
  riy: (body: RiyCalcRequest, signal?: AbortSignal) =>
    post<RiyDto>('/calculations/riy', body, { signal }),
  tax: (body: TaxCalcRequest, signal?: AbortSignal) =>
    post<TaxComputationDto>('/calculations/tax', body, { signal }),
}

// ---------------------------------------------------------------------------
// Persisted analyses
// ---------------------------------------------------------------------------

/** The five verbs every analysis kind supports, bound to its base path. */
export interface AnalysisEndpoints<TWrite, TDto> {
  create: (body: TWrite) => Promise<TDto>
  get: (id: string, signal?: AbortSignal) => Promise<TDto>
  update: (id: string, body: TWrite) => Promise<TDto>
  calculate: (id: string) => Promise<TDto>
  lock: (id: string) => Promise<TDto>
  remove: (id: string) => Promise<void>
}

function analysisEndpoints<TWrite, TDto>(base: string): AnalysisEndpoints<TWrite, TDto> {
  return {
    create: (body) => post<TDto>(base, body),
    get: (id, signal) => get<TDto>(`${base}/${id}`, { signal }),
    update: (id, body) => put<TDto>(`${base}/${id}`, body),
    calculate: (id) => post<TDto>(`${base}/${id}/calculate`),
    lock: (id) => post<TDto>(`${base}/${id}/lock`),
    remove: (id) => del(`${base}/${id}`),
  }
}

export const analyses = {
  pensionSwitch: analysisEndpoints<PensionSwitchAnalysisWrite, PensionSwitchAnalysisDto>(
    '/analyses/pension-switch',
  ),
  dbTransfer: analysisEndpoints<DbTransferAnalysisWrite, DbTransferAnalysisDto>(
    '/analyses/db-transfer',
  ),
  cashflow: {
    ...analysisEndpoints<CashflowPlanWrite, CashflowPlanDto>('/analyses/cashflow'),
    calculateStochastic: (id: string) =>
      post<CashflowPlanDto>(`/analyses/cashflow/${id}/calculate/stochastic`),
  },
}

// ---------------------------------------------------------------------------
// Reports
// ---------------------------------------------------------------------------

export const reports = {
  create: (body: ReportCreateRequest) => post<ReportDto>('/reports', body),
  get: (id: string, signal?: AbortSignal) => get<ReportDto>(`/reports/${id}`, { signal }),
  /** Binary download; use `saveBlob` from `@/lib/files` to hand it to the browser. */
  download: (id: string, signal?: AbortSignal) => download(`/reports/${id}/download`, { signal }),
}

// ---------------------------------------------------------------------------
// Audit, integrations, health
// ---------------------------------------------------------------------------

export const audit = {
  list: (query: AuditListQuery = {}, signal?: AbortSignal) =>
    get<PagedResult<AuditEventDto>>('/audit', { query: { ...query }, signal }),
  verify: (signal?: AbortSignal) => get<AuditVerifyResult>('/audit/verify', { signal }),
}

export const integrations = {
  list: (signal?: AbortSignal) => get<IntegrationDto[]>('/integrations', { signal }),
  import: (connector: IntegrationConnector, body: IntegrationImportRequest = {}) =>
    post<IntegrationImportResult>(`/integrations/${connector}/import`, body),
  morningstarSync: () => post<MorningstarSyncResult>('/integrations/morningstar/sync'),
}

export const health = {
  get: (signal?: AbortSignal) => get<HealthResponse>('/healthz', { signal }),
}

// ---------------------------------------------------------------------------
// Dashboard
// ---------------------------------------------------------------------------

export const dashboard = {
  summary: (signal?: AbortSignal) => get<DashboardSummary>('/dashboard/summary', { signal }),
}

export const endpoints = {
  auth,
  clients,
  schemes,
  catalogue,
  assumptionSets,
  calculations,
  analyses,
  reports,
  audit,
  integrations,
  health,
  dashboard,
}
