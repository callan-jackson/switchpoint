import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { Check, FileText, Lock, Save } from 'lucide-react'
import { calculations, reports as reportsApi } from '@/api/endpoints'
import { errorMessage } from '@/api/client'
import {
  useAssumptionSets,
  useClient,
  useModelPortfolios,
  usePensionSwitchAnalysis,
  usePensionSwitchMutations,
  useProducts,
  useCreateReport,
} from '@/api/queries'
import type {
  AdviserChargesDto,
  AssumptionOverrides,
  HoldingDto,
  PensionSwitchAnalysisWrite,
  PensionSwitchCalcRequest,
  PensionSwitchResultDto,
  SchemeDto,
} from '@/api/types'
import { useLivePreview } from '@/lib/hooks'
import { totalWeightPct } from '@/lib/charges'
import { reportFilename, saveBlob } from '@/lib/files'
import { fmtPct, gbp } from '@/lib/format'
import { DC_SCHEME_TYPES, schemeTypeLabels } from '@/lib/labels'
import { Alert, WarningsAlert } from '@/components/ui/Alert'
import { Badge, StatusBadge, VerdictBadge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { StatTile } from '@/components/ui/StatTile'
import { Tabs } from '@/components/ui/Tabs'
import { useToast } from '@/components/ui/Toast'
import { Field } from '@/components/form/Field'
import { Checkbox, Input, Select } from '@/components/form/Input'
import { IntegerInput } from '@/components/form/NumericInput'
import { ChartFrame } from '@/components/charts/ChartFrame'
import { ExistingVsProposedChart } from '@/components/charts/Charts'
import { AdviserChargeFields, AssumptionFields, ClientPicker, PreviewStatus, StaleWrapper } from '@/components/domain/shared'
import { HoldingsEditor } from '@/components/domain/HoldingsEditor'
import { ChargeBreakdownTable, RiyTable } from '@/components/domain/RiyTable'

interface SwitchState {
  clientId?: string
  title: string
  cedingSchemeIds: string[]
  proposedProductId?: string
  proposedModelPortfolioId?: string
  proposedHoldings: HoldingDto[]
  proposedAdviserCharges: AdviserChargesDto
  retirementAge: number
  assumptionSetId?: string
  overrides: AssumptionOverrides
  redirectContributions: boolean
  rationale: string
}

const initialState: SwitchState = {
  title: '',
  cedingSchemeIds: [],
  proposedHoldings: [],
  proposedAdviserCharges: { initialPct: 2, initialAmount: 0, ongoingPct: 0.75, ongoingAmount: 0 },
  retirementAge: 67,
  overrides: {},
  redirectContributions: true,
  rationale: '',
}

/**
 * Pension switch wizard.
 *
 * The four steps write into one state object, which is also the source of the live preview
 * request. `useLivePreview` debounces that request and aborts a superseded one, so typing in a
 * charge field never leaves a stale answer on screen or a pile of in-flight calls behind it.
 * Saving, calculating and locking are separate, explicit actions: the preview is never persisted.
 */
export default function PensionSwitchPage() {
  const { analysisId } = useParams()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const toast = useToast()

  const [state, setState] = useState<SwitchState>(() => ({
    ...initialState,
    clientId: params.get('clientId') ?? undefined,
  }))
  const [savedId, setSavedId] = useState<string | undefined>(analysisId)

  const { data: client } = useClient(state.clientId)
  const { data: products } = useProducts({ wrapper: 'Sipp' })
  const { data: modelPortfolios } = useModelPortfolios()
  const { data: assumptionSets } = useAssumptionSets()
  const { data: analysis } = usePensionSwitchAnalysis(analysisId)
  const mutations = usePensionSwitchMutations()
  const createReport = useCreateReport()

  // Default the assumption set to the FCA standard one once the list arrives.
  useEffect(() => {
    if (state.assumptionSetId || !assumptionSets?.length) return
    const fca = assumptionSets.find((s) => s.isFcaStandard) ?? assumptionSets[0]
    setState((s) => ({ ...s, assumptionSetId: fca.id }))
  }, [assumptionSets, state.assumptionSetId])

  // Hydrate from a saved analysis when one was opened by id.
  useEffect(() => {
    if (!analysis) return
    setSavedId(analysis.id)
    setState({
      clientId: analysis.clientId,
      title: analysis.title,
      cedingSchemeIds: analysis.cedingSchemeIds,
      proposedProductId: analysis.proposedProductId,
      proposedModelPortfolioId: analysis.proposedModelPortfolioId,
      proposedHoldings: analysis.proposedHoldings,
      proposedAdviserCharges: analysis.proposedAdviserCharges,
      retirementAge: analysis.retirementAge,
      assumptionSetId: analysis.assumptionSetId,
      overrides: analysis.overrides ?? {},
      redirectContributions: true,
      rationale: analysis.rationale ?? '',
    })
  }, [analysis])

  useEffect(() => {
    if (client && !state.title) {
      setState((s) => ({ ...s, title: `${client.fullName} — pension switch`, retirementAge: client.targetRetirementAge }))
    }
  }, [client, state.title])

  const cedingCandidates: SchemeDto[] = useMemo(
    () => (client?.schemes ?? []).filter((s) => DC_SCHEME_TYPES.includes(s.type)),
    [client],
  )
  const selectedSchemes = cedingCandidates.filter((s) => state.cedingSchemeIds.includes(s.id))
  const totalTransfer = selectedSchemes.reduce((sum, s) => sum + s.transferValue, 0)

  const usingModelPortfolio = !!state.proposedModelPortfolioId
  const weightsOk = usingModelPortfolio || totalWeightPct(state.proposedHoldings) === 100
  const locked = analysis?.status === 'locked'

  const readyToCalculate =
    !!state.clientId &&
    state.cedingSchemeIds.length > 0 &&
    !!state.proposedProductId &&
    weightsOk &&
    (usingModelPortfolio || state.proposedHoldings.length > 0)

  const previewRequest: PensionSwitchCalcRequest | null = readyToCalculate
    ? {
        clientId: state.clientId,
        cedingSchemes: state.cedingSchemeIds.map((schemeId) => ({ schemeId })),
        proposedProductId: state.proposedProductId!,
        proposedHoldings: state.proposedHoldings,
        proposedModelPortfolioId: state.proposedModelPortfolioId,
        proposedAdviserCharges: state.proposedAdviserCharges,
        retirementAge: state.retirementAge,
        assumptionSetId: state.assumptionSetId,
        overrides: Object.values(state.overrides).some((v) => v !== undefined) ? state.overrides : undefined,
        redirectContributions: state.redirectContributions,
      }
    : null

  const preview = useLivePreview<PensionSwitchCalcRequest, PensionSwitchResultDto>(
    previewRequest,
    (body, signal) => calculations.pensionSwitch(body, signal),
  )

  const result = analysis?.result ?? preview.data

  const write = (): PensionSwitchAnalysisWrite => ({
    clientId: state.clientId!,
    title: state.title || 'Pension switch analysis',
    retirementAge: state.retirementAge,
    cedingSchemeIds: state.cedingSchemeIds,
    proposedProductId: state.proposedProductId,
    proposedHoldings: state.proposedHoldings,
    proposedModelPortfolioId: state.proposedModelPortfolioId,
    proposedAdviserCharges: state.proposedAdviserCharges,
    assumptionSetId: state.assumptionSetId!,
    overrides: Object.values(state.overrides).some((v) => v !== undefined) ? state.overrides : undefined,
    rationale: state.rationale || undefined,
  })

  const save = async () => {
    try {
      const dto = savedId
        ? await mutations.update.mutateAsync({ id: savedId, body: write() })
        : await mutations.create.mutateAsync(write())
      setSavedId(dto.id)
      if (!analysisId) navigate(`/pension-switch/${dto.id}`, { replace: true })
      toast.success('Analysis saved', dto.title)
    } catch (e) {
      toast.error('Could not save the analysis', errorMessage(e))
    }
  }

  const calculate = async () => {
    if (!savedId) return save().then(() => undefined)
    try {
      const dto = await mutations.calculate.mutateAsync(savedId)
      toast.success('Calculated', `Version ${dto.version}`)
    } catch (e) {
      toast.error('Calculation failed', errorMessage(e))
    }
  }

  const lock = async () => {
    if (!savedId) return
    try {
      await mutations.lock.mutateAsync(savedId)
      toast.success('Analysis locked', 'The figures are now fixed for reporting.')
    } catch (e) {
      toast.error('Could not lock', errorMessage(e))
    }
  }

  const createAndDownloadReport = async () => {
    if (!savedId) return
    try {
      const report = await createReport.mutateAsync({ analysisId: savedId, kind: 'pensionSwitch', format: 'json' })
      const blob = await reportsApi.download(report.id)
      saveBlob(blob, reportFilename(report))
      toast.success('Report created', 'The analysis is now locked.')
    } catch (e) {
      toast.error('Report failed', errorMessage(e))
    }
  }

  return (
    <>
      <PageHeader
        eyebrow="Pension switch"
        title={state.title || 'New pension switch analysis'}
        description="Compare the client's existing arrangements with a proposed product: critical yield, headroom and the COBS 13 charge tables."
        meta={
          <>
            {analysis && <StatusBadge status={analysis.status} />}
            {analysis && <Badge tone="neutral">Version {analysis.version}</Badge>}
            {result?.anyGuaranteesFlagged && <Badge tone="warning">Guarantees flagged</Badge>}
          </>
        }
        actions={
          <>
            <Button variant="outline" icon={<Save />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending} disabled={!state.clientId || !state.assumptionSetId || locked}>
              Save
            </Button>
            <Button variant="outline" icon={<Check />} onClick={calculate} loading={mutations.calculate.isPending} disabled={!savedId || locked}>
              Calculate
            </Button>
            <Button variant="outline" icon={<Lock />} onClick={lock} loading={mutations.lock.isPending} disabled={!savedId || locked}>
              Lock
            </Button>
            <Button icon={<FileText />} onClick={createAndDownloadReport} loading={createReport.isPending} disabled={!savedId}>
              Create report
            </Button>
          </>
        }
      />

      {locked && (
        <Alert tone="info" title="This analysis is locked" className="mb-4">
          The inputs and results are fixed. Copy it to a new analysis to explore a different option.
        </Alert>
      )}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.05fr)]">
        {/* ----------------------------------------------------------------- inputs */}
        <div className="space-y-4">
          <Card title="1. Client and ceding arrangements">
            <div className="space-y-3">
              <ClientPicker
                value={state.clientId}
                disabled={locked || !!analysisId}
                onChange={(clientId) =>
                  setState((s) => ({ ...s, clientId, cedingSchemeIds: [], title: '' }))
                }
              />
              <Field label="Analysis title" required>
                <Input
                  value={state.title}
                  disabled={locked}
                  onChange={(e) => setState((s) => ({ ...s, title: e.target.value }))}
                />
              </Field>

              {client && cedingCandidates.length === 0 && (
                <Alert tone="warning" title="No money purchase arrangements">
                  This client has no DC arrangements that can be switched. Add one on the client's Schemes tab.
                </Alert>
              )}

              {cedingCandidates.length > 0 && (
                <fieldset>
                  <legend className="text-xs font-medium text-fg-muted">Ceding arrangements</legend>
                  <ul className="mt-1.5 space-y-2">
                    {cedingCandidates.map((scheme) => {
                      const flagged =
                        scheme.guarantees.guaranteedAnnuityRatePct != null ||
                        scheme.guarantees.withProfits ||
                        scheme.guarantees.protectedTaxFreeCashPct != null
                      return (
                        <li key={scheme.id} className="rounded-lg border border-border p-3">
                          <Checkbox
                            disabled={locked}
                            checked={state.cedingSchemeIds.includes(scheme.id)}
                            onChange={(e) =>
                              setState((s) => ({
                                ...s,
                                cedingSchemeIds: e.target.checked
                                  ? [...s.cedingSchemeIds, scheme.id]
                                  : s.cedingSchemeIds.filter((id) => id !== scheme.id),
                              }))
                            }
                            label={
                              <span className="flex flex-wrap items-center gap-2">
                                <span className="font-medium">{scheme.productName}</span>
                                <span className="text-xs text-fg-muted">
                                  {schemeTypeLabels[scheme.type]} · {gbp(scheme.transferValue)}
                                </span>
                                {flagged && (
                                  <Badge tone="warning" size="sm">
                                    Guarantees
                                  </Badge>
                                )}
                              </span>
                            }
                            hint={
                              flagged
                                ? [
                                    scheme.guarantees.guaranteedAnnuityRatePct != null &&
                                      `GAR ${fmtPct(scheme.guarantees.guaranteedAnnuityRatePct, 2)}`,
                                    scheme.guarantees.withProfits && 'With-profits fund',
                                    scheme.guarantees.protectedTaxFreeCashPct != null &&
                                      `Protected TFC ${fmtPct(scheme.guarantees.protectedTaxFreeCashPct, 0)}`,
                                  ]
                                    .filter(Boolean)
                                    .join(' · ')
                                : undefined
                            }
                          />
                        </li>
                      )
                    })}
                  </ul>
                  {selectedSchemes.length > 0 && (
                    <p className="mt-2 text-xs text-fg-muted">
                      Total transfer value <span className="font-semibold text-fg">{gbp(totalTransfer)}</span>
                    </p>
                  )}
                </fieldset>
              )}
            </div>
          </Card>

          <Card title="2. Proposed product and investments">
            <div className="space-y-3">
              <Field label="Proposed product" required>
                <Select
                  value={state.proposedProductId ?? ''}
                  disabled={locked}
                  placeholder="Choose a product"
                  onChange={(e) => setState((s) => ({ ...s, proposedProductId: e.target.value || undefined }))}
                  options={(products ?? []).map((p) => ({
                    value: p.id,
                    label: `${p.providerName} — ${p.name} (${fmtPct(p.effectiveChargePctAt100k, 2)} at £100k)`,
                  }))}
                />
              </Field>

              <Field
                label="Model portfolio"
                hint="Choosing a model portfolio replaces the individual holdings below."
              >
                <Select
                  value={state.proposedModelPortfolioId ?? ''}
                  disabled={locked}
                  placeholder="Use individual holdings"
                  onChange={(e) =>
                    setState((s) => ({ ...s, proposedModelPortfolioId: e.target.value || undefined }))
                  }
                  options={(modelPortfolios ?? []).map((m) => ({
                    value: m.id,
                    label: `${m.providerName} — ${m.name} (OCF ${fmtPct(m.blendedOcfPct, 2)})`,
                  }))}
                />
              </Field>

              {!usingModelPortfolio && (
                <HoldingsEditor
                  holdings={state.proposedHoldings}
                  disabled={locked}
                  onChange={(proposedHoldings) => setState((s) => ({ ...s, proposedHoldings }))}
                />
              )}
            </div>
          </Card>

          <Card title="3. Charges, retirement age and assumptions">
            <div className="space-y-4">
              <AdviserChargeFields
                value={state.proposedAdviserCharges}
                disabled={locked}
                transferValue={totalTransfer}
                onChange={(proposedAdviserCharges) => setState((s) => ({ ...s, proposedAdviserCharges }))}
              />
              <div className="grid gap-3 sm:grid-cols-2">
                <Field label="Retirement age" required>
                  <IntegerInput
                    value={state.retirementAge}
                    disabled={locked}
                    onChange={(v) => setState((s) => ({ ...s, retirementAge: v ?? 67 }))}
                  />
                </Field>
                <div className="flex items-end">
                  <Checkbox
                    label="Redirect contributions"
                    hint="Ceding contributions continue into the new product."
                    disabled={locked}
                    checked={state.redirectContributions}
                    onChange={(e) => setState((s) => ({ ...s, redirectContributions: e.target.checked }))}
                  />
                </div>
              </div>
              <AssumptionFields
                sets={assumptionSets}
                assumptionSetId={state.assumptionSetId}
                onAssumptionSetChange={(assumptionSetId) => setState((s) => ({ ...s, assumptionSetId }))}
                overrides={state.overrides}
                onOverridesChange={(overrides) => setState((s) => ({ ...s, overrides }))}
                fields={['growthLowerPct', 'growthIntermediatePct', 'growthHigherPct', 'inflationPct']}
                disabled={locked}
              />
              <Field label="Rationale" hint="Recorded on the analysis and carried into the suitability report.">
                <Input
                  value={state.rationale}
                  disabled={locked}
                  onChange={(e) => setState((s) => ({ ...s, rationale: e.target.value }))}
                  placeholder="Why this switch is being recommended"
                />
              </Field>
            </div>
          </Card>
        </div>

        {/* ---------------------------------------------------------------- preview */}
        <div className="space-y-4">
          <Card title="Live preview" description="Recalculated as you type; never persisted until you press Calculate.">
            <PreviewStatus
              isPending={preview.isPending}
              isFetching={preview.isFetching}
              error={preview.error}
              onRefresh={preview.refresh}
              calculatedAtUtc={result?.calculatedAtUtc}
              engineVersion={result?.engineVersion}
            />

            {!readyToCalculate && (
              <Alert tone="info" title="Not enough detail yet" className="mt-3">
                Choose a client, at least one ceding arrangement, a proposed product and holdings that total 100%.
              </Alert>
            )}

            {result && (
              <StaleWrapper stale={preview.isFetching}>
                <div className="mt-4 grid gap-3 sm:grid-cols-3">
                  <StatTile label="Transfer value" value={gbp(result.totalNetTransferValue)} />
                  <StatTile label="Initial adviser charge" value={gbp(result.initialAdviserCharge)} tone="warning" />
                  <StatTile
                    label="Headroom (intermediate)"
                    value={fmtPct(result.intermediate.headroomPct, 2, true)}
                    tone={result.intermediate.headroomPct >= 0 ? 'success' : 'danger'}
                    hint={`Critical yield ${fmtPct(result.intermediate.criticalYieldPct)}`}
                  />
                </div>

                <WarningsAlert warnings={result.warnings} title="Engine warnings" />

                <div className="mt-4 overflow-x-auto">
                  <table className="table">
                    <caption className="px-3 py-2 text-left text-sm text-fg-muted">
                      Critical yield at the three projection rates
                    </caption>
                    <thead>
                      <tr>
                        <th scope="col">Growth rate</th>
                        <th scope="col" className="text-right">
                          Existing at retirement
                        </th>
                        <th scope="col" className="text-right">
                          Proposed at retirement
                        </th>
                        <th scope="col" className="text-right">
                          Critical yield
                        </th>
                        <th scope="col" className="text-right">
                          Headroom
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {(
                        [
                          ['Lower', result.lower],
                          ['Intermediate', result.intermediate],
                          ['Higher', result.higher],
                        ] as const
                      ).map(([label, row]) => (
                        <tr key={label}>
                          <th scope="row" className="font-normal">
                            {label} ({fmtPct(row.growthPct, 1)})
                          </th>
                          <td className="num">{gbp(row.existingValueAtRetirement)}</td>
                          <td className="num">{gbp(row.receivingValueAtRetirement)}</td>
                          <td className="num">{fmtPct(row.criticalYieldPct)}</td>
                          <td className={`num font-semibold ${row.headroomPct >= 0 ? 'text-success-600' : 'text-danger-600'}`}>
                            {fmtPct(row.headroomPct, 2, true)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="mt-6">
                  <ChartFrame
                    title="Existing arrangements against the proposed product"
                    description="Projected fund value at the intermediate rate, in today's money, from now to retirement."
                    height={300}
                    table={
                      <table className="table table-dense">
                        <caption className="sr-only">Projected fund values by year</caption>
                        <thead>
                          <tr>
                            <th scope="col" className="text-right">
                              Year
                            </th>
                            <th scope="col" className="text-right">
                              Existing (£)
                            </th>
                            <th scope="col" className="text-right">
                              Proposed (£)
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {result.chart.map((point) => (
                            <tr key={point.year}>
                              <th scope="row" className="num font-normal">
                                {point.year}
                              </th>
                              <td className="num">{gbp(point.existingValue)}</td>
                              <td className="num">{gbp(point.receivingValue)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    }
                  >
                    <ExistingVsProposedChart data={result.chart} />
                  </ChartFrame>
                </div>
              </StaleWrapper>
            )}
          </Card>

          {result && (
            <Card title="Per-arrangement verdicts" flush>
              <div className="overflow-x-auto">
                <table className="table">
                  <caption className="px-3 py-2 text-left text-sm text-fg-muted">
                    FSA 2009 switching template outcome for each ceding arrangement
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">Arrangement</th>
                      <th scope="col" className="text-right">
                        Transfer value
                      </th>
                      <th scope="col" className="text-right">
                        Critical yield alone
                      </th>
                      <th scope="col" className="text-right">
                        RIY retained
                      </th>
                      <th scope="col" className="text-right">
                        RIY switched
                      </th>
                      <th scope="col">Verdict</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.schemes.map((scheme) => (
                      <tr key={scheme.name}>
                        <th scope="row" className="font-normal">
                          {scheme.name}
                          {scheme.guaranteesFlagged && (
                            <Badge tone="warning" size="sm" className="ml-2">
                              Guarantees
                            </Badge>
                          )}
                        </th>
                        <td className="num">{gbp(scheme.netTransferValue)}</td>
                        <td className="num">{fmtPct(scheme.criticalYieldAlonePct)}</td>
                        <td className="num">{fmtPct(scheme.riyIfRetained.totalRiyPct)}</td>
                        <td className="num">{fmtPct(scheme.riyIfSwitched.totalRiyPct)}</td>
                        <td>
                          <VerdictBadge verdict={scheme.verdict} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          )}

          {result && <RiyPanels result={result} />}
        </div>
      </div>
    </>
  )
}

function RiyPanels({ result }: { result: PensionSwitchResultDto }) {
  const [tab, setTab] = useState<'effect' | 'breakdown'>('effect')
  return (
    <Card title="Effect of charges — proposed product">
      <Tabs
        aria-label="Charge tables"
        variant="pills"
        value={tab}
        onChange={setTab}
        items={[
          { id: 'effect', label: 'Effect of charges' },
          { id: 'breakdown', label: 'Where the charges go' },
        ]}
      >
        {tab === 'effect' ? (
          <RiyTable riy={result.receivingRiy} caption="Proposed product, at the intermediate growth rate" />
        ) : (
          <ChargeBreakdownTable riy={result.receivingRiy} />
        )}
      </Tabs>
      <p className="mt-3 text-xs text-fg-subtle">
        Assumption set: {result.assumptionSet.name} (version {result.assumptionSet.version}) · engine{' '}
        {result.engineVersion}
      </p>
    </Card>
  )
}
