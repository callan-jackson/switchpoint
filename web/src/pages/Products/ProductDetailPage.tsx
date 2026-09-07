import { useState } from 'react'
import { useParams } from 'react-router'
import { ExternalLink } from 'lucide-react'
import { useProduct } from '@/api/queries'
import { errorMessage } from '@/api/client'
import { annualChargeBreakdown } from '@/lib/charges'
import { date, fmtPct, gbp } from '@/lib/format'
import { fundUniverseLabels } from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Badge, DataQualityBadge } from '@/components/ui/Badge'
import { ButtonLink } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { DefinitionList } from '@/components/ui/DefinitionList'
import { PageHeader } from '@/components/ui/PageHeader'
import { PageSkeleton } from '@/components/ui/Skeleton'
import { Field } from '@/components/form/Field'
import { MoneyInput, PercentInput } from '@/components/form/NumericInput'

/**
 * One product's charge versions plus a client-side "what would this cost at £X" calculator.
 *
 * The calculator mirrors the domain's charge rules closely enough for a conversation; every
 * figure that reaches a report still comes from the API.
 */
export default function ProductDetailPage() {
  const { productId = '' } = useParams()
  const { data: product, isLoading, error } = useProduct(productId)
  const [value, setValue] = useState<number | null>(250_000)
  const [ocf, setOcf] = useState<number | null>(0.2)
  const [versionIndex, setVersionIndex] = useState(0)

  if (isLoading) return <PageSkeleton />
  if (error || !product) {
    return (
      <Alert tone="danger" title="Product not found">
        {errorMessage(error, 'That product is not in the catalogue.')}
      </Alert>
    )
  }

  const version = product.chargeVersions[versionIndex] ?? product.chargeVersions[0]
  const breakdown = version ? annualChargeBreakdown(version.charges, value ?? 0, ocf ?? 0) : null

  return (
    <>
      <PageHeader
        eyebrow={product.providerName}
        title={product.name}
        description={`Effective charge ${fmtPct(product.effectiveChargePctAt100k, 3)} at £100,000 and ${fmtPct(product.effectiveChargePctAt500k, 3)} at £500,000.`}
        meta={
          <>
            <DataQualityBadge quality={product.dataQuality} />
            <Badge>{fundUniverseLabels[product.fundUniverse]}</Badge>
            {product.allowsFamilyLinking && <Badge tone="accent">Family linking</Badge>}
            <Badge tone="neutral">As at {date(product.asAt)}</Badge>
          </>
        }
        actions={
          <ButtonLink to="/products" variant="outline">
            All products
          </ButtonLink>
        }
      />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title="Product">
          <DefinitionList
            items={[
              { label: 'Provider', value: product.providerName },
              { label: 'Minimum investment', value: gbp(product.minimumInvestment) },
              { label: 'Wrappers', value: product.wrapperTypes.join(', ') },
              { label: 'Fund universe', value: fundUniverseLabels[product.fundUniverse] },
              { label: 'Current charge version', value: product.currentChargeVersion },
              {
                label: 'Source',
                wide: true,
                value: product.sourceUrl ? (
                  <a
                    href={product.sourceUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1 text-primary-700 underline"
                  >
                    {product.sourceUrl}
                    <ExternalLink className="size-3.5" aria-hidden="true" />
                  </a>
                ) : (
                  'Not recorded'
                ),
              },
            ]}
          />
        </Card>

        <Card title="Charge at a value" description="Indicative first-year charges on this product.">
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Fund value">
              <MoneyInput value={value} dp={0} onChange={setValue} />
            </Field>
            <Field label="Weighted OCF of the holdings" hint="Used when the product takes its fund charge from holdings.">
              <PercentInput value={ocf} onChange={setOcf} />
            </Field>
          </div>

          {breakdown && (
            <div className="mt-4 overflow-x-auto">
              <table className="table table-dense">
                <caption className="px-3 py-2 text-left text-sm text-fg-muted">
                  Annual charges at {gbp(breakdown.value)}
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Charge</th>
                    <th scope="col" className="text-right">
                      £ per year
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {(
                    [
                      ['Platform', breakdown.platform],
                      ['Product', breakdown.product],
                      ['Fixed fees', breakdown.fixed],
                      ['Fund (OCF)', breakdown.fund],
                      ['Transaction costs', breakdown.transaction],
                      ['Adviser ongoing', breakdown.adviserOngoing],
                      ['Dealing', breakdown.dealing],
                      ['Switching', breakdown.switch],
                      ['Discounts', breakdown.discounts],
                    ] as const
                  )
                    .filter(([, amount]) => Math.abs(amount) > 0.005)
                    .map(([label, amount]) => (
                      <tr key={label}>
                        <th scope="row" className="font-normal">
                          {label}
                        </th>
                        <td className="num">{gbp(amount, { dp: 2 })}</td>
                      </tr>
                    ))}
                </tbody>
                <tfoot>
                  <tr>
                    <th scope="row" className="font-semibold">
                      Total ({fmtPct(breakdown.totalPct, 3)})
                    </th>
                    <td className="num font-semibold">{gbp(breakdown.total, { dp: 2 })}</td>
                  </tr>
                  <tr>
                    <th scope="row" className="font-normal text-fg-muted">
                      Product and platform only ({fmtPct(breakdown.productOnlyPct, 3)})
                    </th>
                    <td className="num text-fg-muted">
                      {gbp(breakdown.platform + breakdown.product + breakdown.fixed + breakdown.discounts, { dp: 2 })}
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          )}
        </Card>
      </div>

      <Card className="mt-4" title="Charge versions" description="Each version records when it applied and where it came from.">
        <ul className="space-y-2">
          {product.chargeVersions.map((v, index) => (
            <li key={v.version}>
              <button
                type="button"
                onClick={() => setVersionIndex(index)}
                aria-pressed={index === versionIndex}
                className={`flex w-full flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-left ${
                  index === versionIndex ? 'border-accent-500 bg-accent-50' : 'border-border hover:bg-surface-muted'
                }`}
              >
                <span className="text-sm font-medium text-fg">Version {v.version}</span>
                <span className="text-xs text-fg-muted">
                  {date(v.effectiveFrom)} → {v.effectiveTo ? date(v.effectiveTo) : 'current'} · captured {date(v.asAt)}
                </span>
                <DataQualityBadge quality={v.dataQuality} />
                {v.sourceUrl && (
                  <a
                    href={v.sourceUrl}
                    target="_blank"
                    rel="noreferrer"
                    onClick={(e) => e.stopPropagation()}
                    className="inline-flex items-center gap-1 text-xs text-primary-700 underline"
                  >
                    Source
                    <ExternalLink className="size-3" aria-hidden="true" />
                  </a>
                )}
              </button>
            </li>
          ))}
        </ul>
      </Card>
    </>
  )
}
