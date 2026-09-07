import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { gbp, num } from '@/lib/format'
import { axisProps, chartColors, chartHeight, DASH_EXISTING, gridProps, series } from './palette'

const moneyTick = (v: number) => gbp(v, { compact: true })
const moneyTip = (v: number) => gbp(v)

const tooltipStyle = {
  contentStyle: {
    borderRadius: 8,
    border: '1px solid var(--border)',
    background: 'var(--surface)',
    fontSize: 12,
    color: 'var(--fg)',
  },
  labelStyle: { color: 'var(--fg-muted)', fontWeight: 600 },
} as const

// ---------------------------------------------------------------------------
// Pension switch: existing (dashed navy) versus proposed (solid teal)
// ---------------------------------------------------------------------------

export interface ProjectionPoint {
  year: number
  existingValue: number
  receivingValue: number
}

export function ExistingVsProposedChart({
  data,
  height = chartHeight.md,
}: {
  data: ProjectionPoint[]
  height?: number
}) {
  return (
    <ResponsiveContainer width="100%" height={height}>
      <LineChart data={data} margin={{ top: 8, right: 16, bottom: 24, left: 8 }}>
        <CartesianGrid {...gridProps} />
        <XAxis
          dataKey="year"
          {...axisProps}
          label={{ value: 'Years from now', position: 'insideBottom', offset: -12, fill: chartColors.axis, fontSize: 11 }}
        />
        <YAxis
          {...axisProps}
          tickFormatter={moneyTick}
          width={64}
          label={{ value: 'Fund value (£, real)', angle: -90, position: 'insideLeft', fill: chartColors.axis, fontSize: 11 }}
        />
        <Tooltip
          {...tooltipStyle}
          formatter={(value: number, name) => [moneyTip(value), name]}
          labelFormatter={(l: number) => `Year ${l}`}
        />
        <Legend verticalAlign="top" height={28} wrapperStyle={{ fontSize: 12 }} />
        <Line
          type="monotone"
          dataKey="existingValue"
          name="Existing arrangements"
          stroke={chartColors.existing}
          strokeDasharray={DASH_EXISTING}
          strokeWidth={2}
          dot={false}
        />
        <Line
          type="monotone"
          dataKey="receivingValue"
          name="Proposed product"
          stroke={chartColors.proposed}
          strokeWidth={2}
          dot={false}
        />
      </LineChart>
    </ResponsiveContainer>
  )
}

// ---------------------------------------------------------------------------
// DB transfer: transfer value comparator (two bars, axis anchored at zero)
// ---------------------------------------------------------------------------

export function TvcBarChart({
  cetv,
  replacementCost,
  height = chartHeight.sm,
}: {
  cetv: number
  replacementCost: number
  height?: number
}) {
  const data = [
    { name: 'Transfer value offered', value: cetv, fill: chartColors.proposed },
    { name: 'Estimated cost of the same benefits', value: replacementCost, fill: chartColors.existing },
  ]
  const max = Math.max(cetv, replacementCost)
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data} layout="vertical" margin={{ top: 8, right: 72, bottom: 24, left: 8 }}>
        <CartesianGrid {...gridProps} vertical horizontal={false} />
        {/* COBS 19 Annex 5 requires the value axis to start at zero. */}
        <XAxis
          type="number"
          domain={[0, Math.ceil(max * 1.15)]}
          {...axisProps}
          tickFormatter={moneyTick}
          label={{ value: 'Amount (£)', position: 'insideBottom', offset: -12, fill: chartColors.axis, fontSize: 11 }}
        />
        <YAxis type="category" dataKey="name" {...axisProps} width={150} />
        <Tooltip {...tooltipStyle} formatter={(value: number) => [moneyTip(value), 'Amount']} />
        <Bar dataKey="value" barSize={28} radius={[0, 4, 4, 0]} label={{ position: 'right', formatter: moneyTip, fontSize: 11, fill: 'var(--fg)' }}>
          {data.map((d) => (
            <Cell key={d.name} fill={d.fill} />
          ))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  )
}

// ---------------------------------------------------------------------------
// DB transfer: income comparison across retirement
// ---------------------------------------------------------------------------

export interface IncomePoint {
  age: number
  schemeIncomeReal: number
  drawdownIncomeReal: number
}

export function IncomeComparisonChart({
  data,
  height = chartHeight.md,
}: {
  data: IncomePoint[]
  height?: number
}) {
  return (
    <ResponsiveContainer width="100%" height={height}>
      <LineChart data={data} margin={{ top: 8, right: 16, bottom: 24, left: 8 }}>
        <CartesianGrid {...gridProps} />
        <XAxis
          dataKey="age"
          {...axisProps}
          label={{ value: 'Age', position: 'insideBottom', offset: -12, fill: chartColors.axis, fontSize: 11 }}
        />
        <YAxis
          {...axisProps}
          tickFormatter={moneyTick}
          width={64}
          label={{ value: 'Income (£ pa, real)', angle: -90, position: 'insideLeft', fill: chartColors.axis, fontSize: 11 }}
        />
        <Tooltip {...tooltipStyle} formatter={(value: number, name) => [moneyTip(value), name]} labelFormatter={(l: number) => `Age ${l}`} />
        <Legend verticalAlign="top" height={28} wrapperStyle={{ fontSize: 12 }} />
        <Line
          type="monotone"
          dataKey="schemeIncomeReal"
          name="Scheme pension (retained)"
          stroke={chartColors.existing}
          strokeDasharray={DASH_EXISTING}
          strokeWidth={2}
          dot={false}
        />
        <Line
          type="monotone"
          dataKey="drawdownIncomeReal"
          name="Sustainable income if transferred"
          stroke={chartColors.proposed}
          strokeWidth={2}
          dot={false}
        />
      </LineChart>
    </ResponsiveContainer>
  )
}

// ---------------------------------------------------------------------------
// Cashflow: stacked assets in real terms
// ---------------------------------------------------------------------------

export interface StackedAssetPoint {
  age: number
  [assetName: string]: number
}

export function AssetStackChart({
  data,
  assetNames,
  shortfallAge,
  height = chartHeight.lg,
}: {
  data: StackedAssetPoint[]
  assetNames: string[]
  shortfallAge?: number
  height?: number
}) {
  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={data} margin={{ top: 8, right: 16, bottom: 24, left: 8 }}>
        <CartesianGrid {...gridProps} />
        <XAxis
          dataKey="age"
          {...axisProps}
          label={{ value: 'Age', position: 'insideBottom', offset: -12, fill: chartColors.axis, fontSize: 11 }}
        />
        <YAxis
          {...axisProps}
          tickFormatter={moneyTick}
          width={64}
          label={{ value: "Assets (£, today's money)", angle: -90, position: 'insideLeft', fill: chartColors.axis, fontSize: 11 }}
        />
        <Tooltip {...tooltipStyle} formatter={(value: number, name) => [moneyTip(value), name]} labelFormatter={(l: number) => `Age ${l}`} />
        <Legend verticalAlign="top" height={28} wrapperStyle={{ fontSize: 12 }} />
        {shortfallAge !== undefined && (
          <ReferenceLine
            x={shortfallAge}
            stroke={chartColors.threshold}
            strokeDasharray="4 3"
            label={{ value: 'First shortfall', position: 'top', fill: chartColors.threshold, fontSize: 11 }}
          />
        )}
        {assetNames.map((name, i) => (
          <Area
            key={name}
            type="monotone"
            dataKey={name}
            name={name}
            stackId="assets"
            stroke={series[i % series.length]}
            fill={series[i % series.length]}
            fillOpacity={0.75}
          />
        ))}
      </AreaChart>
    </ResponsiveContainer>
  )
}

// ---------------------------------------------------------------------------
// Cashflow: income against expenditure, shortfalls highlighted
// ---------------------------------------------------------------------------

export interface IncomeExpensePoint {
  age: number
  netIncomeReal: number
  expensesReal: number
  shortfallReal: number
}

export function IncomeExpenseChart({
  data,
  height = chartHeight.md,
}: {
  data: IncomeExpensePoint[]
  height?: number
}) {
  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data} margin={{ top: 8, right: 16, bottom: 24, left: 8 }}>
        <CartesianGrid {...gridProps} />
        <XAxis
          dataKey="age"
          {...axisProps}
          label={{ value: 'Age', position: 'insideBottom', offset: -12, fill: chartColors.axis, fontSize: 11 }}
        />
        <YAxis
          {...axisProps}
          tickFormatter={moneyTick}
          width={64}
          label={{ value: "£ pa (today's money)", angle: -90, position: 'insideLeft', fill: chartColors.axis, fontSize: 11 }}
        />
        <Tooltip {...tooltipStyle} formatter={(value: number, name) => [moneyTip(value), name]} labelFormatter={(l: number) => `Age ${l}`} />
        <Legend verticalAlign="top" height={28} wrapperStyle={{ fontSize: 12 }} />
        <Bar dataKey="netIncomeReal" name="Net income" fill={chartColors.proposed} />
        <Bar dataKey="expensesReal" name="Expenditure" fill={chartColors.neutral} />
        <Bar dataKey="shortfallReal" name="Shortfall" fill={chartColors.threshold} />
      </BarChart>
    </ResponsiveContainer>
  )
}

// ---------------------------------------------------------------------------
// Cashflow: stochastic fan chart
// ---------------------------------------------------------------------------

export interface FanPoint {
  age: number
  /** [p5, p95] and [p25, p75] as ranges so recharts can stack the bands. */
  outerLow: number
  outerBand: number
  innerLow: number
  innerBand: number
  p50: number
}

export function FanChart({ data, height = chartHeight.lg }: { data: FanPoint[]; height?: number }) {
  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={data} margin={{ top: 8, right: 16, bottom: 24, left: 8 }}>
        <CartesianGrid {...gridProps} />
        <XAxis
          dataKey="age"
          {...axisProps}
          label={{ value: 'Age', position: 'insideBottom', offset: -12, fill: chartColors.axis, fontSize: 11 }}
        />
        <YAxis
          {...axisProps}
          tickFormatter={moneyTick}
          width={64}
          label={{ value: "Assets (£, today's money)", angle: -90, position: 'insideLeft', fill: chartColors.axis, fontSize: 11 }}
        />
        <Tooltip
          {...tooltipStyle}
          formatter={(value: number, name) => (name === 'spacer' ? [] : [moneyTip(value), name])}
          labelFormatter={(l: number) => `Age ${l}`}
        />
        {/* Invisible baselines lift each band to its percentile floor. */}
        <Area dataKey="outerLow" name="spacer" stackId="outer" stroke="none" fill="none" isAnimationActive={false} />
        <Area
          dataKey="outerBand"
          name="5th–95th percentile"
          stackId="outer"
          stroke="none"
          fill={chartColors.band}
          fillOpacity={0.75}
        />
        <Area dataKey="innerLow" name="spacer" stackId="inner" stroke="none" fill="none" isAnimationActive={false} />
        <Area
          dataKey="innerBand"
          name="25th–75th percentile"
          stackId="inner"
          stroke="none"
          fill={chartColors.bandInner}
          fillOpacity={0.85}
        />
        <Area
          dataKey="p50"
          name="Median"
          stroke={chartColors.existing}
          strokeWidth={2}
          fill="none"
          type="monotone"
        />
        <Legend verticalAlign="top" height={28} wrapperStyle={{ fontSize: 12 }} />
      </AreaChart>
    </ResponsiveContainer>
  )
}

// ---------------------------------------------------------------------------
// Fund research: asset allocation donut
// ---------------------------------------------------------------------------

export function AllocationDonut({
  data,
  height = chartHeight.sm,
}: {
  data: { name: string; value: number }[]
  height?: number
}) {
  const shown = data.filter((d) => d.value > 0)
  return (
    <ResponsiveContainer width="100%" height={height}>
      <PieChart>
        <Pie
          data={shown}
          dataKey="value"
          nameKey="name"
          innerRadius="55%"
          outerRadius="85%"
          paddingAngle={1}
          stroke="var(--surface)"
        >
          {shown.map((d, i) => (
            <Cell key={d.name} fill={series[i % series.length]} />
          ))}
        </Pie>
        <Tooltip {...tooltipStyle} formatter={(value: number, name) => [`${num(value, 1)}%`, name]} />
        <Legend verticalAlign="bottom" height={32} wrapperStyle={{ fontSize: 12 }} />
      </PieChart>
    </ResponsiveContainer>
  )
}
