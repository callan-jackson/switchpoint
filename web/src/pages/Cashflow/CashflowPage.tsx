import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { Check, Dices, Lock, Plus, Save, Trash2 } from 'lucide-react'
import { calculations } from '@/api/endpoints'
import { errorMessage } from '@/api/client'
import {
  useAssumptionSets,
  useCashflowPlan,
  useCashflowPlanMutations,
  useClient,
} from '@/api/queries'
import type {
  AssumptionOverrides,
  CashflowAssetDto,
  CashflowAssetKind,
  CashflowCalcRequest,
  CashflowEventDto,
  CashflowExpenseDto,
  CashflowIncomeDto,
  CashflowIncomeKind,
  CashflowPlanWrite,
  CashflowResultDto,
  CashflowStrategyDto,
  CrystallisationStrategy,
  DrawdownRule,
  StochasticResultDto,
} from '@/api/types'
import { flatChargeSchedule } from '@/lib/charges'
import { gbp, num, pct } from '@/lib/format'
import {
  cashflowAssetKindLabels,
  cashflowIncomeKindLabels,
  crystallisationLabels,
  drawdownRuleLabels,
  optionsFrom,
} from '@/lib/labels'
import { useLivePreview } from '@/lib/hooks'
import { Alert } from '@/components/ui/Alert'
import { Badge, StatusBadge } from '@/components/ui/Badge'
import { Button, IconButton } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { StatTile } from '@/components/ui/StatTile'
import { Tabs } from '@/components/ui/Tabs'
import { useToast } from '@/components/ui/Toast'
import { Field } from '@/components/form/Field'
import { Checkbox, Input, Select } from '@/components/form/Input'
import { IntegerInput, MoneyInput, PercentInput } from '@/components/form/NumericInput'
import { ChartFrame } from '@/components/charts/ChartFrame'
import { AssetStackChart, FanChart, IncomeExpenseChart } from '@/components/charts/Charts'
import { AssumptionFields, ClientPicker, PreviewStatus, StaleWrapper } from '@/components/domain/shared'

interface CashflowState {
  clientId?: string
  title: string
  planEndAge: number
  incomes: CashflowIncomeDto[]
  expenses: CashflowExpenseDto[]
  assets: CashflowAssetDto[]
  events: CashflowEventDto[]
  strategy: CashflowStrategyDto
  stochasticSeed: number
  stochasticPaths: number
  assumptionSetId?: string
  overrides: AssumptionOverrides
}

const defaultStrategy: CashflowStrategyDto = {
  withdrawalOrder: ['cash', 'isa', 'generalInvestmentAccount', 'uncrystallisedPension'],
  crystallisation: 'phasedDrawdown',
  drawdownRule: 'gapFill',
  drawdownParameter: 0,
  reinvestSurplusIntoIsa: true,
}

const initialState: CashflowState = {
  title: '',
  planEndAge: 95,
  incomes: [],
  expenses: [],
  assets: [],
  events: [],
  strategy: defaultStrategy,
  stochasticSeed: 20_260_907,
  stochasticPaths: 1_000,
  overrides: {},
}

type TabId = 'people' | 'incomes' | 'expenses' | 'assets' | 'events' | 'strategy'

/**
 * Cashflow plan builder plus its deterministic and stochastic results.
 *
 * The deterministic projection is a live preview against `POST /calculations/cashflow`; the
 * stochastic run is deliberately manual, because a thousand paths is not something to fire on
 * every keystroke. The conservativeness check compares the median stochastic outcome with the
 * deterministic one, which is the sanity test COBS expects of a stochastic model.
 */
export default function CashflowPage() {
  const { planId } = useParams()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const toast = useToast()

  const [state, setState] = useState<CashflowState>(() => ({
    ...initialState,
    clientId: params.get('clientId') ?? undefined,
  }))
  const [savedId, setSavedId] = useState<string | undefined>(planId)
  const [tab, setTab] = useState<TabId>('people')
  const [stochastic, setStochastic] = useState<StochasticResultDto | null>(null)
  const [runningStochastic, setRunningStochastic] = useState(false)

  const { data: client } = useClient(state.clientId)
  const { data: assumptionSets } = useAssumptionSets()
  const { data: plan } = useCashflowPlan(planId)
  const mutations = useCashflowPlanMutations()

  useEffect(() => {
    if (state.assumptionSetId || !assumptionSets?.length) return
    const fca = assumptionSets.find((s) => s.isFcaStandard) ?? assumptionSets[0]
    setState((s) => ({ ...s, assumptionSetId: fca.id }))
  }, [assumptionSets, state.assumptionSetId])

  useEffect(() => {
    if (!plan) return
    setSavedId(plan.id)
    setState((s) => ({
      ...s,
      clientId: plan.clientId,
      title: plan.title,
      planEndAge: plan.planEndAge,
      incomes: plan.incomes,
      expenses: plan.expenses,
      assets: plan.assets,
      events: plan.events,
      strategy: plan.strategy,
      stochasticSeed: plan.stochasticSeed ?? s.stochasticSeed,
      stochasticPaths: plan.stochasticPaths,
      assumptionSetId: plan.assumptionSetId,
      overrides: plan.overrides ?? {},
    }))
  }, [plan])

  // Seed a first plan from the client's own record so the page is useful immediately.
  useEffect(() => {
    if (!client || state.assets.length > 0 || state.incomes.length > 0) return
    setState((s) => ({
      ...s,
      title: s.title || `${client.fullName} — retirement plan`,
      incomes: [
        {
          name: 'Salary',
          kind: 'employment',
          annualAmount: client.annualSalary,
          fromAge: client.age,
          toAge: client.targetRetirementAge,
          growthPct: 3,
          isTaxable: true,
          personIndex: 0,
        },
      ],
      expenses: [{ name: 'Core spending', annualAmount: Math.round(client.annualSalary * 0.55), fromAge: client.age }],
      assets: client.schemes
        .filter((scheme) => scheme.type !== 'definedBenefit')
        .map((scheme) => ({
          name: scheme.productName,
          kind: scheme.type === 'isa' ? ('isa' as const) : ('uncrystallisedPension' as const),
          value: scheme.currentValue,
          growthPct: 5,
          charges: flatChargeSchedule(0.3, scheme.weightedOcfPct ?? 0.2),
          schemeId: scheme.id,
          costBasis: 0,
          annualContribution: 0,
          employerContribution: 0,
          salarySacrifice: false,
          personIndex: 0,
        })),
    }))
  }, [client, state.assets.length, state.incomes.length])

  const locked = plan?.status === 'locked'
  const ready = !!client && state.assets.length > 0 && state.expenses.length > 0

  const request: CashflowCalcRequest | null = useMemo(() => {
    if (!client || !ready) return null
    return {
      clientId: client.id,
      person: {
        name: client.fullName,
        dateOfBirth: client.dateOfBirth,
        sex: client.sex,
        taxRegime: client.taxRegime,
        retirementAge: client.targetRetirementAge,
        statePensionForecastWeekly: client.statePension.forecastWeeklyAmount,
        statePensionQualifyingYears: client.statePension.qualifyingYears,
        mpaaTriggered: false,
      },
      planEndAge: state.planEndAge,
      incomes: state.incomes,
      expenses: state.expenses,
      assets: state.assets,
      events: state.events,
      strategy: state.strategy,
      assumptionSetId: state.assumptionSetId,
      overrides: Object.values(state.overrides).some((v) => v !== undefined) ? state.overrides : undefined,
    }
  }, [client, ready, state])

  const preview = useLivePreview<CashflowCalcRequest, CashflowResultDto>(request, (body, signal) =>
    calculations.cashflow(body, signal),
  )
  const result = preview.data

  const runStochastic = async () => {
    if (!request) return
    setRunningStochastic(true)
    try {
      const dto = await calculations.cashflowStochastic({
        ...request,
        seed: state.stochasticSeed,
        paths: state.stochasticPaths,
      })
      setStochastic(dto)
      toast.success('Stochastic run complete', `${num(dto.paths)} paths`)
    } catch (e) {
      toast.error('Stochastic run failed', errorMessage(e))
    } finally {
      setRunningStochastic(false)
    }
  }

  const write = (): CashflowPlanWrite => ({
    clientId: state.clientId!,
    title: state.title || 'Cashflow plan',
    planEndAge: state.planEndAge,
    incomes: state.incomes,
    expenses: state.expenses,
    assets: state.assets,
    events: state.events,
    strategy: state.strategy,
    stochasticSeed: state.stochasticSeed,
    stochasticPaths: state.stochasticPaths,
    assumptionSetId: state.assumptionSetId!,
    overrides: Object.values(state.overrides).some((v) => v !== undefined) ? state.overrides : undefined,
  })

  const save = async () => {
    try {
      const dto = savedId
        ? await mutations.update.mutateAsync({ id: savedId, body: write() })
        : await mutations.create.mutateAsync(write())
      setSavedId(dto.id)
      if (!planId) navigate(`/cashflow/${dto.id}`, { replace: true })
      toast.success('Plan saved', dto.title)
    } catch (e) {
      toast.error('Could not save the plan', errorMessage(e))
    }
  }

  const assetNames = state.assets.map((a) => a.name)
  const stackData = (result?.rows ?? []).map((row) => {
    const point: Record<string, number> = { age: row.age }
    for (const asset of row.assets) point[asset.name] = asset.valueReal
    return point as { age: number } & Record<string, number>
  })
  const incomeExpenseData = (result?.rows ?? []).map((row) => {
    const deflator = row.netIncome === 0 ? 1 : row.netIncomeReal / row.netIncome
    return {
      age: row.age,
      netIncomeReal: row.netIncomeReal,
      expensesReal: row.expenses * deflator,
      shortfallReal: row.shortfall * deflator,
    }
  })
  const fanData = (stochastic?.totalAssetsReal ?? []).map((row) => ({
    age: row.age,
    outerLow: row.p5,
    outerBand: Math.max(0, row.p95 - row.p5),
    innerLow: row.p25,
    innerBand: Math.max(0, row.p75 - row.p25),
    p50: row.p50,
  }))

  return (
    <>
      <PageHeader
        eyebrow="Cashflow"
        title={state.title || 'New cashflow plan'}
        description="Year-by-year income, tax and assets in today's money, with an optional stochastic run."
        meta={
          <>
            {plan && <StatusBadge status={plan.status} />}
            {result && (
              <Badge tone={result.succeeds ? 'success' : 'danger'}>
                {result.succeeds ? 'Plan holds to the end' : `Shortfall from age ${result.firstShortfallAge}`}
              </Badge>
            )}
          </>
        }
        actions={
          <>
            <Button variant="outline" icon={<Save />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending} disabled={!state.clientId || !state.assumptionSetId || locked}>
              Save
            </Button>
            <Button
              variant="outline"
              icon={<Check />}
              onClick={() => savedId && mutations.calculate.mutateAsync(savedId)}
              loading={mutations.calculate.isPending}
              disabled={!savedId || locked}
            >
              Calculate
            </Button>
            <Button
              variant="outline"
              icon={<Lock />}
              onClick={() => savedId && mutations.lock.mutateAsync(savedId)}
              loading={mutations.lock.isPending}
              disabled={!savedId || locked}
            >
              Lock
            </Button>
            <Button icon={<Dices />} onClick={runStochastic} loading={runningStochastic} disabled={!request}>
              Run stochastic
            </Button>
          </>
        }
      />

      <div className="grid gap-6 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
        <Card title="Plan builder" flush>
          <div className="p-4">
            <Tabs
              aria-label="Plan sections"
              value={tab}
              onChange={setTab}
              items={[
                { id: 'people', label: 'People' },
                { id: 'incomes', label: 'Incomes', badge: state.incomes.length || undefined },
                { id: 'expenses', label: 'Expenses', badge: state.expenses.length || undefined },
                { id: 'assets', label: 'Assets', badge: state.assets.length || undefined },
                { id: 'events', label: 'Events', badge: state.events.length || undefined },
                { id: 'strategy', label: 'Strategy' },
              ]}
            >
              {tab === 'people' && (
                <div className="space-y-3">
                  <ClientPicker
                    value={state.clientId}
                    disabled={locked || !!planId}
                    onChange={(clientId) => setState((s) => ({ ...s, clientId, assets: [], incomes: [], expenses: [] }))}
                  />
                  <Field label="Plan title" required>
                    <Input value={state.title} disabled={locked} onChange={(e) => setState((s) => ({ ...s, title: e.target.value }))} />
                  </Field>
                  <Field label="Plan end age" hint="The projection runs to this age.">
                    <IntegerInput
                      value={state.planEndAge}
                      disabled={locked}
                      onChange={(v) => setState((s) => ({ ...s, planEndAge: v ?? 95 }))}
                    />
                  </Field>
                  {client && (
                    <Alert tone="info" title={`Modelling ${client.fullName}`}>
                      Age {client.age}, retiring at {client.targetRetirementAge}, {client.taxRegime === 'scotland' ? 'Scottish' : 'rest of UK'} tax
                      regime. State pension forecast {client.statePension.forecastWeeklyAmount ? `${gbp(client.statePension.forecastWeeklyAmount, { dp: 2 })} per week` : 'not recorded'}.
                    </Alert>
                  )}
                  <AssumptionFields
                    sets={assumptionSets}
                    assumptionSetId={state.assumptionSetId}
                    onAssumptionSetChange={(assumptionSetId) => setState((s) => ({ ...s, assumptionSetId }))}
                    overrides={state.overrides}
                    onOverridesChange={(overrides) => setState((s) => ({ ...s, overrides }))}
                    fields={['inflationPct', 'earningsGrowthPct', 'statePensionIncreasePct']}
                    disabled={locked}
                  />
                </div>
              )}

              {tab === 'incomes' && (
                <RowEditor
                  title="Incomes"
                  rows={state.incomes}
                  disabled={locked}
                  onAdd={() =>
                    setState((s) => ({
                      ...s,
                      incomes: [
                        ...s.incomes,
                        {
                          name: 'New income',
                          kind: 'other',
                          annualAmount: 0,
                          fromAge: client?.age ?? 55,
                          growthPct: 2,
                          isTaxable: true,
                          personIndex: 0,
                        },
                      ],
                    }))
                  }
                  onRemove={(i) => setState((s) => ({ ...s, incomes: s.incomes.filter((_, x) => x !== i) }))}
                  render={(income, _index, update) => (
                    <>
                      <Field label="Name">
                        <Input sizing="sm" value={income.name} onChange={(e) => update({ name: e.target.value })} />
                      </Field>
                      <Field label="Kind">
                        <Select
                          sizing="sm"
                          value={income.kind}
                          onChange={(e) => update({ kind: e.target.value as CashflowIncomeKind })}
                          options={optionsFrom(cashflowIncomeKindLabels)}
                        />
                      </Field>
                      <Field label="Amount pa">
                        <MoneyInput sizing="sm" value={income.annualAmount} onChange={(v) => update({ annualAmount: v ?? 0 })} />
                      </Field>
                      <Field label="From age">
                        <IntegerInput sizing="sm" value={income.fromAge} onChange={(v) => update({ fromAge: v ?? 0 })} />
                      </Field>
                      <Field label="To age">
                        <IntegerInput
                          sizing="sm"
                          value={income.toAge ?? null}
                          placeholder="For life"
                          onChange={(v) => update({ toAge: v ?? undefined })}
                        />
                      </Field>
                      <Field label="Growth">
                        <PercentInput sizing="sm" value={income.growthPct} onChange={(v) => update({ growthPct: v ?? 0 })} />
                      </Field>
                      <div className="flex items-end pb-1">
                        <Checkbox
                          label="Taxable"
                          checked={income.isTaxable}
                          onChange={(e) => update({ isTaxable: e.target.checked })}
                        />
                      </div>
                    </>
                  )}
                  onUpdate={(i, patch) =>
                    setState((s) => {
                      const incomes = [...s.incomes]
                      incomes[i] = { ...incomes[i], ...patch }
                      return { ...s, incomes }
                    })
                  }
                />
              )}

              {tab === 'expenses' && (
                <RowEditor
                  title="Expense phases"
                  rows={state.expenses}
                  disabled={locked}
                  onAdd={() =>
                    setState((s) => ({
                      ...s,
                      expenses: [...s.expenses, { name: 'New phase', annualAmount: 0, fromAge: client?.age ?? 55 }],
                    }))
                  }
                  onRemove={(i) => setState((s) => ({ ...s, expenses: s.expenses.filter((_, x) => x !== i) }))}
                  onUpdate={(i, patch) =>
                    setState((s) => {
                      const expenses = [...s.expenses]
                      expenses[i] = { ...expenses[i], ...patch }
                      return { ...s, expenses }
                    })
                  }
                  render={(expense, _index, update) => (
                    <>
                      <Field label="Name">
                        <Input sizing="sm" value={expense.name} onChange={(e) => update({ name: e.target.value })} />
                      </Field>
                      <Field label="Amount pa (today's money)">
                        <MoneyInput sizing="sm" value={expense.annualAmount} onChange={(v) => update({ annualAmount: v ?? 0 })} />
                      </Field>
                      <Field label="From age">
                        <IntegerInput sizing="sm" value={expense.fromAge} onChange={(v) => update({ fromAge: v ?? 0 })} />
                      </Field>
                      <Field label="To age">
                        <IntegerInput
                          sizing="sm"
                          value={expense.toAge ?? null}
                          placeholder="To the end"
                          onChange={(v) => update({ toAge: v ?? undefined })}
                        />
                      </Field>
                    </>
                  )}
                />
              )}

              {tab === 'assets' && (
                <RowEditor
                  title="Assets"
                  rows={state.assets}
                  disabled={locked}
                  onAdd={() =>
                    setState((s) => ({
                      ...s,
                      assets: [
                        ...s.assets,
                        {
                          name: 'New asset',
                          kind: 'isa',
                          value: 0,
                          growthPct: 5,
                          charges: flatChargeSchedule(0.3, 0.2),
                          costBasis: 0,
                          annualContribution: 0,
                          employerContribution: 0,
                          salarySacrifice: false,
                          personIndex: 0,
                          allocation: { equityPct: 60, fixedInterestPct: 30, propertyPct: 5, cashPct: 5, alternativesPct: 0 },
                        },
                      ],
                    }))
                  }
                  onRemove={(i) => setState((s) => ({ ...s, assets: s.assets.filter((_, x) => x !== i) }))}
                  onUpdate={(i, patch) =>
                    setState((s) => {
                      const assets = [...s.assets]
                      assets[i] = { ...assets[i], ...patch }
                      return { ...s, assets }
                    })
                  }
                  render={(asset, _index, update) => (
                    <>
                      <Field label="Name">
                        <Input sizing="sm" value={asset.name} onChange={(e) => update({ name: e.target.value })} />
                      </Field>
                      <Field label="Kind">
                        <Select
                          sizing="sm"
                          value={asset.kind}
                          onChange={(e) => update({ kind: e.target.value as CashflowAssetKind })}
                          options={optionsFrom(cashflowAssetKindLabels)}
                        />
                      </Field>
                      <Field label="Value">
                        <MoneyInput sizing="sm" value={asset.value} onChange={(v) => update({ value: v ?? 0 })} />
                      </Field>
                      <Field label="Growth">
                        <PercentInput sizing="sm" value={asset.growthPct} onChange={(v) => update({ growthPct: v ?? 0 })} />
                      </Field>
                      <Field label="Contribution pa">
                        <MoneyInput sizing="sm" value={asset.annualContribution} onChange={(v) => update({ annualContribution: v ?? 0 })} />
                      </Field>
                      <Field label="Employer pa">
                        <MoneyInput sizing="sm" value={asset.employerContribution} onChange={(v) => update({ employerContribution: v ?? 0 })} />
                      </Field>
                      <Field label="Equity allocation">
                        <PercentInput
                          sizing="sm"
                          value={asset.allocation?.equityPct ?? null}
                          onChange={(v) =>
                            update({
                              allocation: {
                                equityPct: v ?? 0,
                                fixedInterestPct: Math.max(0, 100 - (v ?? 0)),
                                propertyPct: 0,
                                cashPct: 0,
                                alternativesPct: 0,
                              },
                            })
                          }
                        />
                      </Field>
                    </>
                  )}
                />
              )}

              {tab === 'events' && (
                <RowEditor
                  title="One-off events"
                  rows={state.events}
                  disabled={locked}
                  onAdd={() =>
                    setState((s) => ({ ...s, events: [...s.events, { name: 'New event', atAge: client?.age ?? 60, amount: 0 }] }))
                  }
                  onRemove={(i) => setState((s) => ({ ...s, events: s.events.filter((_, x) => x !== i) }))}
                  onUpdate={(i, patch) =>
                    setState((s) => {
                      const events = [...s.events]
                      events[i] = { ...events[i], ...patch }
                      return { ...s, events }
                    })
                  }
                  render={(event, _index, update) => (
                    <>
                      <Field label="Name">
                        <Input sizing="sm" value={event.name} onChange={(e) => update({ name: e.target.value })} />
                      </Field>
                      <Field label="At age">
                        <IntegerInput sizing="sm" value={event.atAge} onChange={(v) => update({ atAge: v ?? 0 })} />
                      </Field>
                      <Field label="Amount" hint="Negative for a cost.">
                        <MoneyInput sizing="sm" value={event.amount} onChange={(v) => update({ amount: v ?? 0 })} />
                      </Field>
                    </>
                  )}
                />
              )}

              {tab === 'strategy' && (
                <div className="space-y-3">
                  <Field label="Withdrawal order" hint="Assets are drawn in this order when income falls short.">
                    <Input
                      value={state.strategy.withdrawalOrder.join(', ')}
                      disabled={locked}
                      onChange={(e) =>
                        setState((s) => ({
                          ...s,
                          strategy: {
                            ...s.strategy,
                            withdrawalOrder: e.target.value.split(',').map((v) => v.trim()).filter(Boolean),
                          },
                        }))
                      }
                    />
                  </Field>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <Field label="Crystallisation">
                      <Select
                        disabled={locked}
                        value={state.strategy.crystallisation}
                        onChange={(e) =>
                          setState((s) => ({
                            ...s,
                            strategy: { ...s.strategy, crystallisation: e.target.value as CrystallisationStrategy },
                          }))
                        }
                        options={optionsFrom(crystallisationLabels)}
                      />
                    </Field>
                    <Field label="Drawdown rule">
                      <Select
                        disabled={locked}
                        value={state.strategy.drawdownRule}
                        onChange={(e) =>
                          setState((s) => ({
                            ...s,
                            strategy: { ...s.strategy, drawdownRule: e.target.value as DrawdownRule },
                          }))
                        }
                        options={optionsFrom(drawdownRuleLabels)}
                      />
                    </Field>
                    <Field label="Drawdown parameter" hint="Amount or percentage, depending on the rule.">
                      <MoneyInput
                        disabled={locked}
                        value={state.strategy.drawdownParameter}
                        onChange={(v) => setState((s) => ({ ...s, strategy: { ...s.strategy, drawdownParameter: v ?? 0 } }))}
                      />
                    </Field>
                    <Field label="Annuity purchase age">
                      <IntegerInput
                        disabled={locked}
                        value={state.strategy.annuityPurchaseAge ?? null}
                        placeholder="No annuity"
                        onChange={(v) => setState((s) => ({ ...s, strategy: { ...s.strategy, annuityPurchaseAge: v ?? undefined } }))}
                      />
                    </Field>
                  </div>
                  <Checkbox
                    label="Reinvest surplus into an ISA"
                    disabled={locked}
                    checked={state.strategy.reinvestSurplusIntoIsa}
                    onChange={(e) =>
                      setState((s) => ({ ...s, strategy: { ...s.strategy, reinvestSurplusIntoIsa: e.target.checked } }))
                    }
                  />
                  <div className="grid gap-3 sm:grid-cols-2">
                    <Field label="Stochastic seed" hint="Fixing the seed makes a run reproducible.">
                      <IntegerInput
                        disabled={locked}
                        value={state.stochasticSeed}
                        onChange={(v) => setState((s) => ({ ...s, stochasticSeed: v ?? 0 }))}
                      />
                    </Field>
                    <Field label="Paths">
                      <IntegerInput
                        disabled={locked}
                        value={state.stochasticPaths}
                        onChange={(v) => setState((s) => ({ ...s, stochasticPaths: v ?? 1000 }))}
                      />
                    </Field>
                  </div>
                </div>
              )}
            </Tabs>
          </div>
        </Card>

        <div className="space-y-4">
          <Card title="Results">
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
                Choose a client, then add at least one asset and one expense phase.
              </Alert>
            )}
            {result && (
              <StaleWrapper stale={preview.isFetching}>
                <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                  <StatTile
                    label="Sustainable spend"
                    value={gbp(result.sustainableSpend)}
                    hint="Per year, today's money"
                    tone="accent"
                  />
                  <StatTile
                    label="Legacy at plan end"
                    value={gbp(result.legacyAtEndReal)}
                    hint="Today's money"
                    tone={result.legacyAtEndReal > 0 ? 'success' : 'danger'}
                  />
                  <StatTile label="Total income tax" value={gbp(result.totalIncomeTax)} />
                  <StatTile
                    label="First shortfall"
                    value={result.firstShortfallAge ? `Age ${result.firstShortfallAge}` : 'None'}
                    tone={result.firstShortfallAge ? 'danger' : 'success'}
                  />
                </div>

                <div className="mt-6">
                  <ChartFrame
                    title="Assets through the plan"
                    description="Stacked asset values in today's money, from now to the plan end age."
                    height={340}
                    table={<CashflowTable result={result} />}
                  >
                    <AssetStackChart data={stackData} assetNames={assetNames} shortfallAge={result.firstShortfallAge} />
                  </ChartFrame>
                </div>

                <div className="mt-6">
                  <ChartFrame
                    title="Income against expenditure"
                    description="Net income and expenditure in today's money; amber bars are the shortfall."
                    height={280}
                    table={<CashflowTable result={result} />}
                  >
                    <IncomeExpenseChart data={incomeExpenseData} />
                  </ChartFrame>
                </div>
              </StaleWrapper>
            )}
          </Card>

          {result && (
            <Card title="Year by year" flush description="Every row the engine produced, in today's money.">
              <div className="max-h-[520px] overflow-auto">
                <CashflowTable result={result} />
              </div>
            </Card>
          )}

          <Card
            title="Stochastic projection"
            description="A Monte Carlo run around the same plan. Fire it deliberately, not on every keystroke."
            actions={
              <Button size="sm" icon={<Dices />} onClick={runStochastic} loading={runningStochastic} disabled={!request}>
                Run stochastic
              </Button>
            }
          >
            {!stochastic ? (
              <p className="text-sm text-fg-muted">
                No stochastic run yet. It uses seed {num(state.stochasticSeed)} over {num(state.stochasticPaths)} paths,
                so the same inputs always give the same answer.
              </p>
            ) : (
              <>
                <div className="grid gap-3 sm:grid-cols-3">
                  <StatTile
                    label="Probability of success"
                    value={pct(stochastic.probabilityOfSuccess, 0)}
                    tone={stochastic.probabilityOfSuccess >= 0.75 ? 'success' : stochastic.probabilityOfSuccess >= 0.5 ? 'warning' : 'danger'}
                    hint={`${num(stochastic.paths)} paths, seed ${stochastic.seed}`}
                  />
                  <StatTile label="Mean legacy" value={gbp(stochastic.meanLegacyReal)} hint="Today's money" />
                  <StatTile
                    label="Worst decile shortfall"
                    value={stochastic.worstDecileShortfallAge ? `Age ${stochastic.worstDecileShortfallAge}` : 'None'}
                    tone={stochastic.worstDecileShortfallAge ? 'warning' : 'success'}
                  />
                </div>

                <Alert
                  tone={stochastic.conservativeness.medianIsNoLessConservative ? 'success' : 'warning'}
                  title="Conservativeness check"
                  className="mt-3"
                >
                  Deterministic assets at the plan end {gbp(stochastic.conservativeness.deterministicAssetsAtEnd)};
                  stochastic median {gbp(stochastic.conservativeness.medianAssetsAtEnd)}.{' '}
                  {stochastic.conservativeness.medianIsNoLessConservative
                    ? 'The median outcome is no less conservative than the deterministic projection.'
                    : 'The median outcome is more optimistic than the deterministic projection — review the capital market assumptions before relying on it.'}
                </Alert>

                <div className="mt-6">
                  <ChartFrame
                    title="Range of outcomes"
                    description="Total assets in today's money: the 5th–95th and 25th–75th percentile bands with the median."
                    height={340}
                    table={
                      <table className="table table-dense">
                        <caption className="sr-only">Percentiles of total assets by age</caption>
                        <thead>
                          <tr>
                            <th scope="col" className="text-right">
                              Age
                            </th>
                            <th scope="col" className="text-right">
                              5th (£)
                            </th>
                            <th scope="col" className="text-right">
                              25th (£)
                            </th>
                            <th scope="col" className="text-right">
                              Median (£)
                            </th>
                            <th scope="col" className="text-right">
                              75th (£)
                            </th>
                            <th scope="col" className="text-right">
                              95th (£)
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {stochastic.totalAssetsReal.map((row) => (
                            <tr key={row.age}>
                              <th scope="row" className="num font-normal">
                                {row.age}
                              </th>
                              <td className="num">{gbp(row.p5)}</td>
                              <td className="num">{gbp(row.p25)}</td>
                              <td className="num">{gbp(row.p50)}</td>
                              <td className="num">{gbp(row.p75)}</td>
                              <td className="num">{gbp(row.p95)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    }
                  >
                    <FanChart data={fanData} />
                  </ChartFrame>
                </div>
              </>
            )}
          </Card>
        </div>
      </div>
    </>
  )
}

/** Generic add/remove/edit list used by the incomes, expenses, assets and events tabs. */
function RowEditor<T>({
  title,
  rows,
  render,
  onAdd,
  onRemove,
  onUpdate,
  disabled,
}: {
  title: string
  rows: T[]
  render: (row: T, index: number, update: (patch: Partial<T>) => void) => ReactNode
  onAdd: () => void
  onRemove: (index: number) => void
  onUpdate: (index: number, patch: Partial<T>) => void
  disabled?: boolean
}) {
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="section-title">{title}</p>
        <Button size="sm" variant="outline" icon={<Plus />} onClick={onAdd} disabled={disabled}>
          Add
        </Button>
      </div>
      {rows.length === 0 ? (
        <p className="text-sm text-fg-muted">Nothing added yet.</p>
      ) : (
        <ul className="space-y-3">
          {rows.map((row, index) => (
            <li key={index} className="rounded-lg border border-border p-3">
              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                {render(row, index, (patch) => onUpdate(index, patch))}
              </div>
              <div className="mt-2 flex justify-end">
                <IconButton size="sm" label={`Remove item ${index + 1}`} onClick={() => onRemove(index)} disabled={disabled}>
                  <Trash2 />
                </IconButton>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function CashflowTable({ result }: { result: CashflowResultDto }) {
  return (
    <table className="table table-dense">
      <caption className="px-3 py-2 text-left text-sm text-fg-muted">
        Year-by-year cashflow. Real columns are in today's money.
      </caption>
      <thead>
        <tr>
          <th scope="col" className="text-right">
            Age
          </th>
          <th scope="col" className="text-right">
            Earned (£)
          </th>
          <th scope="col" className="text-right">
            State pension (£)
          </th>
          <th scope="col" className="text-right">
            Withdrawals (£)
          </th>
          <th scope="col" className="text-right">
            Income tax (£)
          </th>
          <th scope="col" className="text-right">
            Net income (real £)
          </th>
          <th scope="col" className="text-right">
            Expenses (£)
          </th>
          <th scope="col" className="text-right">
            Shortfall (£)
          </th>
          <th scope="col" className="text-right">
            Assets (real £)
          </th>
        </tr>
      </thead>
      <tbody>
        {result.rows.map((row) => (
          <tr key={row.year} className={row.shortfall > 0 ? 'bg-warning-50' : undefined}>
            <th scope="row" className="num font-normal">
              {row.age}
            </th>
            <td className="num">{gbp(row.employmentIncome)}</td>
            <td className="num">{gbp(row.statePensionIncome)}</td>
            <td className="num">{gbp(row.pensionWithdrawalsTaxable + row.isaWithdrawals + row.giaWithdrawals + row.cashWithdrawals)}</td>
            <td className="num">{gbp(row.incomeTax)}</td>
            <td className="num">{gbp(row.netIncomeReal)}</td>
            <td className="num">{gbp(row.expenses)}</td>
            <td className={`num ${row.shortfall > 0 ? 'font-semibold text-danger-600' : ''}`}>
              {row.shortfall > 0 ? gbp(row.shortfall) : '—'}
            </td>
            <td className="num">{gbp(row.totalAssetsReal)}</td>
          </tr>
        ))}
      </tbody>
      <tfoot>
        <tr>
          <th scope="row" className="num">
            Totals
          </th>
          <td colSpan={3} />
          <td className="num font-semibold">{gbp(result.totalIncomeTax)}</td>
          <td colSpan={2} />
          <td className="num font-semibold">{result.totalShortfall > 0 ? gbp(result.totalShortfall) : '—'}</td>
          <td className="num font-semibold">{gbp(result.legacyAtEndReal)}</td>
        </tr>
      </tfoot>
    </table>
  )
}
