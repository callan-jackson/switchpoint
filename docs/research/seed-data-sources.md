# Seed data sources

Every figure in `data/providers.json`, `data/funds.json`, `data/model-portfolios.json`,
`data/assumption-sets.json` and `data/capital-market-assumptions.json` is recorded below with the
source it was taken from and the date it was read.

- **All pages and documents in this file were read on 7 September 2026** unless another date is
  given. That date is the `asAt` on every product charge version and fund record.
- `dataQuality` on a product charge version means:
  - `verified` — the numbers were read from the provider's own published charge sheet (the
    document or page linked in `sourceUrl`);
  - `indicative` — the numbers come from a secondary or third-party copy of the provider's
    disclosure, or the provider does not publish the full structure;
  - `placeholder` — illustrative only (the three legacy personal pensions), never to be used for a
    client recommendation.
- `effectiveFrom` is the commencement date printed on the charge sheet where one is given. Where a
  provider publishes no commencement date, `2026-01-01` is used as a proxy for "the version in force
  at the as-at date" — this is flagged per provider below.
- All monetary fixed charges are recorded **including** VAT where the provider quotes a
  VAT-exclusive figure and VAT is payable (the arithmetic is shown in the charge `description`).
- `expectedFundDealsPerYear` / `expectedEtfDealsPerYear` are left at 0 throughout: the per-deal
  amounts are recorded from the charge sheets, but assuming a turnover rate would be invention.

---

## 1. Platforms and pension providers (`data/providers.json`)

24 providers, 32 products (29 current products marked `verified`/`indicative` plus 3 `placeholder`
legacy contracts).

### AJ Bell Investcentre — `verified`
- Source: <https://www.investcentre.co.uk/sites/default/files/AJBIC_charges_and_rates.pdf>
  ("Platform charges and rates", effective from 2 December 2024; document reference
  AJBIC/P/C&R/20260209), reached from <https://www.investcentre.co.uk/platform/charges>.
- Taken: annual custody charge bands 0.20% to £500,000, 0.175% £500,000–£1m, 0.15% £1m–£1.5m,
  0.075% £1.5m–£2m, nil above £2m. Note 5 of the document states "The annual custody charge for
  each band applies to assets assigned to that band and not the asset value as a whole. The total
  annual custody charge will be a blended rate" — hence `marginal`.
- Also taken: SIPP quarterly administration £50 / £60 / £70 by fund value, waived where £200,000+
  is held in the Funds & Shares Service; flexi-access drawdown £150 a year; online dealing £3.95 a
  deal (bulk dealing / model service nil); standard transfer-out £75; account linking of joint and
  designated accounts for the custody charge calculation (`allowsFamilyLinking: true`).
- Also taken for the model portfolios: AJ Bell Managed Portfolio Service AMC 0.15% a year, Gilt MPS
  AMC 0.10% a year.

### Transact — `verified`
- Source: <https://www.transact-online.co.uk/documents/charges-schedule> (Transact Charges Schedule,
  Integrated Financial Arrangements Ltd; PDF produced 16 June 2026).
- Taken: annual charge table for **portfolios of £100,000 and above** — 0.26% £0–£600,000, 0.17%
  £600,000–£1,200,000, 0.07% £1,200,000–£5,000,000, 0.05% on the remainder; and the separate table
  for **portfolios below £100,000** — 0.50% £0–£60,000, 0.26% above £60,000 to £100,000. Both are
  modelled as separate products. The worked example in the schedule (£1,000,000 portfolio: first
  £600,000 × 0.26% = £1,560 plus remaining £400,000 × 0.17% = £680) confirms `marginal`.
- Also taken: quarterly wrapper administration fee £20 for the SIPP and Personal Pension Plan (one
  fee per wrapper type; a single fee across a linked family group); dealing £3.75 per stock-exchange
  transaction; no charge for transferring investments in or out; family/trust portfolio
  consolidation on request (`allowsFamilyLinking: true`).

### Quilter — `verified`
- Source: <https://www.quilter.com/siteassets/documents/platform/guides-and-brochures/18035-making-the-cost-of-investment-clear.pdf>
  ("Making the cost of investment clear — for investments on Charge Basis 3").
- Taken: Service Charge / Product Charge 0.35% first £50,000, 0.25% £50,000–£250,000, 0.20%
  £250,000–£750,000, 0.15% above £750,000. The document states "These charges work like tax bands"
  and the worked example (£125,000 → 0.29% blended) confirms `marginal`.
- Also taken: dealing charge on ETFs/ETCs/investment trusts — £3.50 per trade under £10,000, 0.035%
  from £10,000 capped at £15 (£1 inside a model portfolio); Family Linking on request; the Product
  Charge applies to the Collective Retirement Account, the Service Charge to the ISA/JISA/CIA.
- `effectiveFrom` is 18 December 2012 — the date the document gives for accounts falling onto
  Charge Basis 3.

### Aegon — `verified`
- Source: <https://www.aegon.co.uk/content/dam/auk/assets/publication/marketing-support/arc-charges-guide.pdf>
  ("Aegon Retirement Choices — charges").
- Taken: annual charge bands 0.60% first £29,999.99, 0.55% £30,000–£49,999.99, 0.50%
  £50,000–£99,999.99, 0.45% £100,000–£249,999.99, 0.00% above £250,000 (an effective cap of £1,215
  a year); the guide's own worked example (£300,000 → £1,215 → 0.405%) confirms `marginal`.
- Also taken: drawdown fee £75 a year, charged once however many income streams are taken;
  stockbroker fee £15 for each sale or purchase of equities and investment trusts.
- Junior products are excluded from the parent's charge calculation, so `allowsFamilyLinking` is
  false. No commencement date is printed — `effectiveFrom` uses the 2026-01-01 proxy.

### Aberdeen (abrdn) Adviser — Wrap and Elevate — `verified`
- Wrap source: <https://www.aberdeenadviser.com/library/wrap-charges-and-discounts---guide.pdf>
  ("Wrap charges guide, April 2026"). Taken: platform charge 0.30% on the first £0–£250,000, 0.20%
  on the next £250,000–£500,000, 0.10% on the balance above £500,000, described in the guide as a
  "'banded' or 'tiered' structure, operating in a similar way to income tax" — hence `marginal`.
  Listed-securities transaction charge £10 under £25,000 (£25 to £99,999, 0.025% above £100,000).
  Family terms link partners' and close family members' Wrap accounts where the combined value
  exceeds £500,000 (`allowsFamilyLinking: true`).
- Elevate source: <https://www.aberdeenadviser.com/library/elevate---your-guide-to-charges.pdf>
  ("Elevate — your guide to charges"). Taken: Elevate Portfolio Charge 0.30% £0–£149,999, 0.25%
  £150,000–£999,999, 0.20% £1m–£2,499,999, 0.15% £2.5m–£4,999,999, 0.10% £5m+. The guide's linking
  example (two £100,000 portfolios each charged 0.30%; linked £200,000 charged **0.25%**) and its
  worked example (£150,000 → £375 a year = exactly 0.25%) show the band rate applies to the whole
  portfolio — hence `wholeOfFund`. Securities trading charge £12.50 per quote-and-deal trade; the
  guide states there are no product charges, switch fees, opening or exit fees.
- The former **Standard Life Wrap** is this platform: it was rebranded abrdn Wrap and then Aberdeen
  Wrap, and is captured by the Wrap product above rather than under Standard Life.

### Fidelity Adviser Solutions — `verified`
- Source: <https://adviserservices.fidelity.co.uk/media/fnw/guides/client-guide-to-fees-and-charges.pdf>
  ("Pricing at a glance"), cross-checked against
  <https://adviserservices.fidelity.co.uk/media/fnw/guides/literature-library/fnw-advisers-charges-and-fees-guide.pdf>.
- Taken: Service Fee flat 0.25% a year of investments (no tiering is published), Investor Fee £45 a
  year collected as £3.75 a month, one fee for all accounts in the client's sole name. Aggregated
  buy/sell of exchange-traded investments through the dealing partner is £0. "Fidelity does not
  charge exit fees when selling or moving your investments to another provider."
- No commencement date is printed — `effectiveFrom` uses the 2026-01-01 proxy.

### Seven Investment Management (7IM) — `verified`
- Source: <https://www.7im.co.uk/media/r55h1p4k/platform-fees-and-charges.pdf>
  ("Fees and charges — Platform Service", September 2024).
- Taken: Platform Service Fee 0.30% on the first £500,000, 0.25% £500,000–£1m, 0.15% £1m–£2m, 0.08%
  £2m–£5m, 0.05% above £5m; the document's £750,000 example (£1,500 + £625 = £2,125 = 0.28%)
  confirms `marginal`. SIPP annual administration fee £0 (£100 + VAT for SIPP accounts below
  £75,000); drawdown ("payment of income") fee £135 + VAT = £162 a year. "7IM do not apply any exit
  fees"; no charge for transferring investments to or from another provider.

### Nucleus Financial Platforms — `indicative`
- Source: <https://nucleusfinancial.com/wrap/clients/our-platform/costs-and-charges>.
- Taken: platform charge 0.33% up to £199,999, 0.30% £200,000–£499,999, 0.175% £500,000–£999,999,
  0.05% £1,000,000+; no charge for transferring money, moving into drawdown or day-to-day fund
  dealing; family account linking.
- Marked `indicative` because the page does not state whether the band rate applies to the slice or
  to the whole portfolio; SwitchPoint models it as `marginal`.
- **James Hay**: the James Hay products were upgraded onto the Nucleus Platform
  (<https://nucleusfinancial.com/advisers/support/simplified-pricing>). The former James Hay Modular
  iSIPP is seeded as "Nucleus Modular iSIPP (formerly James Hay Modular iSIPP)" and is `indicative`:
  Nucleus publishes the *shape* of the new pricing (a three-, four- or five-tier platform charge,
  module charges for off-platform assets, an income drawdown charge) but the rates are issued to
  each client in a personal charges schedule, not published on the website. The drawdown charge of
  £180 a year is an estimate, not a published figure.

### Wealthtime (Novia Financial plc, FRN 481600) — `verified`
- Source: <https://www.wealthtime.com/wp-content/uploads/sites/7/2026/05/WT-Charges-schedule_0526-1.pdf>
  ("Charges schedule, effective 1 May 2025", document ref WT-CS-0526).
- Taken: annual charge 0.30% £0–£500k, 0.20% £500k–£1m, 0.10% £1m+, described as "a combined rate
  across the total value" — `marginal`; minimum £100 a year. SIPP income drawdown / UFPLS charge
  £62.50 + VAT = £75 a year. Novia Stockbroker account 0.30% of trade, minimum £15, maximum £75.
  FCA firm reference number 481600 is printed on the schedule.

### Parmenion — `verified`
- Source: <https://parmenion.co.uk/charges/>.
- Taken: platform/custody charge 0.30% £0–£299,999.99, 0.25% £300,000–£599,999.99, 0.20%
  £600,000–£1,499,999.99, 0.15% £1,500,000+, described by Parmenion as a "cliff edge" structure in
  which the rate applies to all assets once a threshold is reached — hence `wholeOfFund`. SIPP
  administration £18 + VAT a quarter; minimum £5 a month per client over 18; portfolio transaction
  costs of about 0.05% a year (0.45% per transaction on roughly 12% annual turnover) recorded as
  `transactionCostsPct`. Discretionary management is 0.12% passive / 0.24% active — used for the
  Parmenion model portfolios rather than the platform product.
- No commencement date is published — `effectiveFrom` uses the 2026-01-01 proxy.

### Standard Life — `verified`
- Source: <https://www.standardlife.co.uk/pensions/personal-pension/charges> ("Figures correct as at
  13 August 2026").
- Taken: Personal Pension ready-made option — Service Charge 0.45% plus a Total Fund Charge of
  0.10%, a total of 0.55%. Modelled as a whole-of-fund product charge of 0.45% with an explicit
  fund charge of 0.10%.
- The Standard Life **Wrap** platform is now Aberdeen Wrap (see Aberdeen above).

### M&G Wealth Platform (Investment Funds Direct Limited, FRN 114432) — `verified`
- Source: <https://www.mandg.com/wealth/platform/your-platform/pricing-and-charging>.
- Taken: 0.30% up to and including £1m, 0.10% £1m–£3m, 0.06% £3m–£5m (terms on request above £5m);
  minimum fee £15 a month; "no SIPP fees, no drawdown fees, no model portfolio fees and no exit
  fees"; unlimited in-house dealing at no extra charge; family linking discount. The £15 a month
  minimum is documented here but not seeded as a fixed charge, because it is a floor on the
  percentage charge rather than an addition to it.
- FRN 114432 is printed in the page footer. No commencement date is published — `effectiveFrom`
  uses the 2026-01-01 proxy.

### Scottish Widows Platform — `verified`
- Source: <https://platform.scottishwidows.co.uk/wp-content/uploads/Client_Charges_Guide.pdf>
  ("Client guide to Platform charges").
- Taken: Ongoing Platform Charge "at each band" — 0.35% on the first £100,000, 0.30% £100,000–
  £250,000, 0.25% £250,000–£500,000, 0.10% above £500,000; the guide's £200,000 example
  (£350 + £300 = £650) confirms `marginal`. Pension Account Charge £75 a year (£6.25 a month).
  Stockbroker partner trading charge 0.07% subject to a £7.50 minimum and £120 maximum (£1 inside a
  discretionary model). Family linking where the combined value is £200,000 or more.

### Royal London — `verified`
- Source: <https://adviser.royallondon.com/globalassets/docs/adviser/guides/65g2035-pensions-and-investments-charges-guide.pdf>
  ("Clear charges — Pensions and Investments").
- Taken: basic charge 1.00% a year less a tiered management charge discount, giving an AMC of
  0.75% (£0–£48,300), 0.50% (£48,301–£96,700), 0.45% (£96,701–£290,000), 0.40% (£290,001–£967,000)
  and 0.35% (£967,001+). The discount produces a single AMC applied to the whole fund — hence
  `wholeOfFund`. The guide notes discount thresholds are increased each 6 April in line with RPI,
  and that the discount on new Pension Portfolio plans rose from 0.10% to 0.25% on 6 April 2025 —
  the date used for `effectiveFrom`.
- Additional investment charges apply to external funds; these are not seeded.

### Prudential (M&G) Retirement Account — `verified`
- Source: <https://www.mandg.com/assets/shared/documents/en/rack164801.pdf> (Retirement Account key
  features).
- Taken: product charge 0.30% below £99,999, 0.20% £100,000–£249,999, 0.15% £250,000–£749,999,
  0.125% £750,000–£999,999, 0.10% £1,000,000+. Both worked examples in the document apply the band
  rate to the entire Retirement Account value — hence `wholeOfFund`. "We do not charge you for
  transferring to a new arrangement" (no exit penalty).
- Background on the charge mechanics: <https://www.mandg.com/assets/shared/documents/en/pruf100810912.pdf>
  ("A guide to the costs and charges associated with investing in PruFund").
- No commencement date is printed — `effectiveFrom` uses the 2026-01-01 proxy.

### Aviva — `verified`
- Source: <https://connect.avivab2b.co.uk/adviser/retirement/individual-pensions/pension-portfolio/>
  ("Standard charges for new customers").
- Taken: "Aviva charge by portfolio value" — Core option 0.35% / 0.30% / 0.20% / 0.10% and Choice
  option 0.40% / 0.35% / 0.25% / 0.15% at the bands up to £30,000, £30,000.01–£250,000,
  £250,000.01–£400,000 and £400,000.01+. Both options are seeded as separate products (Core is
  `restricted`, Choice `wholeOfMarket`). Equity trading through Winterflood is "No charge" for both
  individual trades and trades within a model portfolio.
- Aviva's charge-terminology factsheet
  (<https://static.aviva.io/content/dam/document-library/adviser/adviserplatform/lf01139c.pdf>,
  LF01139 12.25) confirms the advised platform charges only "the Aviva charge" plus an equity
  dealing charge, with no wrapper charge and no drawdown fee.
- The tiered discount is based on the client's own holdings across the ISA, Investment and Pension
  Portfolios, so `allowsFamilyLinking` is false.

### Hargreaves Lansdown — `verified`
- Source: <https://www.hl.co.uk/pensions/sipp/charges-and-interest-rates>.
- Taken: annual account charge on funds 0.35% up to £250,000, 0.25% £250,000–£1m, 0.10% £1m–£2m,
  no charge above £2m; shares and other equities 0.35% capped at £12.50 a month; fund dealing
  £1.95 a deal (free for monthly regular investing); share/ETF dealing £6.95 for 0–19 trades a
  month, £3.95 for 20+; no exit fees. The £12.50 a month cap on the shares/ETF element cannot be
  expressed in the charge schedule and is noted here instead.
- Minimum investment £100 lump sum / £25 a month.

### interactive investor — `verified`
- Source: <https://www.ii.co.uk/our-charges>.
- Taken: Core plan £5.99 a month for portfolios up to £100,000; Plus plan £14.99 a month, no
  portfolio limit (an account automatically moves to Plus once it exceeds £100,000); Premium
  £39.99 a month. All plans include the Personal Pension (SIPP), ISA and Trading Account. Trading:
  Core £3.99 funds and UK/US shares (including ETFs and investment trusts); Plus £1.49 funds,
  £3.99 UK/US shares. No transfer charges in or out. Core and Plus are seeded as separate products.

### Vanguard Investor UK — `verified`
- Source: <https://www.vanguardinvestor.co.uk/what-we-offer/fees-explained>.
- Taken: account fee 0.15% a year capped at £375 a year (modelled as 0.15% to £250,000 and nil
  above), or £4 a month (£48 a year) for balances under £32,000. The £4 a month floor is documented
  here rather than seeded, because it replaces rather than adds to the percentage charge. The fund
  universe is restricted to Vanguard funds (fund management costs 0.06%–0.79%).

### PensionBee — `verified`
- Source: <https://www.pensionbee.com/uk/plans>.
- Taken: "Pay just one annual pension fee ranging from 0.50% to 0.95%" and "Pay half the annual fee
  on the portion of your savings over £100,000. For example, if you have £250,000 in the Global
  Leaders Plan, you'll pay 0.70% on the first £100,000 but only 0.35% on the remaining £150,000" —
  seeded as the Global Leaders Plan at 0.70% / 0.35% on a `marginal` basis. Combining pensions and
  switching plans are free.

### True Potential — `verified`
- Source: <https://www.tpinvestor.com/our-fees> (fee calculator).
- Taken: Ongoing Platform Fee 0.40%; typical Ongoing Investment Charge 0.75% (not seeded — it is a
  fund charge, not a product charge); Ongoing Advice Fee 0.50% for the Central Advice Service (not
  seeded). True Potential Wealth Management LLP offers restricted advice — FRN 529810, from the
  same site's footer.

### Fundment — `indicative`
- Source: <https://www.zedra.com/wp-content/uploads/2025/09/Fundment_costs_and_charges_2024.pdf>
  ("Fundment Personal Pension Disclosure of Costs & Charges 2024", published September 2025 and
  hosted by an adviser firm): "Platform fees also apply to the Fundment Personal Pension. This is
  also known as the administration cost. This cost was 0.2%."
- Marked `indicative`: the figure is a historic disclosure republished by a third party, not
  Fundment's live charge sheet, and Fundment does not publish a public pricing page
  (<https://www.fundment.com/en_gb/platform> carries no rates).

### SS&C Hubwise (Hubwise Securities Limited, FRN 502619) — `indicative`
- Source: <https://www.ssctech.com/about/disclosures/ssc-hubwise-schedule-of-charges>.
- Taken: Hubwise SIPP product charge 10 bps capped at £50 + VAT; drawdown £125 + VAT = £150 a year;
  GIA and ISA/JISA product charge £0; Offshore Bond 20/10/5 bps with a £250 minimum; FRN 502619.
- Marked `indicative` because the headline **platform (custody) charge** is not published: the
  schedule says it "is detailed within your Terms and Conditions", so the seeded product carries
  only the published SIPP product charge and drawdown fee.

### Heritage Life Assurance (illustrative legacy book) — `placeholder`
Three synthetic legacy contracts, marked `placeholder` and carrying no `sourceUrl`. They exist so
that the switching analysis has realistic ceding-scheme shapes (whole-of-fund AMC, monthly policy
fee, bid/offer spread, allocation rate below 100% and a declining exit penalty) and must never be
used for a client recommendation:

| Product | AMC (whole of fund) | Policy fee | Bid/offer spread | Allocation rate | Exit penalty |
|---|---|---|---|---|---|
| Retirement Annuity Contract (1978 series) | 1.50% | £2.50/month, +5% a year | 5% | 95% | 8% / 6% / 4% / 2% to years 1–4 |
| Personal Pension Plan (1994 series, unit-linked) | 1.25% | £3.50/month, +5% a year | 5% | 97% | 5% / 3% / 1% to years 2, 4 and 6 |
| Personal Pension Plan (2001 series, stakeholder-priced) | 1.00% | £1.50/month, +5% a year | 5% | 100% | 4% / 2% to years 3 and 5 |

---

## 2. Funds (`data/funds.json`)

87 funds and ETFs.

### ISIN validation
Every ISIN was checked twice before writing:
1. **Check digit** — a Python implementation of the ISO 6166 Luhn routine (expand letters A–Z to
   10–35, then Luhn over the resulting digit string). All 87 pass; the seeder rejects any that do
   not, and the seeding run produced no "invalid ISIN" warnings.
2. **Identity** — each ISIN was mapped through the OpenFIGI v3 mapping API
   (<https://api.openfigi.com/v3/mapping>) and the returned security name compared with the name
   seeded. Two names were corrected as a result:
   - `GB00B18B9X76` is **WS** Lindsell Train UK Equity Fund (formerly LF Lindsell Train);
   - `GB00B702WG47` is now the **HSBC Global Listed Real Assets Fund** (formerly HSBC Global
     Property Fund).
   One ISIN, `GB0002051844` (Fidelity UK Gilt Fund W Inc), returns "no identifier found" from
   OpenFIGI; it is retained because it appears with that name in Fundment's published costs and
   charges disclosure and its check digit is valid.
3. The ISINs the task required to be kept were checked against their true names rather than assumed:
   `GB00B3X7QG63` is the **Vanguard FTSE U.K. All Share Index Unit Trust Accumulation** (not a
   LifeStrategy fund — LifeStrategy 60% Equity A Acc is `GB00B3TYHH97`, which is also seeded), and
   `GB00B41YBW71` is the **Fundsmith Equity Fund I Acc** (share class I, not T). `GB00B4PQW151`,
   `GB00B3ZHN960`, `GB00B41XG308`, `IE00B4L5Y983` and `IE00B3XXRP09` are as expected.

### ISINs and transaction costs
- Primary source for 79 of the 87 funds:
  <https://www.zedra.com/wp-content/uploads/2025/09/Fundment_costs_and_charges_2024.pdf> —
  "Fundment Personal Pension Disclosure of Costs & Charges 2024" (published September 2025), a
  PRIIPs ex-post disclosure listing ISIN, full fund name and transaction costs as at 31 December
  2024 for every fund on the Fundment platform. `transactionCostsPct` is taken from this table
  (negative values reported by the disclosure are floored at zero).
- The remaining 8 (iShares Core MSCI World, iShares Core FTSE 100, SPDR S&P 500, Fundsmith Equity,
  Lindsell Train Global Equity, Jupiter European, Jupiter India, L&G Multi-Index 5) carry the
  manager's own site as `sourceUrl` and an estimated transaction cost.

### Ongoing charges figures
`ocfPct` is the manager's published ongoing charges figure for the share class named, recorded as
**indicative**: it was not re-read from each individual KIID/factsheet on 7 September 2026. Refresh
these from a fund data feed (the Morningstar connector) before any figure is used in client-facing
output. `factsheetUrl` points at the FT tearsheet for each ISIN
(`https://markets.ft.com/data/funds/tearsheet/summary?s=<ISIN>:GBX`, or `/data/etfs/...` for ETFs)
so a reviewer can check OCF, price and performance per fund.

### Coverage
Vanguard LifeStrategy 20/40/60/80/100; Vanguard UK, global, regional and emerging-market index
funds and ETFs; Vanguard gilt, index-linked gilt, long-duration gilt, investment-grade, global bond
and sterling money market funds; HSBC Global Strategy Adventurous and Dynamic plus the HSBC index
range (All-World, All Share, American, European, Japan, Pacific, UK Gilt, Sterling Corporate Bond)
and HSBC Global Listed Real Assets; L&G Multi-Index 5; BlackRock MyMap 6 and the iShares UK index
fund range (corporate bond, gilts all-stocks, index-linked gilt, Japan equity, mid-cap UK equity,
real estate); iShares Core MSCI World, Core FTSE 100, Core MSCI Pacific ex-Japan, UK Gilts 0–5yr and
Physical Gold; SPDR S&P 500 and SPDR Bloomberg Global Aggregate; Invesco GBP Corporate Bond ESG and
UK Gilts; Fidelity Multi Asset Allocator/Open Adventurous, Fidelity Index World/UK and Fidelity UK
Gilt; Royal London Sustainable Leaders, Sustainable World, Corporate Bond and Short Term Money
Market; Baillie Gifford American, Managed, Positive Change and Japanese; Fundsmith Equity; WS
Lindsell Train UK Equity and Lindsell Train Global Equity; Liontrust Sustainable Future Managed
Growth and UK Smaller Companies; Artemis Income, UK Select and Short-Duration Strategic Bond;
Jupiter European and India; Rathbone Global Opportunities, Ethical Bond and Multi Asset Total
Return; M&G Corporate Bond; Schroder Sterling Corporate Bond; abrdn Sterling Money Market.

---

## 3. Model portfolios (`data/model-portfolios.json`)

10 portfolios across three discretionary managers, each with a **published** management fee.
Holdings are drawn from `data/funds.json` and weights total 100 in every case. The **fee** is the
sourced figure; the **asset mix and fund selection are SwitchPoint's own illustration**, not the
manager's published model, and are documented as such.

| Provider | Portfolios | Fee | Source |
|---|---|---|---|
| AJ Bell Asset Management | Cautious, Balanced, Adventurous | 0.15% a year | AJ Bell Investcentre platform charges and rates PDF (above), "Managed Portfolio Service (MPS) charges — Annual management charge (AMC) 0.15% p.a." |
| AJ Bell Asset Management | Gilt Managed Portfolio Service | 0.10% a year | Same PDF, "Gilt MPS charges — Annual management charge (AMC) 0.10% p.a. There are no underlying charges for the investments within the portfolio." |
| Parmenion Investment Management | Strategic Passive Risk Grade 3 and 5 | 0.12% a year | <https://parmenion.co.uk/charges/> — "Discretionary Investment Management: Passive 0.12%" |
| Parmenion Investment Management | Strategic Active Risk Grade 7 | 0.24% a year | Same page — "Active 0.24%" |
| Tatton Investment Management | Tracker Balanced, Core Aggressive, Managed Cautious | 0.15% a year | <https://tattoninvestments.com/our-products/> — "With a fee of 0.15% per annum across all strategies" |

The MPS providers are seeded under their asset-management names ("AJ Bell Asset Management",
"Parmenion Investment Management") so that they do not collide with the platform providers of the
same group in `providers.json`.

**Seeder note.** `DataSeeder.SeedModelPortfoliosAsync` reads providers and funds from the database
before `SaveChangesAsync` runs, so on a *completely empty* database the model portfolios find no
funds and are skipped with a warning. Seeding is idempotent, and the second start of the API against
the same database creates all 10 portfolios with their holdings and logs no warnings. This is a
seeder ordering issue, not a data issue.

---

## 4. Assumption sets (`data/assumption-sets.json`)

Two sets, both for tax year 2026/27 on a real (`real`) projection basis with ONS National Life
Tables 2020–22 mortality.

### FCA standard 2026/27 (`isFcaStandard: true`)
Growth 2% / 5% / 8%; price inflation 2.0%; RPI 3.0%; earnings growth 3.5%; charge inflation 2.0%;
pre-retirement product charge 0.4%; annuity expense loading 4.0%; spouse age gap 3 years; state
pension increase 3.5%. These are the COBS 13 Annex 2, COBS 19 Annex 4C and AS TM1 v5.2 values
already recorded in `data/fca-assumptions.json`, which cites
<https://www.handbook.fca.org.uk/handbook/COBS/13/Annex2.html>,
<https://www.handbook.fca.org.uk/handbook/COBS/19/Annex4C.html> and
<https://www.frc.org.uk/documents/8999/AS_TM1_Statutory_Money_Purchase_Illustrations_v5.2.pdf>.

### Market inputs — as at 7 September 2026
COBS 19 Annex 4C 2R requires the FTSE Actuaries UK gilt fixed-coupon yield for each term band. The
FTSE Actuaries indices are licensed, so a **public proxy** is used and the derivation is recorded
here.

- Proxy source: <https://www.giltcalculator.co.uk/gilts> — live redemption yields for every
  conventional UK gilt, read on 7 September 2026. (Tradeweb's FTSE Gilt Closing Prices page, the
  usual public route to the same data, blocks automated access; the Bank of England's yield-curve
  spreadsheets are the other public cross-check.)
- Derivation: gilts were bucketed by years to maturity from 7 September 2026 and the mean
  redemption yield of each bucket taken, excluding strips and gilts within weeks of redemption
  (whose quoted yields are artefacts):

  | Term band (COBS 19 Annex 4C 2R) | Gilts in band | Mean redemption yield | Seeded |
  |---|---|---|---|
  | ≤ 5 years | 18 | 4.383% | `giltYieldUpTo5Pct` 4.38 |
  | 5–10 years | 15 | 4.877% | `giltYield5To10Pct` 4.88 |
  | 10–15 years | 9 | 5.333% | `giltYield10To15Pct` 5.33 |
  | > 15 years | 16 | 5.702% | `giltYieldOver15Pct` 5.70 |

- **`cobs13YPct` = 2.0%.** COBS 13 Annex 2 3.1R(6) defines Y as
  `0.5 × (ILG0 + ILG5) − 0.5%`, rounded to the nearest 0.2%, where ILG0 and ILG5 are the FTSE
  Actuaries index-linked over-5-year real yields on the 0% and 5% future-inflation bases. Taking the
  over-15-year nominal proxy of 5.70% against the COBS 13 Annex 2 RPI assumption of 3.0% gives an
  implied real yield of (1.0570 ÷ 1.0300) − 1 = 2.62%; allowing the customary ~0.2 percentage point
  gap between the 0% and 5% inflation bases gives ILG0 ≈ 2.60% and ILG5 ≈ 2.40%, a mean of 2.50%.
  Y = 2.50% − 0.50% = **2.00%**, which is already a multiple of 0.2%.
- **`tvcAnnuityRateRpiLinkedPct` = 2.0%.** The COBS 13 Annex 2 intermediate spread over Y for an
  RPI-linked annuity is 0.0% (`rpiAnnuitySpreadOverY.intermediate` in `data/fca-assumptions.json`),
  so the rate is Y itself. COBS 19 Annex 4C 1R(2)(a) adopts this rate for the TVC.
- **`tvcAnnuityRateLevelPct` = 5.5%.** The intermediate spread over Y for a level or fixed-increase
  annuity is 3.5% (`levelAnnuitySpreadOverY.intermediate`), so the rate is 2.0% + 3.5% = 5.5%
  (COBS 19 Annex 4C 1R(2)(b)).
- The CPI-linked TVC rate is derived by the domain as the RPI-linked rate + 1.0%
  (COBS 19 Annex 4C 1R(2)(c)) — 3.0% on these inputs.
- Caveat: COBS 13 Annex 2 sets Y from the index values on 15 February and applies the annuity rates
  as a three-month average. The figures above are a single-day snapshot on a proxy index, so they
  are directionally right but are **not** a compliant reproduction of the FTSE Actuaries series.
  Replace them with licensed FTSE Actuaries data before any figure reaches a client document.

### Cautious firm default (`isFcaStandard: false`)
Growth 1% / 4% / 7%, earnings growth 3.0%, state pension increase 3.0%; all other values and the
same market inputs as the FCA standard set. This is a firm-level house view, not a published
figure — it exists so the UI has a non-FCA set to copy and edit.

---

## 5. Capital market assumptions (`data/capital-market-assumptions.json`)

Seven asset classes in domain enum order, with a 7×7 correlation matrix. As at 7 September 2026.

| Asset class | Expected return (nominal) | Volatility |
|---|---|---|
| UK equity | 8.0% | 16.0% |
| Global equity | 7.5% | 15.0% |
| Government bonds | 5.3% | 8.0% |
| Corporate bonds | 6.0% | 7.0% |
| Property | 6.5% | 13.0% |
| Cash | 3.8% | 1.0% |
| Alternatives | 6.0% | 10.0% |

- **Anchor.** The bond and cash returns are built from the measured UK gilt curve above
  (<https://www.giltcalculator.co.uk/gilts>, 7 September 2026): government bonds take the ~5.3%
  mean redemption yield of the 10–15 year and shorter buckets blended to a medium duration, cash
  takes the short end less a margin, and corporate bonds add a ~0.7pp investment-grade credit
  spread.
- **Risk premia and volatilities** are long-run UK and world figures from the UBS/London Business
  School *Global Investment Returns Yearbook* and the *Barclays Equity Gilt Study* — the two public
  long-run UK series — rounded to the nearest 0.5pp. Correlations are long-run sterling-investor
  estimates over the same series.
- **Positive definiteness** was proved before writing the file by running a Cholesky decomposition
  in pure Python; every pivot is strictly positive (diagonal 1.000, 0.527, 0.993, 0.655, 0.887,
  0.975, 0.820). The matrix is symmetric with a unit diagonal and all entries within [−1, 1], which
  the domain also enforces. A stochastic cashflow run against the seeded database
  (`POST /api/v1/calculations/cashflow/stochastic`, 200 paths, seed 42) completed successfully,
  confirming the engine can decompose it.
- These are a documented house build-up, not a vendor's published capital market assumptions.
  Replace them with a licensed set before using stochastic output in client documents.

---

## 6. Verification run

```
Seed__DataDirectory=/Users/cj/switchpoint/data  Seed__Demo=true  port 5131  fresh SQLite database
```

- First start: providers, products, funds and assumption sets seeded; model portfolios skipped with
  the "fund … not in catalogue" warnings described above (seeder ordering).
- Second start against the same database: **zero warnings** in the log — no "Skipping fund",
  no "Skipping charge version", no "not in catalogue".
- `GET /api/v1/providers` → 27 providers (24 seeded plus the three discretionary managers created
  from the model portfolios).
- `GET /api/v1/products` → 32 products, all with a current charge version and computed effective
  charges at £100k and £500k.
- `GET /api/v1/funds` → 87 funds; `GET /api/v1/funds/{isin}` returns the full record.
- `GET /api/v1/model-portfolios` → 10 portfolios with all holdings resolved.
- `GET /api/v1/assumption-sets` → 2 sets with the market inputs above.

---

## 7. Providers that could not be fully verified

| Provider | What is missing | Why |
|---|---|---|
| Nucleus Financial Platforms | Whether the published tiers are marginal or whole-of-fund | Not stated on the costs-and-charges page |
| James Hay (as Nucleus Modular iSIPP) | All rates | Nucleus issues a client-specific charges schedule; only the pricing *structure* is public |
| SS&C Hubwise | Headline platform/custody charge | Set in each firm's terms and conditions, not published |
| Fundment | Current platform rate | No public pricing page; the 0.20% figure is a 2024 disclosure republished by a third party |
| Aviva, Aegon, Fidelity, Parmenion, M&G Wealth, Scottish Widows, Prudential, Standard Life, HL, ii, Vanguard, PensionBee, True Potential, Quilter (CRA) | Commencement date of the current charge sheet | No `effectiveFrom` printed; the 2026-01-01 proxy is used |
| All providers except Wealthtime, M&G Wealth, SS&C Hubwise and True Potential | FCA firm reference number | Left null rather than guessed; populate from the FCA register |
| All funds | Ongoing charges figures | Indicative, not re-read from each KIID on the as-at date |
| FCA market inputs | FTSE Actuaries gilt and index-linked index values | Licensed data; a public gilt-yield proxy is used and the derivation is set out in section 4 |
