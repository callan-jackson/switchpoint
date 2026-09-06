import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { UseQueryOptions } from '@tanstack/react-query'
import {
  analyses,
  assumptionSets,
  audit,
  auth,
  catalogue,
  clients,
  integrations,
  reports,
  schemes,
} from './endpoints'
import type { AuditListQuery, ClientListQuery, FundListQuery, ProductListQuery } from './endpoints'
import type {
  AssumptionSetCopyRequest,
  AssumptionSetWrite,
  CashflowPlanWrite,
  ClientWrite,
  DbTransferAnalysisWrite,
  IntegrationConnector,
  IntegrationImportRequest,
  PensionSwitchAnalysisWrite,
  ReportCreateRequest,
  SchemeWrite,
} from './types'

/**
 * Query-key factory. Keys are hierarchical so invalidating `queryKeys.clients.all` also drops
 * every list/detail/scheme query beneath it. Always build keys through this object; never
 * hand-write arrays in pages.
 */
export const queryKeys = {
  auth: {
    me: ['auth', 'me'] as const,
  },
  clients: {
    all: ['clients'] as const,
    lists: () => ['clients', 'list'] as const,
    list: (query: ClientListQuery) => ['clients', 'list', query] as const,
    detail: (id: string) => ['clients', 'detail', id] as const,
    schemes: (id: string) => ['clients', 'detail', id, 'schemes'] as const,
    analyses: (id: string) => ['clients', 'detail', id, 'analyses'] as const,
    reports: (id: string) => ['clients', 'detail', id, 'reports'] as const,
  },
  providers: {
    all: ['providers'] as const,
  },
  products: {
    all: ['products'] as const,
    list: (query: ProductListQuery) => ['products', 'list', query] as const,
    detail: (id: string) => ['products', 'detail', id] as const,
  },
  funds: {
    all: ['funds'] as const,
    list: (query: FundListQuery) => ['funds', 'list', query] as const,
    detail: (isin: string) => ['funds', 'detail', isin] as const,
  },
  modelPortfolios: {
    all: ['model-portfolios'] as const,
  },
  assumptionSets: {
    all: ['assumption-sets'] as const,
    detail: (id: string) => ['assumption-sets', 'detail', id] as const,
  },
  analyses: {
    all: ['analyses'] as const,
    pensionSwitch: (id: string) => ['analyses', 'pension-switch', id] as const,
    dbTransfer: (id: string) => ['analyses', 'db-transfer', id] as const,
    cashflow: (id: string) => ['analyses', 'cashflow', id] as const,
  },
  reports: {
    all: ['reports'] as const,
    detail: (id: string) => ['reports', 'detail', id] as const,
  },
  audit: {
    all: ['audit'] as const,
    list: (query: AuditListQuery) => ['audit', 'list', query] as const,
    verify: ['audit', 'verify'] as const,
  },
  integrations: {
    all: ['integrations'] as const,
  },
}

/** Catalogue data changes rarely; cache it for the session. */
const CATALOGUE_STALE_MS = 10 * 60_000

type QueryOverrides<T> = Omit<UseQueryOptions<T>, 'queryKey' | 'queryFn'>

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

export function useMe(options?: QueryOverrides<Awaited<ReturnType<typeof auth.me>>>) {
  return useQuery({ queryKey: queryKeys.auth.me, queryFn: ({ signal }) => auth.me(signal), ...options })
}

// ---------------------------------------------------------------------------
// Clients and schemes
// ---------------------------------------------------------------------------

export function useClients(query: ClientListQuery = {}) {
  return useQuery({
    queryKey: queryKeys.clients.list(query),
    queryFn: ({ signal }) => clients.list(query, signal),
    placeholderData: (previous) => previous,
  })
}

export function useClient(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.clients.detail(id ?? ''),
    queryFn: ({ signal }) => clients.get(id ?? '', signal),
    enabled: !!id,
  })
}

export function useClientSchemes(clientId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.clients.schemes(clientId ?? ''),
    queryFn: ({ signal }) => schemes.list(clientId ?? '', signal),
    enabled: !!clientId,
  })
}

export function useClientAnalyses(clientId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.clients.analyses(clientId ?? ''),
    queryFn: ({ signal }) => clients.analyses(clientId ?? '', signal),
    enabled: !!clientId,
  })
}

export function useClientReports(clientId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.clients.reports(clientId ?? ''),
    queryFn: ({ signal }) => clients.reports(clientId ?? '', signal),
    enabled: !!clientId,
  })
}

export function useCreateClient() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: ClientWrite) => clients.create(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.clients.all }),
  })
}

export function useUpdateClient(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: ClientWrite) => clients.update(id, body),
    onSuccess: (detail) => {
      qc.setQueryData(queryKeys.clients.detail(id), detail)
      return qc.invalidateQueries({ queryKey: queryKeys.clients.lists() })
    },
  })
}

export function useDeleteClient() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => clients.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.clients.all }),
  })
}

export function useCreateScheme(clientId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: SchemeWrite) => schemes.create(clientId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.clients.detail(clientId) }),
  })
}

export function useUpdateScheme(clientId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ schemeId, body }: { schemeId: string; body: SchemeWrite }) =>
      schemes.update(clientId, schemeId, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.clients.detail(clientId) }),
  })
}

export function useDeleteScheme(clientId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (schemeId: string) => schemes.remove(clientId, schemeId),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.clients.detail(clientId) }),
  })
}

// ---------------------------------------------------------------------------
// Market catalogue
// ---------------------------------------------------------------------------

export function useProviders() {
  return useQuery({
    queryKey: queryKeys.providers.all,
    queryFn: ({ signal }) => catalogue.providers(signal),
    staleTime: CATALOGUE_STALE_MS,
  })
}

export function useProducts(query: ProductListQuery = {}) {
  return useQuery({
    queryKey: queryKeys.products.list(query),
    queryFn: ({ signal }) => catalogue.products(query, signal),
    staleTime: CATALOGUE_STALE_MS,
    placeholderData: (previous) => previous,
  })
}

export function useProduct(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.products.detail(id ?? ''),
    queryFn: ({ signal }) => catalogue.product(id ?? '', signal),
    enabled: !!id,
    staleTime: CATALOGUE_STALE_MS,
  })
}

export function useFunds(query: FundListQuery = {}) {
  return useQuery({
    queryKey: queryKeys.funds.list(query),
    queryFn: ({ signal }) => catalogue.funds(query, signal),
    staleTime: CATALOGUE_STALE_MS,
    placeholderData: (previous) => previous,
  })
}

export function useFund(isin: string | undefined) {
  return useQuery({
    queryKey: queryKeys.funds.detail(isin ?? ''),
    queryFn: ({ signal }) => catalogue.fund(isin ?? '', signal),
    enabled: !!isin,
    staleTime: CATALOGUE_STALE_MS,
  })
}

export function useModelPortfolios() {
  return useQuery({
    queryKey: queryKeys.modelPortfolios.all,
    queryFn: ({ signal }) => catalogue.modelPortfolios(signal),
    staleTime: CATALOGUE_STALE_MS,
  })
}

// ---------------------------------------------------------------------------
// Assumption sets
// ---------------------------------------------------------------------------

export function useAssumptionSets() {
  return useQuery({
    queryKey: queryKeys.assumptionSets.all,
    queryFn: ({ signal }) => assumptionSets.list(signal),
    staleTime: CATALOGUE_STALE_MS,
  })
}

export function useAssumptionSet(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.assumptionSets.detail(id ?? ''),
    queryFn: ({ signal }) => assumptionSets.get(id ?? '', signal),
    enabled: !!id,
  })
}

export function useCopyAssumptionSet() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: AssumptionSetCopyRequest }) =>
      assumptionSets.copy(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.assumptionSets.all }),
  })
}

export function useUpdateAssumptionSet(id: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: AssumptionSetWrite) => assumptionSets.update(id, body),
    onSuccess: (dto) => {
      qc.setQueryData(queryKeys.assumptionSets.detail(id), dto)
      return qc.invalidateQueries({ queryKey: queryKeys.assumptionSets.all })
    },
  })
}

// ---------------------------------------------------------------------------
// Persisted analyses
// ---------------------------------------------------------------------------

export function usePensionSwitchAnalysis(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.analyses.pensionSwitch(id ?? ''),
    queryFn: ({ signal }) => analyses.pensionSwitch.get(id ?? '', signal),
    enabled: !!id,
  })
}

export function useDbTransferAnalysis(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.analyses.dbTransfer(id ?? ''),
    queryFn: ({ signal }) => analyses.dbTransfer.get(id ?? '', signal),
    enabled: !!id,
  })
}

export function useCashflowPlan(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.analyses.cashflow(id ?? ''),
    queryFn: ({ signal }) => analyses.cashflow.get(id ?? '', signal),
    enabled: !!id,
  })
}

/** Create / update / calculate / lock / delete for a pension switch analysis. */
export function usePensionSwitchMutations() {
  const qc = useQueryClient()
  const invalidate = (id?: string) =>
    Promise.all([
      id ? qc.invalidateQueries({ queryKey: queryKeys.analyses.pensionSwitch(id) }) : undefined,
      qc.invalidateQueries({ queryKey: queryKeys.clients.all }),
    ])
  return {
    create: useMutation({
      mutationFn: (body: PensionSwitchAnalysisWrite) => analyses.pensionSwitch.create(body),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: PensionSwitchAnalysisWrite }) =>
        analyses.pensionSwitch.update(id, body),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    calculate: useMutation({
      mutationFn: (id: string) => analyses.pensionSwitch.calculate(id),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    lock: useMutation({
      mutationFn: (id: string) => analyses.pensionSwitch.lock(id),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    remove: useMutation({
      mutationFn: (id: string) => analyses.pensionSwitch.remove(id),
      onSuccess: () => invalidate(),
    }),
  }
}

/** Create / update / calculate / lock / delete for a DB transfer analysis. */
export function useDbTransferMutations() {
  const qc = useQueryClient()
  const invalidate = (id?: string) =>
    Promise.all([
      id ? qc.invalidateQueries({ queryKey: queryKeys.analyses.dbTransfer(id) }) : undefined,
      qc.invalidateQueries({ queryKey: queryKeys.clients.all }),
    ])
  return {
    create: useMutation({
      mutationFn: (body: DbTransferAnalysisWrite) => analyses.dbTransfer.create(body),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: DbTransferAnalysisWrite }) =>
        analyses.dbTransfer.update(id, body),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    calculate: useMutation({
      mutationFn: (id: string) => analyses.dbTransfer.calculate(id),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    lock: useMutation({
      mutationFn: (id: string) => analyses.dbTransfer.lock(id),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    remove: useMutation({
      mutationFn: (id: string) => analyses.dbTransfer.remove(id),
      onSuccess: () => invalidate(),
    }),
  }
}

/** Create / update / calculate (+ stochastic) / lock / delete for a cashflow plan. */
export function useCashflowPlanMutations() {
  const qc = useQueryClient()
  const invalidate = (id?: string) =>
    Promise.all([
      id ? qc.invalidateQueries({ queryKey: queryKeys.analyses.cashflow(id) }) : undefined,
      qc.invalidateQueries({ queryKey: queryKeys.clients.all }),
    ])
  return {
    create: useMutation({
      mutationFn: (body: CashflowPlanWrite) => analyses.cashflow.create(body),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: CashflowPlanWrite }) =>
        analyses.cashflow.update(id, body),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    calculate: useMutation({
      mutationFn: (id: string) => analyses.cashflow.calculate(id),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    calculateStochastic: useMutation({
      mutationFn: (id: string) => analyses.cashflow.calculateStochastic(id),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    lock: useMutation({
      mutationFn: (id: string) => analyses.cashflow.lock(id),
      onSuccess: (dto) => invalidate(dto.id),
    }),
    remove: useMutation({
      mutationFn: (id: string) => analyses.cashflow.remove(id),
      onSuccess: () => invalidate(),
    }),
  }
}

// ---------------------------------------------------------------------------
// Reports
// ---------------------------------------------------------------------------

export function useReport(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.reports.detail(id ?? ''),
    queryFn: ({ signal }) => reports.get(id ?? '', signal),
    enabled: !!id,
  })
}

export function useCreateReport() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: ReportCreateRequest) => reports.create(body),
    onSuccess: (dto) =>
      Promise.all([
        qc.invalidateQueries({ queryKey: queryKeys.clients.reports(dto.clientId) }),
        qc.invalidateQueries({ queryKey: queryKeys.analyses.all }),
      ]),
  })
}

export function useDownloadReport() {
  return useMutation({ mutationFn: (id: string) => reports.download(id) })
}

// ---------------------------------------------------------------------------
// Audit and integrations
// ---------------------------------------------------------------------------

export function useAuditEvents(query: AuditListQuery = {}) {
  return useQuery({
    queryKey: queryKeys.audit.list(query),
    queryFn: ({ signal }) => audit.list(query, signal),
    placeholderData: (previous) => previous,
  })
}

export function useAuditVerify(enabled = true) {
  return useQuery({
    queryKey: queryKeys.audit.verify,
    queryFn: ({ signal }) => audit.verify(signal),
    enabled,
    staleTime: 0,
  })
}

export function useIntegrations() {
  return useQuery({
    queryKey: queryKeys.integrations.all,
    queryFn: ({ signal }) => integrations.list(signal),
  })
}

export function useImportFromIntegration() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({
      connector,
      body,
    }: {
      connector: IntegrationConnector
      body?: IntegrationImportRequest
    }) => integrations.import(connector, body),
    onSuccess: () =>
      Promise.all([
        qc.invalidateQueries({ queryKey: queryKeys.clients.all }),
        qc.invalidateQueries({ queryKey: queryKeys.integrations.all }),
      ]),
  })
}
