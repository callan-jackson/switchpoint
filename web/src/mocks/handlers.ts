import { HttpResponse, http, type HttpHandler } from 'msw'
import type {
  AnalysisSummary,
  AssumptionSetDto,
  CashflowCalcRequest,
  CashflowPlanDto,
  CashflowPlanWrite,
  ClientDetail,
  ClientWrite,
  DbTransferAnalysisDto,
  DbTransferAnalysisWrite,
  DbTransferCalcRequest,
  PensionSwitchAnalysisDto,
  PensionSwitchAnalysisWrite,
  PensionSwitchCalcRequest,
  ReportCreateRequest,
  ReportDto,
  SchemeDto,
  SchemeWrite,
  StochasticCalcRequest,
} from '@/api/types'
import {
  DEMO_PASSWORD,
  FIRM_ID,
  analyses,
  assumptionSets,
  auditEvents,
  clientSummary,
  clients,
  funds,
  modelPortfolios,
  products,
  providers,
  reports,
  users,
} from './data'
import { cashflowResult, dbTransferResult, pensionSwitchResult, stochasticResult } from './results'

const BASE = '/api/v1'

/** Mutable copies so the mock server behaves like a real store within a session. */
const db = {
  clients: structuredClone(clients),
  analyses: structuredClone(analyses) as AnalysisSummary[],
  reports: structuredClone(reports) as ReportDto[],
  assumptionSets: structuredClone(assumptionSets) as AssumptionSetDto[],
  pensionSwitch: new Map<string, PensionSwitchAnalysisDto>(),
  dbTransfer: new Map<string, DbTransferAnalysisDto>(),
  cashflow: new Map<string, CashflowPlanDto>(),
}

export function resetMockDb() {
  db.clients = structuredClone(clients)
  db.analyses = structuredClone(analyses)
  db.reports = structuredClone(reports)
  db.assumptionSets = structuredClone(assumptionSets)
  db.pensionSwitch.clear()
  db.dbTransfer.clear()
  db.cashflow.clear()
  seedSavedAnalyses()
}

/**
 * A saved, locked pension-switch analysis matching the `an-switch-1` summary.
 *
 * Without this the store starts empty, so `GET /analyses/pension-switch/:id` 404s for every id in the
 * summary list and no test can open a saved analysis — or copy one.
 */
function seedSavedAnalyses() {
  db.pensionSwitch.set('an-switch-1', {
    id: 'an-switch-1',
    firmId: FIRM_ID,
    clientId: 'c-sarah',
    title: 'Sarah Mitchell — consolidation to Investcentre SIPP',
    status: 'locked',
    version: 2,
    retirementAge: 67,
    cedingSchemeIds: [],
    proposedHoldings: [],
    proposedAdviserCharges: { initialPct: 1, initialAmount: 0, ongoingPct: 0.5, ongoingAmount: 0 },
    assumptionSetId: db.assumptionSets[0]?.id ?? '',
    rationale: 'Lower ongoing charges and access to drawdown.',
    createdAtUtc: '2026-09-05T14:22:00Z',
    updatedAtUtc: '2026-09-05T14:22:00Z',
    createdBy: users.adviser.id,
  } as PensionSwitchAnalysisDto)
}


seedSavedAnalyses()

let idSeed = 1
const newId = (prefix: string) => `${prefix}-${(idSeed++).toString().padStart(4, '0')}`

const nowIso = () => new Date().toISOString()

function problem(status: number, title: string, detail?: string, errors?: Record<string, string[]>) {
  return HttpResponse.json(
    { type: `https://httpstatuses.io/${status}`, title, status, detail, errors, traceId: 'mock-trace' },
    { status, headers: { 'content-type': 'application/problem+json' } },
  )
}

function envelope(status: 'draft' | 'calculated' | 'locked' = 'draft') {
  return {
    firmId: FIRM_ID,
    status,
    version: 0,
    createdAtUtc: nowIso(),
    updatedAtUtc: nowIso(),
    createdBy: users.adviser.id,
  }
}

function upsertSummary(summary: AnalysisSummary) {
  const index = db.analyses.findIndex((a) => a.id === summary.id)
  if (index >= 0) db.analyses[index] = summary
  else db.analyses.unshift(summary)
}

export const handlers: HttpHandler[] = [
  // --- health ------------------------------------------------------------
  http.get('/healthz', () => HttpResponse.text('Healthy')),

  // --- auth --------------------------------------------------------------
  http.post(`${BASE}/auth/login`, async ({ request }) => {
    const body = (await request.json()) as { email?: string; password?: string }
    const email = (body.email ?? '').toLowerCase()
    const user = Object.values(users).find((u) => u.email === email)
    if (!user || body.password !== DEMO_PASSWORD) {
      return problem(401, 'Not signed in', 'Those details were not recognised.')
    }
    return HttpResponse.json({
      accessToken: `mock.${user.id}.token`,
      expiresAtUtc: new Date(Date.now() + 8 * 3_600_000).toISOString(),
      user,
    })
  }),
  http.get(`${BASE}/auth/me`, () => HttpResponse.json(users.adviser)),

  // --- dashboard ---------------------------------------------------------
  http.get(`${BASE}/dashboard/summary`, () =>
    HttpResponse.json({
      clients: db.clients.length,
      analysesInProgress: db.analyses.filter((a) => a.status !== 'locked').length,
      reportsThisMonth: db.reports.length,
      fundsInCatalogue: funds.length,
      recentAnalyses: db.analyses.slice(0, 5),
    }),
  ),

  // --- clients -----------------------------------------------------------
  http.get(`${BASE}/clients`, ({ request }) => {
    const url = new URL(request.url)
    const search = (url.searchParams.get('search') ?? '').toLowerCase()
    const page = Number(url.searchParams.get('page') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 25)
    const matched = db.clients
      .filter((c) => !search || c.fullName.toLowerCase().includes(search) || (c.email ?? '').toLowerCase().includes(search))
      .map(clientSummary)
    return HttpResponse.json({
      items: matched.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      total: matched.length,
    })
  }),
  http.post(`${BASE}/clients`, async ({ request }) => {
    const body = (await request.json()) as ClientWrite
    if (!body.firstName || !body.lastName) {
      return problem(400, 'One or more validation errors occurred.', undefined, {
        firstName: body.firstName ? [] : ['First name is required.'],
        lastName: body.lastName ? [] : ['Last name is required.'],
      })
    }
    const created: ClientDetail = {
      ...body,
      id: newId('c'),
      fullName: [body.title, body.firstName, body.lastName].filter(Boolean).join(' '),
      age: new Date().getFullYear() - Number(body.dateOfBirth.slice(0, 4)),
      externalReference: { source: 'manual' },
      schemes: [],
      createdAtUtc: nowIso(),
      updatedAtUtc: nowIso(),
    }
    db.clients.unshift(created)
    return HttpResponse.json(created, { status: 201 })
  }),
  http.get(`${BASE}/clients/:id`, ({ params }) => {
    const client = db.clients.find((c) => c.id === params.id)
    return client ? HttpResponse.json(client) : problem(404, 'Not found', 'No such client.')
  }),
  http.put(`${BASE}/clients/:id`, async ({ params, request }) => {
    const index = db.clients.findIndex((c) => c.id === params.id)
    if (index < 0) return problem(404, 'Not found')
    const body = (await request.json()) as ClientWrite
    db.clients[index] = {
      ...db.clients[index],
      ...body,
      fullName: [body.title, body.firstName, body.lastName].filter(Boolean).join(' '),
      updatedAtUtc: nowIso(),
    }
    return HttpResponse.json(db.clients[index])
  }),
  http.delete(`${BASE}/clients/:id`, ({ params }) => {
    db.clients = db.clients.filter((c) => c.id !== params.id)
    return new HttpResponse(null, { status: 204 })
  }),

  // --- schemes -----------------------------------------------------------
  http.get(`${BASE}/clients/:id/schemes`, ({ params }) => {
    const client = db.clients.find((c) => c.id === params.id)
    return client ? HttpResponse.json(client.schemes) : problem(404, 'Not found')
  }),
  http.post(`${BASE}/clients/:id/schemes`, async ({ params, request }) => {
    const client = db.clients.find((c) => c.id === params.id)
    if (!client) return problem(404, 'Not found')
    const body = (await request.json()) as SchemeWrite
    const created: SchemeDto = {
      ...body,
      id: newId('s'),
      clientId: client.id,
      providerName: providers.find((p) => p.id === body.providerId)?.name,
      netTransferValue: body.transferValue,
      weightedOcfPct:
        body.holdings.reduce((s, h) => s + h.weightPct * (h.ocfPct ?? 0), 0) /
        Math.max(1, body.holdings.reduce((s, h) => s + h.weightPct, 0)),
      createdAtUtc: nowIso(),
      updatedAtUtc: nowIso(),
    }
    client.schemes.push(created)
    return HttpResponse.json(created, { status: 201 })
  }),
  http.put(`${BASE}/clients/:id/schemes/:schemeId`, async ({ params, request }) => {
    const client = db.clients.find((c) => c.id === params.id)
    const index = client?.schemes.findIndex((s) => s.id === params.schemeId) ?? -1
    if (!client || index < 0) return problem(404, 'Not found')
    const body = (await request.json()) as SchemeWrite
    client.schemes[index] = {
      ...client.schemes[index],
      ...body,
      providerName: providers.find((p) => p.id === body.providerId)?.name,
      netTransferValue: body.transferValue,
      updatedAtUtc: nowIso(),
    }
    return HttpResponse.json(client.schemes[index])
  }),
  http.delete(`${BASE}/clients/:id/schemes/:schemeId`, ({ params }) => {
    const client = db.clients.find((c) => c.id === params.id)
    if (client) client.schemes = client.schemes.filter((s) => s.id !== params.schemeId)
    return new HttpResponse(null, { status: 204 })
  }),
  http.get(`${BASE}/clients/:id/analyses`, ({ params }) =>
    HttpResponse.json(db.analyses.filter((a) => a.clientId === params.id)),
  ),
  http.get(`${BASE}/clients/:id/reports`, ({ params }) =>
    HttpResponse.json(db.reports.filter((r) => r.clientId === params.id)),
  ),

  // --- catalogue ---------------------------------------------------------
  http.get(`${BASE}/providers`, () => HttpResponse.json(providers)),
  http.get(`${BASE}/products`, ({ request }) => {
    const url = new URL(request.url)
    const wrapper = url.searchParams.get('wrapper')
    const search = (url.searchParams.get('search') ?? '').toLowerCase()
    const list = products
      .filter((p) => !wrapper || p.wrapperTypes.some((w) => w.toLowerCase() === wrapper.toLowerCase()))
      .filter((p) => !search || p.name.toLowerCase().includes(search) || p.providerName.toLowerCase().includes(search))
      .map(({ chargeVersions: _chargeVersions, ...summary }) => summary)
    return HttpResponse.json(list)
  }),
  http.get(`${BASE}/products/:id`, ({ params }) => {
    const product = products.find((p) => p.id === params.id)
    return product ? HttpResponse.json(product) : problem(404, 'Not found')
  }),
  http.get(`${BASE}/funds`, ({ request }) => {
    const url = new URL(request.url)
    const search = (url.searchParams.get('search') ?? '').toLowerCase()
    const sector = url.searchParams.get('sector')
    const maxOcf = url.searchParams.get('maxOcfPct')
    const page = Number(url.searchParams.get('page') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 25)
    const matched = funds
      .filter((f) => !search || f.name.toLowerCase().includes(search) || f.isin.toLowerCase().includes(search) || f.managerName.toLowerCase().includes(search))
      .filter((f) => !sector || f.iaSector === sector)
      .filter((f) => !maxOcf || f.ocfPct <= Number(maxOcf))
    return HttpResponse.json({
      items: matched.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      total: matched.length,
    })
  }),
  http.get(`${BASE}/funds/:isin`, ({ params }) => {
    const found = funds.find((f) => f.isin === params.isin)
    return found ? HttpResponse.json(found) : problem(404, 'Not found')
  }),
  http.get(`${BASE}/model-portfolios`, () => HttpResponse.json(modelPortfolios)),

  // --- assumption sets ---------------------------------------------------
  http.get(`${BASE}/assumption-sets`, () => HttpResponse.json(db.assumptionSets)),
  http.get(`${BASE}/assumption-sets/:id`, ({ params }) => {
    const found = db.assumptionSets.find((s) => s.id === params.id)
    return found ? HttpResponse.json(found) : problem(404, 'Not found')
  }),
  http.post(`${BASE}/assumption-sets/:id/copy`, async ({ params, request }) => {
    const source = db.assumptionSets.find((s) => s.id === params.id)
    if (!source) return problem(404, 'Not found')
    const { name } = (await request.json()) as { name: string }
    const copy: AssumptionSetDto = { ...source, id: newId('as'), firmId: FIRM_ID, isFcaStandard: false, version: 1, name }
    db.assumptionSets.push(copy)
    return HttpResponse.json(copy)
  }),
  http.put(`${BASE}/assumption-sets/:id`, async ({ params, request }) => {
    const index = db.assumptionSets.findIndex((s) => s.id === params.id)
    if (index < 0) return problem(404, 'Not found')
    if (db.assumptionSets[index].isFcaStandard) {
      return problem(400, 'FCA standard sets cannot be edited', 'Copy the set to your firm first.')
    }
    const body = (await request.json()) as AssumptionSetDto
    db.assumptionSets[index] = { ...db.assumptionSets[index], ...body, version: db.assumptionSets[index].version + 1 }
    return HttpResponse.json(db.assumptionSets[index])
  }),

  // --- stateless calculations -------------------------------------------
  http.post(`${BASE}/calculations/pension-switch`, async ({ request }) => {
    const body = (await request.json()) as PensionSwitchCalcRequest
    if (!body.proposedProductId) return problem(400, 'Bad request', 'A proposed product is required.')
    return HttpResponse.json(pensionSwitchResult(body))
  }),
  http.post(`${BASE}/calculations/db-transfer`, async ({ request }) => {
    const body = (await request.json()) as DbTransferCalcRequest
    if (!body.proposedProductId) return problem(400, 'Bad request', 'A proposed product is required.')
    return HttpResponse.json(dbTransferResult(body))
  }),
  http.post(`${BASE}/calculations/cashflow`, async ({ request }) => {
    const body = (await request.json()) as CashflowCalcRequest
    return HttpResponse.json(cashflowResult(body))
  }),
  http.post(`${BASE}/calculations/cashflow/stochastic`, async ({ request }) => {
    const body = (await request.json()) as StochasticCalcRequest
    return HttpResponse.json(stochasticResult(body))
  }),

  // --- persisted analyses -----------------------------------------------
  http.post(`${BASE}/analyses/pension-switch`, async ({ request }) => {
    const body = (await request.json()) as PensionSwitchAnalysisWrite
    const dto: PensionSwitchAnalysisDto = { ...body, ...envelope(), id: newId('an-ps') }
    db.pensionSwitch.set(dto.id, dto)
    upsertSummary({
      id: dto.id,
      clientId: dto.clientId,
      kind: 'pensionSwitch',
      title: dto.title,
      status: dto.status,
      version: dto.version,
      updatedAtUtc: dto.updatedAtUtc,
      createdBy: dto.createdBy,
    })
    return HttpResponse.json(dto, { status: 201 })
  }),
  http.get(`${BASE}/analyses/pension-switch/:id`, ({ params }) => {
    const dto = db.pensionSwitch.get(String(params.id))
    return dto ? HttpResponse.json(dto) : problem(404, 'Not found')
  }),
  http.put(`${BASE}/analyses/pension-switch/:id`, async ({ params, request }) => {
    const existing = db.pensionSwitch.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    if (existing.status === 'locked') return problem(409, 'Conflict', 'A locked analysis cannot be edited.')
    const body = (await request.json()) as PensionSwitchAnalysisWrite
    const dto = { ...existing, ...body, updatedAtUtc: nowIso() }
    db.pensionSwitch.set(dto.id, dto)
    return HttpResponse.json(dto)
  }),
  http.post(`${BASE}/analyses/pension-switch/:id/calculate`, ({ params }) => {
    const existing = db.pensionSwitch.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    const result = pensionSwitchResult({
      clientId: existing.clientId,
      cedingSchemes: existing.cedingSchemeIds.map((schemeId) => ({ schemeId })),
      proposedProductId: existing.proposedProductId ?? products[0].id,
      proposedHoldings: existing.proposedHoldings,
      proposedAdviserCharges: existing.proposedAdviserCharges,
      retirementAge: existing.retirementAge,
      assumptionSetId: existing.assumptionSetId,
      overrides: existing.overrides,
      redirectContributions: true,
    })
    const dto: PensionSwitchAnalysisDto = {
      ...existing,
      status: 'calculated',
      version: existing.version + 1,
      resultHash: 'mock-hash',
      calculatedAtUtc: nowIso(),
      engineVersion: result.engineVersion,
      result,
      updatedAtUtc: nowIso(),
    }
    db.pensionSwitch.set(dto.id, dto)
    upsertSummary({
      id: dto.id,
      clientId: dto.clientId,
      kind: 'pensionSwitch',
      title: dto.title,
      status: dto.status,
      version: dto.version,
      calculatedAtUtc: dto.calculatedAtUtc,
      updatedAtUtc: dto.updatedAtUtc,
      createdBy: dto.createdBy,
    })
    return HttpResponse.json(dto)
  }),
  http.post(`${BASE}/analyses/pension-switch/:id/lock`, ({ params }) => {
    const existing = db.pensionSwitch.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    const dto = { ...existing, status: 'locked' as const, updatedAtUtc: nowIso() }
    db.pensionSwitch.set(dto.id, dto)
    return HttpResponse.json(dto)
  }),
  http.delete(`${BASE}/analyses/pension-switch/:id`, ({ params }) => {
    db.pensionSwitch.delete(String(params.id))
    db.analyses = db.analyses.filter((a) => a.id !== params.id)
    return new HttpResponse(null, { status: 204 })
  }),

  http.post(`${BASE}/analyses/db-transfer`, async ({ request }) => {
    const body = (await request.json()) as DbTransferAnalysisWrite
    const dto: DbTransferAnalysisDto = { ...body, ...envelope(), id: newId('an-db') }
    db.dbTransfer.set(dto.id, dto)
    upsertSummary({
      id: dto.id,
      clientId: dto.clientId,
      kind: 'dbTransfer',
      title: `DB transfer — ${dto.dbSchemeId}`,
      status: dto.status,
      version: dto.version,
      updatedAtUtc: dto.updatedAtUtc,
      createdBy: dto.createdBy,
    })
    return HttpResponse.json(dto, { status: 201 })
  }),
  http.get(`${BASE}/analyses/db-transfer/:id`, ({ params }) => {
    const dto = db.dbTransfer.get(String(params.id))
    return dto ? HttpResponse.json(dto) : problem(404, 'Not found')
  }),
  http.put(`${BASE}/analyses/db-transfer/:id`, async ({ params, request }) => {
    const existing = db.dbTransfer.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    const body = (await request.json()) as DbTransferAnalysisWrite
    const dto = { ...existing, ...body, updatedAtUtc: nowIso() }
    db.dbTransfer.set(dto.id, dto)
    return HttpResponse.json(dto)
  }),
  http.post(`${BASE}/analyses/db-transfer/:id/calculate`, ({ params }) => {
    const existing = db.dbTransfer.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    const result = dbTransferResult({
      clientId: existing.clientId,
      dbSchemeId: existing.dbSchemeId,
      proposedProductId: existing.proposedProductId ?? products[0].id,
      proposedHoldings: existing.proposedHoldings,
      proposedAdviserCharges: existing.proposedAdviserCharges,
      aptaGrowthPct: existing.aptaGrowthPct ?? 5,
      planEndAge: existing.planEndAge,
      initialAdviceFee: existing.initialAdviceFee,
      assumptionSetId: existing.assumptionSetId,
    })
    const dto: DbTransferAnalysisDto = {
      ...existing,
      status: 'calculated',
      version: existing.version + 1,
      resultHash: 'mock-hash',
      calculatedAtUtc: nowIso(),
      engineVersion: result.engineVersion,
      result,
      updatedAtUtc: nowIso(),
    }
    db.dbTransfer.set(dto.id, dto)
    return HttpResponse.json(dto)
  }),
  http.post(`${BASE}/analyses/db-transfer/:id/lock`, ({ params }) => {
    const existing = db.dbTransfer.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    const dto = { ...existing, status: 'locked' as const, updatedAtUtc: nowIso() }
    db.dbTransfer.set(dto.id, dto)
    return HttpResponse.json(dto)
  }),
  http.delete(`${BASE}/analyses/db-transfer/:id`, ({ params }) => {
    db.dbTransfer.delete(String(params.id))
    return new HttpResponse(null, { status: 204 })
  }),

  http.post(`${BASE}/analyses/cashflow`, async ({ request }) => {
    const body = (await request.json()) as CashflowPlanWrite
    const dto: CashflowPlanDto = { ...body, ...envelope(), id: newId('an-cf') }
    db.cashflow.set(dto.id, dto)
    upsertSummary({
      id: dto.id,
      clientId: dto.clientId,
      kind: 'cashflow',
      title: dto.title,
      status: dto.status,
      version: dto.version,
      updatedAtUtc: dto.updatedAtUtc,
      createdBy: dto.createdBy,
    })
    return HttpResponse.json(dto, { status: 201 })
  }),
  http.get(`${BASE}/analyses/cashflow/:id`, ({ params }) => {
    const dto = db.cashflow.get(String(params.id))
    return dto ? HttpResponse.json(dto) : problem(404, 'Not found')
  }),
  http.put(`${BASE}/analyses/cashflow/:id`, async ({ params, request }) => {
    const existing = db.cashflow.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    const body = (await request.json()) as CashflowPlanWrite
    const dto = { ...existing, ...body, updatedAtUtc: nowIso() }
    db.cashflow.set(dto.id, dto)
    return HttpResponse.json(dto)
  }),
  http.post(`${BASE}/analyses/cashflow/:id/calculate`, ({ params }) => {
    const existing = db.cashflow.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    const dto: CashflowPlanDto = {
      ...existing,
      status: 'calculated',
      version: existing.version + 1,
      calculatedAtUtc: nowIso(),
      updatedAtUtc: nowIso(),
    }
    db.cashflow.set(dto.id, dto)
    return HttpResponse.json(dto)
  }),
  http.post(`${BASE}/analyses/cashflow/:id/calculate/stochastic`, ({ params }) => {
    const existing = db.cashflow.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    return HttpResponse.json(existing)
  }),
  http.post(`${BASE}/analyses/cashflow/:id/lock`, ({ params }) => {
    const existing = db.cashflow.get(String(params.id))
    if (!existing) return problem(404, 'Not found')
    const dto = { ...existing, status: 'locked' as const, updatedAtUtc: nowIso() }
    db.cashflow.set(dto.id, dto)
    return HttpResponse.json(dto)
  }),
  http.delete(`${BASE}/analyses/cashflow/:id`, ({ params }) => {
    db.cashflow.delete(String(params.id))
    return new HttpResponse(null, { status: 204 })
  }),

  // --- reports -----------------------------------------------------------
  http.get(`${BASE}/reports`, () => HttpResponse.json(db.reports)),
  http.post(`${BASE}/reports`, async ({ request }) => {
    const body = (await request.json()) as ReportCreateRequest
    const summary = db.analyses.find((a) => a.id === body.analysisId)
    if (!summary) return problem(404, 'Not found', 'No such analysis.')
    if (body.format !== 'json') {
      return problem(500, 'An unexpected error occurred.', `${body.format.toUpperCase()} rendering is not implemented.`)
    }
    const report: ReportDto = {
      id: newId('rep'),
      clientId: summary.clientId,
      analysisId: body.analysisId,
      analysisVersion: summary.version,
      analysisResultHash: 'mock-hash',
      kind: body.kind,
      format: body.format,
      templateVersion: '2026.09.1',
      generatedBy: users.adviser.id,
      sha256: 'mock-sha256',
      sizeBytes: 16_384,
      generatedAtUtc: nowIso(),
      downloadUrl: '',
    }
    report.downloadUrl = `${BASE}/reports/${report.id}/download`
    db.reports.unshift(report)
    upsertSummary({ ...summary, status: 'locked' })
    return HttpResponse.json(report, { status: 201 })
  }),
  http.get(`${BASE}/reports/:id`, ({ params }) => {
    const found = db.reports.find((r) => r.id === params.id)
    return found ? HttpResponse.json(found) : problem(404, 'Not found')
  }),
  http.get(`${BASE}/reports/:id/download`, ({ params }) => {
    const found = db.reports.find((r) => r.id === params.id)
    if (!found) return problem(404, 'Not found')
    return HttpResponse.json(
      { report: found, note: 'Sample report body served by the mock server.' },
      {
        headers: {
          'content-disposition': `attachment; filename=${found.kind}-${found.analysisId}-v${found.analysisVersion}.json`,
        },
      },
    )
  }),

  // --- audit and integrations -------------------------------------------
  http.get(`${BASE}/audit`, ({ request }) => {
    const url = new URL(request.url)
    const entityId = url.searchParams.get('entityId')
    const entityType = url.searchParams.get('entityType')
    const page = Number(url.searchParams.get('page') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 25)
    const matched = auditEvents
      .filter((e) => !entityId || e.entityId === entityId)
      .filter((e) => !entityType || e.entityType === entityType)
    return HttpResponse.json({
      items: matched.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      total: matched.length,
    })
  }),
  http.get(`${BASE}/audit/verify`, () =>
    HttpResponse.json({ isValid: true, firstBrokenIndex: -1, reason: null, eventsChecked: auditEvents.length }),
  ),
  http.get(`${BASE}/integrations`, () =>
    HttpResponse.json(
      (['Intelliflo', 'Xplan', 'TruePotential', 'OrigoHub', 'Morningstar'] as const).map((connector) => ({
        connector,
        configured: true,
        mode: 'Sandbox' as const,
        lastSyncUtc: connector === 'Morningstar' ? '2026-09-01T02:00:00Z' : undefined,
      })),
    ),
  ),
  http.post(`${BASE}/integrations/:connector/import`, () =>
    HttpResponse.json({ imported: 2, updated: 1, skipped: 0, messages: ['Imported 2 new clients from the sandbox.'] }),
  ),
  http.post(`${BASE}/integrations/morningstar/sync`, () =>
    HttpResponse.json({ fundsUpdated: funds.length, messages: ['Prices and statistics refreshed.'] }),
  ),
]
