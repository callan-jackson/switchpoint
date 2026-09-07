/**
 * One palette for every chart in the app, so a series means the same thing on every page.
 *
 * - `existing` (navy) is always the arrangement the client already has;
 * - `proposed` (teal) is always the thing being recommended;
 * - `threshold` (amber) marks a limit, a critical yield or a shortfall;
 * - `grid`/`axis` are slate and never carry meaning.
 *
 * Existing series are drawn dashed and proposed series solid, so the two are distinguishable
 * without colour.
 */
export const chartColors = {
  existing: '#0f2a4a',
  proposed: '#0f8b8d',
  threshold: '#d97706',
  danger: '#b91c1c',
  success: '#15803d',
  neutral: '#64748b',
  grid: '#e2e8f0',
  axis: '#64748b',
  band: '#cdeeee',
  bandInner: '#9edcdd',
} as const

/** Categorical series order for charts with more than two series. */
export const series = [
  '#0f2a4a',
  '#0f8b8d',
  '#d97706',
  '#7c3aed',
  '#64748b',
  '#2bb3b5',
  '#4f7aab',
] as const

export const DASH_EXISTING = '6 4'

/** Shared axis/grid props so every chart lines up. */
export const axisProps = {
  stroke: chartColors.axis,
  tick: { fill: chartColors.axis, fontSize: 11 },
  tickLine: false,
  axisLine: { stroke: chartColors.grid },
} as const

export const gridProps = {
  stroke: chartColors.grid,
  strokeDasharray: '3 3',
  vertical: false,
} as const

/** Chart heights stay between 240 and 360px inside a ResponsiveContainer. */
export const chartHeight = { sm: 240, md: 300, lg: 360 } as const
