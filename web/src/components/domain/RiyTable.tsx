import type { RiyDto } from '@/api/types'
import { fmtPct, gbp } from '@/lib/format'
import { Alert } from '@/components/ui/Alert'

/**
 * COBS 13 Annex 3 "effect of charges" table plus the two required sentences.
 *
 * The sentences come straight from the engine (`productSentence` / `totalSentence`) so the
 * wording that reaches a client is the wording the calculation produced, never a UI paraphrase.
 */
export function RiyTable({ riy, caption }: { riy: RiyDto; caption: string }) {
  return (
    <div className="space-y-3">
      <div className="overflow-x-auto">
        <table className="table table-dense">
          <caption className="px-3 py-2 text-left text-sm text-fg-muted">{caption}</caption>
          <thead>
            <tr>
              <th scope="col" className="text-right">
                At end of year
              </th>
              <th scope="col" className="text-right">
                Payments to date (£)
              </th>
              <th scope="col" className="text-right">
                Before charges (£)
              </th>
              <th scope="col" className="text-right">
                After plan and investment charges (£)
              </th>
              <th scope="col" className="text-right">
                After all charges (£)
              </th>
              <th scope="col" className="text-right">
                Effect of deductions to date (£)
              </th>
            </tr>
          </thead>
          <tbody>
            {riy.effectOfCharges.map((row) => (
              <tr key={row.year}>
                <th scope="row" className="num font-normal">
                  {row.year}
                </th>
                <td className="num">{gbp(row.paymentsToDate)}</td>
                <td className="num">{gbp(row.beforeCharges)}</td>
                <td className="num">{gbp(row.planAndInvestmentChargesOnly)}</td>
                <td className="num">{gbp(row.afterAllCharges)}</td>
                <td className="num">{gbp(row.effectOfDeductionsToDate)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Alert tone="info" title="Reduction in yield">
        <p>{riy.productSentence}</p>
        <p className="mt-1">{riy.totalSentence}</p>
        <p className="mt-2 text-xs">
          Product RIY {fmtPct(riy.productRiyPct)} · total RIY {fmtPct(riy.totalRiyPct)} · assumed growth{' '}
          {fmtPct(riy.growthPct, 1)}
        </p>
      </Alert>
    </div>
  )
}

/** Where the money went: one row per charge category, largest first. */
export function ChargeBreakdownTable({ riy }: { riy: RiyDto }) {
  const rows = (
    [
      ['Platform', riy.totalCharges.platform],
      ['Product', riy.totalCharges.product],
      ['Fund (OCF)', riy.totalCharges.fund],
      ['Transaction costs', riy.totalCharges.transaction],
      ['Adviser — initial', riy.totalCharges.adviserInitial],
      ['Adviser — ongoing', riy.totalCharges.adviserOngoing],
      ['Fixed fees', riy.totalCharges.fixed],
      ['Dealing', riy.totalCharges.dealing],
      ['Switching', riy.totalCharges.switch],
      ['Allocation and spread', riy.totalCharges.allocationAndSpread],
      ['Exit penalty', riy.totalCharges.exitPenalty],
      ['Discounts', riy.totalCharges.discounts],
    ] as const
  )
    .filter(([, amount]) => Math.abs(amount) > 0.5)
    .sort((a, b) => b[1] - a[1])

  return (
    <div className="overflow-x-auto">
      <table className="table table-dense">
        <caption className="px-3 py-2 text-left text-sm text-fg-muted">
          Charges deducted over the projection term
        </caption>
        <thead>
          <tr>
            <th scope="col">Charge</th>
            <th scope="col" className="text-right">
              Total (£)
            </th>
            <th scope="col" className="text-right">
              Share
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map(([label, amount]) => (
            <tr key={label}>
              <th scope="row" className="font-normal">
                {label}
              </th>
              <td className="num">{gbp(amount)}</td>
              <td className="num">{fmtPct((amount / Math.max(riy.totalCharges.total, 1)) * 100, 1)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <th scope="row" className="font-semibold">
              Total
            </th>
            <td className="num font-semibold">{gbp(riy.totalCharges.total)}</td>
            <td className="num">100.00%</td>
          </tr>
        </tfoot>
      </table>
    </div>
  )
}
