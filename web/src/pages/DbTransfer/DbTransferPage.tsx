import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { Check, FileText, Lock, Save } from 'lucide-react'
import { calculations, reports as reportsApi } from '@/api/endpoints'
import { errorMessage } from '@/api/client'
import {
  useAssumptionSets,
  useClient,
  useCreateReport,
  useDbTransferAnalysis,
  useDbTransferMutations,
  useProducts,
} from '@/api/queries'
import type {
  AdviserChargesDto,
  AssumptionOverrides,
  ChargeBasis,
  DbTransferAnalysisWrite,
  DbTransferCalcRequest,
  DbTransferResultDto,
  HoldingDto,
} from '@/api/types'
import { totalWeightPct } from '@/lib/charges'
import { reportFilename, saveBlob } from '@/lib/files'
import { date, fmtPct, gbp, months, num } from '@/lib/format'
import { chargeBasisLabels, optionsFrom } from '@/lib/labels'
import { useLivePreview } from '@/lib/hooks'
import { Alert, WarningsAlert } from '@/components/ui/Alert'
import { Badge, StatusBadge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DefinitionList } from '@/components/ui/DefinitionList'
import { PageHeader } from '@/components/ui/PageHeader'
import { StatTile } from '@/components/ui/StatTile'
import { useToast } from '@/components/ui/Toast'
import { Field } from '@/components/form/Field'
import { Input, Select } from '@/components/form/Input'
import { IntegerInput, MoneyInput, PercentInput } from '@/components/form/NumericInput'
import { ChartFrame } from '@/components/charts/ChartFrame'
import { IncomeComparisonChart, TvcBarChart } from '@/components/charts/Charts'
import { AdviserChargeFields, AssumptionFields, ClientPicker, PreviewStatus, StaleWrapper } from '@/components/domain/shared'
import { HoldingsEditor } from '@/components/domain/HoldingsEditor'

interface DbState {
  clientId?: string
  dbSchemeId?: string
  transferDate: string
  planEndAge: number
  proposedProductId?: string
  proposedHoldings: HoldingDto[]
  proposedAdviserCharges: AdviserChargesDto
  aptaGrowthPct: number
  initialAdviceFee: number
  workplaceDefaultChargePct?: number
  chargeBasis: ChargeBasis
  contingentChargingCarveOut: string
  assumptionSetId?: string
  overrides: AssumptionOverrides
}

const initialState: DbState = {
  transferDate: new Date().toISOString().slice(0, 10),
  planEndAge: 95,
  proposedHoldings: [],
  proposedAdviserCharges: { initialPct: 0, initialAmount: 0, ongoingPct: 0.75, ongoingAmount: 0 },
  aptaGrowthPct: 5,
  initialAdviceFee: 5_000,
  chargeBasis: 'nonContingent',
  contingentChargingCarveOut: '',
  overrides: {},
}

/**
 * DB transfer analysis.
 *
 * The Transfer Value Comparator panel follows the COBS 19 Annex 5 layout: the regulator's
 * wording, a two-bar chart whose value axis starts at zero, and the three explanatory notes.
 * Critical yields are shown for information only — they are not an FCA requirement for a DB
 * transfer and are labelled as such so nobody reads them as the test.
 */
export default function DbTransferPage() {
  const { analysisId } = useParams()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const toast = useToast()

  const [state, setState] = useState<DbState>(() => ({
    ...initialState,
    clientId: params.get('clientId') ?? undefined,
  }))
  const [savedId, setSavedId] = useState<string | undefined>(analysisId)

  const { data: client } = useClient(state.clientId)
  const { data: products } = useProducts({ wrapper: 'Sipp' })
  const { data: assumptionSets } = useAssumptionSets()
  const { data: analysis } = useDbTransferAnalysis(analysisId)
  const mutations = useDbTransferMutations()
  const createReport = useCreateReport()

  useEffect(() => {
    if (state.assumptionSetId || !assumptionSets?.length) return
    const fca = assumptionSets.find((s) => s.isFcaStandard) ?? assumptionSets[0]
    setState((s) => ({ ...s, assumptionSetId: fca.id }))
  }, [assumptionSets, state.assumptionSetId])

  useEffect(() => {
    if (!analysis) return
    setSavedId(analysis.id)
    setState((s) => ({
      ...s,
      clientId: analysis.clientId,
      dbSchemeId: analysis.dbSchemeId,
      transferDate: analysis.transferDate,
      planEndAge: analysis.planEndAge,
      proposedProductId: analysis.proposedProductId,
      proposedHoldings: analysis.proposedHoldings,
      proposedAdviserCharges: analysis.proposedAdviserCharges,
      aptaGrowthPct: analysis.aptaGrowthPct ?? 5,
      initialAdviceFee: analysis.initialAdviceFee,
      workplaceDefaultChargePct: analysis.workplaceDefaultChargePct,
      chargeBasis: analysis.chargeBasis,
      contingentChargingCarveOut: analysis.contingentChargingCarveOut ?? '',
      assumptionSetId: analysis.assumptionSetId,
      overrides: analysis.overrides ?? {},
    }))
  }, [analysis])

  const dbSchemes = useMemo(() => (client?.schemes ?? []).filter((s) => s.type === 'definedBenefit'), [client])
  const dbScheme = dbSchemes.find((s) => s.id === state.dbSchemeId)
  const locked = analysis?.status === 'locked'

  const weightsOk = totalWeightPct(state.proposedHoldings) === 100
  const ready = !!state.clientId && !!state.dbSchemeId && !!state.proposedProductId && weightsOk

  const previewRequest: DbTransferCalcRequest | null = ready
    ? {
        clientId: state.clientId,
        dbSchemeId: state.dbSchemeId,
        proposedProductId: state.proposedProductId!,
        proposedHoldings: state.proposedHoldings,
        proposedAdviserCharges: state.proposedAdviserCharges,
        aptaGrowthPct: state.aptaGrowthPct,
        planEndAge: state.planEndAge,
        initialAdviceFee: state.initialAdviceFee,
        workplaceDefaultChargePct: state.workplaceDefaultChargePct,
        assumptionSetId: state.assumptionSetId,
        overrides: Object.values(state.overrides).some((v) => v !== undefined) ? state.overrides : undefined,
        transferDate: state.transferDate,
      }
    : null

  const preview = useLivePreview<DbTransferCalcRequest, DbTransferResultDto>(previewRequest, (body, signal) =>
    calculations.dbTransfer(body, signal),
  )
  const result = analysis?.result ?? preview.data

  const write = (): DbTransferAnalysisWrite => ({
    clientId: state.clientId!,
    dbSchemeId: state.dbSchemeId!,
    transferDate: state.transferDate,
    planEndAge: state.planEndAge,
    proposedProductId: state.proposedProductId,
    proposedHoldings: state.proposedHoldings,
    proposedAdviserCharges: state.proposedAdviserCharges,
    aptaGrowthPct: state.aptaGrowthPct,
    chargeBasis: state.chargeBasis,
    contingentChargingCarveOut: state.contingentChargingCarveOut || undefined,
    initialAdviceFee: state.initialAdviceFee,
    workplaceDefaultChargePct: state.workplaceDefaultChargePct,
    assumptionSetId: state.assumptionSetId!,
    overrides: Object.values(state.overrides).some((v) => v !== undefined) ? state.overrides : undefined,
  })

  const save = async () => {
    try {
      const dto = savedId
        ? await mutations.update.mutateAsync({ id: savedId, body: write() })
        : await mutations.create.mutateAsync(write())
      setSavedId(dto.id)
      if (!analysisId) navigate(`/db-transfer/${dto.id}`, { replace: true })
      toast.success('Analysis saved')
    } catch (e) {
      toast.error('Could not save the analysis', errorMessage(e))
    }
  }

  const calculate = async () => {
    if (!savedId) return
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
      toast.success('Analysis locked')
    } catch (e) {
      toast.error('Could not lock', errorMessage(e))
    }
  }

  const createAndDownloadReport = async () => {
    if (!savedId) return
    try {
      const report = await createReport.mutateAsync({ analysisId: savedId, kind: 'dbTransfer', format: 'json' })
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
        eyebrow="DB transfer"
        title={dbScheme ? `${client?.fullName} — ${dbScheme.productName}` : 'New DB transfer analysis'}
        description="Transfer value comparator, revaluation, income comparison and stress tests for a defined benefit transfer."
        meta={
          <>
            {analysis && <StatusBadge status={analysis.status} />}
            {analysis && <Badge tone="neutral">Version {analysis.version}</Badge>}
          </>
        }
        actions={
          <>
            <Button variant="outline" icon={<Save />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending} disabled={!ready || locked}>
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

      <Alert tone="warning" title="Starting assumption" className="mb-4">
        A transfer out of a defined benefit scheme is unlikely to be in most clients' best interests
        (COBS 19.1.6G). The analysis below has to overcome that starting point, not simply compare numbers.
      </Alert>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)]">
        <div className="space-y-4">
          <Card title="1. Client and scheme">
            <div className="space-y-3">
              <ClientPicker
                value={state.clientId}
                disabled={locked || !!analysisId}
                onChange={(clientId) => setState((s) => ({ ...s, clientId, dbSchemeId: undefined }))}
              />
              <Field label="Defined benefit scheme" required>
                <Select
                  value={state.dbSchemeId ?? ''}
                  disabled={locked || dbSchemes.length === 0}
                  placeholder={dbSchemes.length === 0 ? 'This client has no DB scheme' : 'Choose the DB scheme'}
                  onChange={(e) => setState((s) => ({ ...s, dbSchemeId: e.target.value || undefined }))}
                  options={dbSchemes.map((s) => ({ value: s.id, label: `${s.productName} — CETV ${gbp(s.transferValue)}` }))}
                />
              </Field>
              {dbScheme?.definedBenefit && (
                <DefinitionList
                  cols={2}
                  items={[
                    { label: 'CETV', value: gbp(dbScheme.transferValue) },
                    { label: 'Normal retirement age', value: dbScheme.definedBenefit.normalRetirementAge },
                    { label: 'Date of leaving', value: date(dbScheme.definedBenefit.dateOfLeaving) },
                    { label: 'CETV guarantee expires', value: date(dbScheme.definedBenefit.cetvGuaranteeExpiry) },
                    { label: 'Tranches', value: dbScheme.definedBenefit.tranches.length },
                    { label: "Spouse's pension", value: fmtPct(dbScheme.definedBenefit.spousePensionPct, 0) },
                  ]}
                />
              )}
              <div className="grid gap-3 sm:grid-cols-2">
                <Field label="Transfer date" required>
                  <Input
                    type="date"
                    disabled={locked}
                    value={state.transferDate}
                    onChange={(e) => setState((s) => ({ ...s, transferDate: e.target.value }))}
                  />
                </Field>
                <Field label="Plan end age" hint="Age the cashflow comparison runs to.">
                  <IntegerInput
                    disabled={locked}
                    value={state.planEndAge}
                    onChange={(v) => setState((s) => ({ ...s, planEndAge: v ?? 95 }))}
                  />
                </Field>
              </div>
            </div>
          </Card>

          <Card title="2. Proposed arrangement">
            <div className="space-y-3">
              <Field label="Receiving product" required>
                <Select
                  value={state.proposedProductId ?? ''}
                  disabled={locked}
                  placeholder="Choose a product"
                  onChange={(e) => setState((s) => ({ ...s, proposedProductId: e.target.value || undefined }))}
                  options={(products ?? []).map((p) => ({
                    value: p.id,
                    label: `${p.providerName} — ${p.name}`,
                  }))}
                />
              </Field>
              <HoldingsEditor
                holdings={state.proposedHoldings}
                disabled={locked}
                onChange={(proposedHoldings) => setState((s) => ({ ...s, proposedHoldings }))}
              />
            </div>
          </Card>

          <Card title="3. Charges and assumptions">
            <div className="space-y-4">
              <AdviserChargeFields
                value={state.proposedAdviserCharges}
                disabled={locked}
                transferValue={dbScheme?.transferValue}
                onChange={(proposedAdviserCharges) => setState((s) => ({ ...s, proposedAdviserCharges }))}
              />
              <div className="grid gap-3 sm:grid-cols-2">
                <Field label="Initial advice fee" hint="Charged for the advice itself, whatever the outcome.">
                  <MoneyInput
                    disabled={locked}
                    value={state.initialAdviceFee}
                    onChange={(v) => setState((s) => ({ ...s, initialAdviceFee: v ?? 0 }))}
                  />
                </Field>
                <Field label="APTA growth rate" hint="Growth used for the appropriate pension transfer analysis.">
                  <PercentInput
                    disabled={locked}
                    value={state.aptaGrowthPct}
                    onChange={(v) => setState((s) => ({ ...s, aptaGrowthPct: v ?? 5 }))}
                  />
                </Field>
                <Field label="Charging basis" required>
                  <Select
                    disabled={locked}
                    value={state.chargeBasis}
                    onChange={(e) => setState((s) => ({ ...s, chargeBasis: e.target.value as ChargeBasis }))}
                    options={optionsFrom(chargeBasisLabels)}
                  />
                </Field>
                <Field
                  label="Workplace default charge"
                  hint="Charge of the client's workplace scheme, for the comparison."
                >
                  <PercentInput
                    disabled={locked}
                    value={state.workplaceDefaultChargePct ?? null}
                    placeholder="Not compared"
                    onChange={(v) => setState((s) => ({ ...s, workplaceDefaultChargePct: v ?? undefined }))}
                  />
                </Field>
              </div>
              {state.chargeBasis === 'contingent' && (
                <Field label="Carve-out relied on" required>
                  <Input
                    disabled={locked}
                    value={state.contingentChargingCarveOut}
                    placeholder="e.g. serious ill health"
                    onChange={(e) => setState((s) => ({ ...s, contingentChargingCarveOut: e.target.value }))}
                  />
                </Field>
              )}
              <AssumptionFields
                sets={assumptionSets}
                assumptionSetId={state.assumptionSetId}
                onAssumptionSetChange={(assumptionSetId) => setState((s) => ({ ...s, assumptionSetId }))}
                overrides={state.overrides}
                onOverridesChange={(overrides) => setState((s) => ({ ...s, overrides }))}
                fields={['growthIntermediatePct', 'inflationPct']}
                disabled={locked}
              />
            </div>
          </Card>
        </div>

        <div className="space-y-4">
          <Card title="Live preview">
            <PreviewStatus
              isPending={preview.isPending}
              isFetching={preview.isFetching}
              error={preview.error}
              onRefresh={preview.refresh}
              calculatedAtUtc={result?.calculatedAtUtc}
              engineVersion={result?.engineVersion}
            />
            {!ready && (
              <Alert tone="info" title="Not enough detail yet" className="mt-3">
                Choose a client with a DB scheme, a receiving product, and holdings totalling 100%.
              </Alert>
            )}
            {result && (
              <StaleWrapper stale={preview.isFetching}>
                <div className="mt-4 grid gap-3 sm:grid-cols-3">
                  <StatTile label="Sustainable real income" value={gbp(result.sustainableRealIncomeFromTransfer)} hint="If transferred" />
                  <StatTile
                    label="Scheme pension at NRA"
                    value={gbp(result.tvc.pensionAtRetirement)}
                    hint={`Age ${result.tvc.retirementAgeUsed}`}
                    tone="primary"
                  />
                  <StatTile
                    label="Life expectancy at retirement"
                    value={`${num(result.lifeExpectancyAtRetirement, 1)} yrs`}
                  />
                </div>
                <WarningsAlert warnings={result.warnings} />
              </StaleWrapper>
            )}
          </Card>

          {result && <TvcPanel result={result} />}
          {result && <RevaluationPanel result={result} />}
          {result && <IncomePanel result={result} />}
          {result && <StressAndSummary result={result} />}
        </div>
      </div>
    </>
  )
}

/** COBS 19 Annex 5 transfer value comparator. */
function TvcPanel({ result }: { result: DbTransferResultDto }) {
  const { tvc } = result
  return (
    <Card title="Transfer value comparator" description="Presented in the COBS 19 Annex 5 layout.">
      <blockquote className="rounded-lg border-l-4 border-primary-700 bg-surface-muted p-4 text-sm text-fg">
        {tvc.wording}
      </blockquote>

      <div className="mt-4">
        <ChartFrame
          title="Transfer value against the cost of the same benefits"
          description="Both bars are measured from zero, as the comparator requires."
          height={240}
          table={
            <table className="table table-dense">
              <caption className="sr-only">Transfer value comparator figures</caption>
              <thead>
                <tr>
                  <th scope="col">Measure</th>
                  <th scope="col" className="text-right">
                    Amount (£)
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <th scope="row" className="font-normal">
                    Transfer value offered
                  </th>
                  <td className="num">{gbp(tvc.cashEquivalentTransferValue)}</td>
                </tr>
                <tr>
                  <th scope="row" className="font-normal">
                    Estimated cost of the same benefits
                  </th>
                  <td className="num">{gbp(tvc.estimatedReplacementCost)}</td>
                </tr>
                <tr>
                  <th scope="row" className="font-normal">
                    Difference
                  </th>
                  <td className="num">{gbp(tvc.difference, { signed: true })}</td>
                </tr>
              </tbody>
            </table>
          }
        >
          <TvcBarChart cetv={tvc.cashEquivalentTransferValue} replacementCost={tvc.estimatedReplacementCost} />
        </ChartFrame>
      </div>

      <ol className="mt-4 list-decimal space-y-1.5 pl-5 text-xs text-fg-muted">
        {tvc.notes.map((note) => (
          <li key={note}>{note}</li>
        ))}
      </ol>

      <DefinitionList
        cols={3}
        className="mt-4"
        items={[
          { label: 'Retirement age used', value: tvc.retirementAgeUsed },
          { label: 'Term', value: `${num(tvc.termYears, 1)} yrs` },
          { label: 'Gilt yield used', value: fmtPct(tvc.giltYieldUsedPct) },
          { label: 'Discount rate used', value: fmtPct(tvc.discountRateUsedPct) },
          { label: 'Annuity cost at retirement', value: gbp(tvc.annuityCostAtRetirement) },
          { label: 'Pension at retirement', value: `${gbp(tvc.pensionAtRetirement)} pa` },
        ]}
      />
    </Card>
  )
}

function RevaluationPanel({ result }: { result: DbTransferResultDto }) {
  return (
    <Card title="Revaluation by tranche" flush description="How each slice of accrual grows to normal retirement age.">
      <div className="overflow-x-auto">
        <table className="table table-dense">
          <caption className="px-3 py-2 text-left text-sm text-fg-muted">Tranche revaluation and annuity cost</caption>
          <thead>
            <tr>
              <th scope="col">Tranche</th>
              <th scope="col" className="text-right">
                Accrued (£ pa)
              </th>
              <th scope="col" className="text-right">
                Revaluation
              </th>
              <th scope="col" className="text-right">
                Years
              </th>
              <th scope="col" className="text-right">
                At retirement (£ pa)
              </th>
              <th scope="col" className="text-right">
                Escalation
              </th>
              <th scope="col" className="text-right">
                £ per £1
              </th>
              <th scope="col" className="text-right">
                Annuity cost (£)
              </th>
            </tr>
          </thead>
          <tbody>
            {result.tvc.tranches.map((tranche) => (
              <tr key={tranche.name}>
                <th scope="row" className="font-normal">
                  {tranche.name}
                  {tranche.isGmp && (
                    <Badge tone="primary" size="sm" className="ml-2">
                      GMP
                    </Badge>
                  )}
                </th>
                <td className="num">{gbp(tranche.accruedAnnualPension)}</td>
                <td className="num">{fmtPct(tranche.revaluationRatePct)}</td>
                <td className="num">{num(tranche.yearsRevalued)}</td>
                <td className="num">{gbp(tranche.pensionAtRetirement)}</td>
                <td className="num">{fmtPct(tranche.escalationInPaymentPct)}</td>
                <td className="num">{num(tranche.annuityPricePerPound, 2)}</td>
                <td className="num">{gbp(tranche.annuityCost)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

function IncomePanel({ result }: { result: DbTransferResultDto }) {
  const { criticalYields } = result
  return (
    <>
      <Card title="Critical yields" description="For information; not an FCA requirement for a DB transfer.">
        <Alert tone="info" title="How to read these">
          The FCA's test is the appropriate pension transfer analysis and the transfer value
          comparator above. These yields are shown for information; not an FCA requirement.
        </Alert>
        <DefinitionList
          cols={3}
          className="mt-3"
          items={[
            { label: 'Type A — match the scheme pension', value: fmtPct(criticalYields.typeAAnnuityMatchPct) },
            { label: 'Type B — PCLS and reduced pension', value: fmtPct(criticalYields.typeBPclsAndReducedPensionPct) },
            { label: 'Drawdown hurdle rate', value: fmtPct(criticalYields.drawdownHurdleRatePct) },
            { label: 'Scheme PCLS', value: gbp(criticalYields.schemePcls) },
            { label: 'Residual pension after PCLS', value: `${gbp(criticalYields.residualPensionAfterPcls)} pa` },
            { label: 'Converged', value: criticalYields.converged ? 'Yes' : 'No — treat with caution' },
          ]}
        />
      </Card>

      <Card title="Income through retirement">
        <ChartFrame
          title="Scheme pension against a sustainable drawdown income"
          description="Both series are in today's money, from normal retirement age to the plan end age."
          height={300}
          table={
            <table className="table table-dense">
              <caption className="sr-only">Income comparison by age</caption>
              <thead>
                <tr>
                  <th scope="col" className="text-right">
                    Age
                  </th>
                  <th scope="col" className="text-right">
                    Scheme (nominal £)
                  </th>
                  <th scope="col" className="text-right">
                    Scheme (real £)
                  </th>
                  <th scope="col" className="text-right">
                    Drawdown (real £)
                  </th>
                  <th scope="col" className="text-right">
                    Residual fund (real £)
                  </th>
                  <th scope="col" className="text-right">
                    Death benefit (real £)
                  </th>
                </tr>
              </thead>
              <tbody>
                {result.incomeComparison.map((row) => (
                  <tr key={row.age}>
                    <th scope="row" className="num font-normal">
                      {row.age}
                    </th>
                    <td className="num">{gbp(row.schemeIncomeNominal)}</td>
                    <td className="num">{gbp(row.schemeIncomeReal)}</td>
                    <td className="num">{gbp(row.drawdownIncomeReal)}</td>
                    <td className="num">{gbp(row.residualFundReal)}</td>
                    <td className="num">{gbp(row.schemeDeathBenefitReal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          }
        >
          <IncomeComparisonChart data={result.incomeComparison} />
        </ChartFrame>
      </Card>
    </>
  )
}

function StressAndSummary({ result }: { result: DbTransferResultDto }) {
  const { summary } = result
  return (
    <>
      <Card title="Stress tests" flush description="What happens to the sustainable income if the world does not cooperate.">
        <div className="overflow-x-auto">
          <table className="table">
            <caption className="px-3 py-2 text-left text-sm text-fg-muted">Sustainable real income under stress</caption>
            <thead>
              <tr>
                <th scope="col">Scenario</th>
                <th scope="col" className="text-right">
                  Sustainable real income (£ pa)
                </th>
                <th scope="col" className="text-right">
                  Change
                </th>
              </tr>
            </thead>
            <tbody>
              {result.stressTests.map((test) => (
                <tr key={test.name}>
                  <th scope="row" className="font-normal">
                    {test.name}
                  </th>
                  <td className="num">{gbp(test.sustainableRealIncome)}</td>
                  <td className={`num ${test.change < 0 ? 'text-danger-600' : ''}`}>
                    {test.change === 0 ? '—' : gbp(test.change, { signed: true })}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <Card title="One-page summary" description="The figures a client sees first.">
        <DefinitionList
          cols={2}
          items={[
            { label: 'Initial advice fee', value: gbp(summary.initialAdviceFee) },
            { label: 'Revalued monthly income given up', value: gbp(summary.revaluedMonthlyIncome, { dp: 2 }) },
            { label: 'Payback period', value: months(summary.paybackMonths) },
            { label: 'First-year charges — proposed', value: gbp(summary.firstYearChargesProposed) },
            { label: 'Ongoing annual charges — proposed', value: gbp(summary.ongoingAnnualChargesProposed) },
            { label: 'First-year charges — ceding scheme', value: gbp(summary.firstYearChargesCeding) },
            {
              label: 'First-year charges — workplace default',
              value: summary.firstYearChargesWorkplaceDefault != null ? gbp(summary.firstYearChargesWorkplaceDefault) : 'Not compared',
              wide: true,
            },
          ]}
        />
        <p className="mt-3 text-xs text-fg-subtle">
          Assumption set: {result.assumptionSet.name} (version {result.assumptionSet.version}) · engine{' '}
          {result.engineVersion}
        </p>
      </Card>
    </>
  )
}
