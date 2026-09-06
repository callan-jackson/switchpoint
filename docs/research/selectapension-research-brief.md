# Selectapension research brief

Prepared for the SwitchPoint build on 6 September 2026. Statements are cited to the numbered
sources at the end; anything marked **[likely]** rests on a search snippet or a single secondary
page, and anything marked **[unconfirmed]** could not be verified against a reachable primary source.
The structured findings of the six research angles (company, products, integrations, regulation,
tax, competitors) are in `appendix-raw-findings.md`; the machine-readable digest of this brief is
`domain-facts.json`; the verified numeric parameters live in `data/tax-years/2026-27.json` and
`data/fca-assumptions.json`.

## 1. Executive summary

Selectapension Limited is a 22-person, Crowborough-based software publisher (Companies House
05075441, incorporated 16 March 2004) that sells a browser-based research-to-recommendation suite to
UK financial advisers and paraplanners [1][7]. Since December 2020 it has been owned by the Perseus
operating group of Toronto-listed Constellation Software, held through Ibcos Computers Limited of
Poole [4][7][9]. Turnover has fallen for three consecutive years (£2.63m in FY2022 to £2.28m in
FY2024) while operating profit recovered to £0.60m in FY2024 as headcount dropped from 29 to 22 [7][8].

The product is organised as three pillars, Switching, Cashflow & Drawdown Reviews and Portfolio
Insight, plus Defined Benefit Transfers, projections, a pay-per-report bureau and provider services
[22]. Its calculation set is the full UK "switching stack": standardised projections at the COBS 13
rates, reduction in yield, a rate-of-return-required (critical yield) comparison of ceding and
receiving plans, death-benefit capital values, an APTA-with-TVC defined benefit module with single
and joint-life critical yields, hurdle rates and PPF yields, and a deterministic cashflow modeller
with historical stress events [32][35][36][37][41]. Only O&M Profiler ESP (Iress) offers a comparable
range; Defaqto, FE fundinfo and Synaptic stop at cost and RIY comparisons [125][129][131][135].

The data ecosystem is small and well documented: Morningstar for 70,000+ funds and c.90 DFM model
portfolio ranges, AKG for with-profits, Assureweb for annuity quotes, Intelliflo Office, Iress
Xplan, Plannr, True Potential and 4admin for client and plan import and report write-back, and the
Origo Integration Hub for its Quote & Apply flow [20][24][25][61]. The regulatory core a rebuild must
encode is COBS 19.1 (APTA and TVC, with the Annex 4C assumptions), COBS 13 Annexes 2 to 4
(projections, effect-of-charges tables, RIY), the FSA 2009 switching template, COBS 19.1B contingent
charging, COBS 9.4 suitability reports, Consumer Duty fair value, AS TM1 v5.2 and the PRIIPs-to-CCI
transition [73][76][82][84][86][89][91][93][96][99].

SwitchPoint's Domain and Calculation layers already cover every calculation Selectapension
advertises except PPF-basis yields, AKG financial-strength data and live annuity quotes, and they add
three things Selectapension does not appear to have: a stochastic engine, a tamper-evident audit
chain and a versioned product-charge catalogue (section 9). Technically, Selectapension is a
long-lived ASP.NET Web Forms application on IIS in Microsoft's London Azure region, fronted by a
WordPress site and Pardot marketing automation (section 8), which is where a modern .NET 10 and
React rebuild has the clearest architectural advantage.

## 2. Company profile

**Legal entity.** Selectapension Limited, company number 05075441, private limited company,
incorporated 16 March 2004, active, SIC codes 58290 (other software publishing) and 62012 (business
and domestic software development) [1]. Registered office: Pine Grove Enterprise Centre, Pine Grove,
Crowborough, East Sussex TN6 1DH, moved there from "Selectapension House", Eridge Road on 30 January
2023 [2]. Last accounts to 31 December 2024 were filed on 5 December 2025; the next are due 30
September 2026 [2].

**Founding and early history.** Founded in 2004 by Andy McCabe, formerly of Legal & General's pension
transfer systems, who remortgaged his house to build "a cheap online system which had everybody's
products on it. A compare the market type of system"; the tool passed 3,000 users by August 2013
[13]. A management buy-out in July 2016 that returned the company to founder control is reported by
a now-offline article **[unconfirmed]** [154]; Companies House shows only that McCabe held over 50%
of the holding company Selectapension (2013) Limited from April 2016 and Helen McCabe 25 to 50% from
October 2020 [6].

**Ownership.** On 11 December 2020 the group was sold, for an undisclosed price, to the Perseus
operating group of Constellation Software Inc. (TSX: CSU), Perseus's first UK pensions and
investments acquisition; Chia Loh (VP, Perseus) and Andy McCabe were quoted, and the Crowborough
office was retained [9][10]. Mechanically, Ibcos Computers Ltd of Poole, a machinery-dealership
software business founded in 1979 and bought by Constellation in 2012, became person with
significant control of the holding company on 11 December 2020 and direct PSC of Selectapension
Limited (75%+) on 13 September 2024; the holding company was struck off and dissolved on 11 February
2025 [4][5][11]. The FY2024 accounts name Ibcos Computers Limited as immediate parent and
Constellation Software Inc., 1200-20 Adelaide Street East, Toronto, as ultimate parent and
controlling party [7]. Perseus describes a buy-and-hold-forever strategy; its public portfolio page
does not list Selectapension [12].

**Financials (audited, FRS 101, Hillier Hopkins LLP, signed 3 and 4 December 2025)** [7][8]:

| Year to 31 Dec | Turnover | Operating profit | Profit after tax | Net assets | Average staff |
|---|---|---|---|---|---|
| 2022 | £2,631,008 | £545,884 | £390,235 | £2,852,135 | not stated |
| 2023 | £2,400,622 | £265,529 | £260,948 | £3,113,083 | 29 |
| 2024 | £2,275,847 | £601,646 | £532,500 | £3,645,583 | 22 (19 staff + 3 directors) |

FY2024 also shows gross profit £1,618,625, cash £723,965 (2023: £291,151), £4,433,520 owed by group
undertakings (the cash sweep typical of Constellation subsidiaries), share capital £1,052 and staff
costs of £1,032,620 against £1,413,745 the year before [7]. All turnover arose in the UK and the
principal activity is "development of pension management software" [7]. Revenue has therefore
declined roughly 13% over two years while margin recovered through cost reduction.

**Leadership.** Statutory directors are Constellation appointees Bonnie Jean Wilhelm (US, from 11
December 2020) and Alvin Lau (Canadian, from 29 March 2023); Vipin Khullar resigned 18 July 2025,
Dexter Salna 1 January 2024 and Andy McCabe on completion in December 2020 [3]. The operating team
on the website is Adrian Malin, Managing Director (previously Operations Director; a snippet says he
became MD in January 2024 **[unconfirmed]** [153]), Luke Dickens, Head of Partnerships and
Operations, and Daniel Cheeseman, Head of Sales and Marketing [14]; Gary Hollands is listed as Head
of Technical Development **[likely]** [15]. Peter Bradshaw, National Accounts Director, was the
long-time press spokesperson [156]. There is no CEO or CTO title.

**Scale.** 3,000+ adviser subscribers and about 45,000 switching cases a year at the time of the
Intelliflo store launch in September 2018 [17]; "over £6bn of assets analysed across the last 12
months with over 17,000 cases" and an average pot of over £155,000 in November 2024 [18]; a claim of
about 2,000 firms and 3,000 advisers including most top-20 consolidators appears on LinkedIn only
**[unconfirmed]** [16]. Coverage is stated as 150+ products from 50+ providers on the report-writing
and Paradigm pages, 130+ products from 70+ providers on LinkedIn, and "60+ providers" in a 2026 blog,
so the exact figure is an open question [16][19][44][178]. LinkedIn lists 23 employees [16].

**Positioning and commercial model.** "Market leading financial planning software... from research
to recommendation" [21]. There is no public price list (the prices-and-packages page returns 404);
subscriptions include free weekly training sessions and unlimited phone, email and screen-share
support; Quote & Apply is free to subscribers; the report-writing bureau is "from £225 plus VAT"
(Paradigm) or £250 plus VAT within ten working days (own site); provider services are bespoke
[19][30][44][45][164]. An older paraplanner thread put the subscription at roughly £50 a month with
the switching module, about a fifth of O&M's price [128]. Whether licences are per user or per firm
could not be confirmed.

**Notable history.** Pension Monster, a free consumer retirement-guidance site, launched September
2016 and is still live [181]; the DB-transfer bureau suspended advice in 2017 after FCA action
against its outsourced partner CFPML, and Selectapension Bureau Services Ltd was dissolved in 2022
[29]; Rapid Reviewer (MiFID II annual reviews) launched April 2019 [162]; Cashflow & Drawdown
Strategies embedded cashflow modelling in June 2020 [156]; Quote & Apply via the Origo Integration
Hub launched November 2020 with Aegon as first provider [25]; Morningstar MPS performance data was
added November 2024 [27]; Portfolio Insight launched 3 April 2025 and was expanded with Fund & MPS
Research on 30 July 2026 [23][166]; Short Reports arrived July 2025 and the 4admin integration in
February 2026 [24][28]. Awards claimed are an FT Adviser 5-Star (2022) and an Acquisition
International "Best Pension Planning Software Provider 2025"; both pages were unreachable **[likely]**.

## 3. Product suite and adviser workflow

**Structure.** The site groups the suite into three pillars (Switching, Cashflow, Portfolio Insight)
and "other solutions" (Defined Benefit Transfers, Integrations, Pension and Investment Projections,
Report Writing, Provider Services) [22]. The in-app modules, from the user-guide index, are Pension
Switching, Investment Switching, Drawdown Switching, Cashflow and Drawdown ("Retirement Income
Strategies including Cashflow"), Defined Benefit Transfer ("APTA with TVC") with an Income Modeller,
Funds Functionality, Portfolio Insight, Portfolio Upload, New Pension Projections, New Investment
Review, Bespoke Plans (including bespoke workplace schemes), With Profits and AKG company profile
reports, and integration guides for iO, Xplan and Plannr [31]. Older directories also list Rapid
Reviewer, QROPS analysis, Fund Research and Retirement Planning; whether these are still sold is
unconfirmed [163].

**Pension switching workflow** (from the August 2024 user guide, republished January 2026) [32]:

1. Create or link a client (manually, or "Link Client from iO/Xplan/Plannr") and start an analysis.
2. Existing plans: provider from a dropdown or "Other", product, type, valuation date, fund and
   transfer values, regular contributions (gross amount, frequency, escalation and month), current
   death-benefit value, scheme retirement age and an optional desired age, or "Link Plan from iO".
3. Funds within the existing product, by name or ISIN, splits totalling 100%. These are used only
   for past-performance reporting; the tool does not project the existing plan from its fund
   charges [34].
4. Existing scheme projections. Projection basis is Monetary (e.g. 2%, 5%, 8%) or Inflation
   Adjusted (e.g. 0%, 2.94%, 5.88%); growth-rate terms are "Headline" or "Inflation Adjusted";
   rates may be aggregate or per fund. Where the ceding provider will not supply projections, a
   Calculate button generates them from the entered charging structure (fund charges, product AMC,
   indexed fixed fees) [32][33].
5. Additional plan benefits, then remuneration: single-premium initial (% or £), single-premium
   fund based, regular-premium level or initial, and advanced fee styles for timing and frequency.
6. A product-features filter (saveable templates, "products available" counter) and the new
   investment choice: funds and sectors, user templates such as a Royal London Governed Portfolio,
   DFM managed portfolios, or plan default settings, blended to 100%. Where a product offers
   several share classes of a fund the cheapest is chosen, ties broken alphabetically [34].
7. Alternative products: all providers and products, product types, an "Existing Plan" flag that
   applies large-fund discounts and waived fees where the client already holds the product, and
   bespoke workplace schemes [35].
8. Results summary ranks products by projected value at the mid growth rate (falling back to the low
   rate), highlights the existing scheme in green and shows RIY; charges can be bespoked (adjust
   allocation, adjust AMC, lump-sum credit or charge, fund-split override) and recalculated.
9. Full analysis for up to three plans (one if several ceding schemes): consolidated and individual
   results, "Rate of Return Required", effect on fund, RIY, paid-up versus redirection-of-premium
   results and day-one capital value of death benefits before and after transfer.
10. Recommendations text, additional notes and an analysis label, then Word or PDF output (PDF only
    when Morningstar content is included), archived and optionally uploaded to iO or Xplan.

Rules confirmed in the FAQ: no limit on the number of ceding plans; consolidation uses weighted
average growth rates and requires a consistent rate set across plans; redirection results show the
ceding fund left paid-up with new contributions going to the recommended plan; RIY is "the impact of
the charges on the medium growth rate at your chosen retirement date"; transfers into a group
personal pension use either the Bespoke Plans add-on or a "Generic Pension Plan" with editable
charges [33].

**Bespoke Plans charge engine.** Elements are Growth, Product AMC, Allocation Rate, Bonus, Fund
Initial Charge, Fund AMC, Initial Remuneration, Fund-Based Remuneration and Other Charge; each
applies to total fund, single- or regular-premium fund, or premiums; styles are %, % p.a., £ or £
p.a.; tiers with a "use only best tier" option; indexation by NAE, RPI or a fixed rate; frequencies
once, monthly, quarterly, half-yearly or annual, with delay, duration, per-frequency minimum and
maximum and a day-one flag; monthly % p.a. charges apply one twelfth per month [39]. Death benefit
defaults to 100% with enhancements entered as the excess; an "implicit remuneration GPP" flag
handles workplace schemes [39]. This is the closest published description of Selectapension's
internal charge model and it maps almost one-to-one onto SwitchPoint's `ChargeSchedule`.

**Defined benefit transfer.** Scheme details capture CETV and guarantee date, money-purchase AVCs
and underpins, contracting-out, join and leave dates, PCLS commutation, scheme and desired
retirement ages, funding status and wind-up positions [35]. Retirement benefits are entered as
tranches quoted at leaving, calculation, retirement or a specific date, with revaluation and
increase-in-payment dropdowns, GMP fixed-rate defaults by leaving date, bridging pensions, State
Pension deductions and GMP bridges, cash sums, discretionary increase history, early and late
retirement factors and additional benefit groups with their own NRA [35]. Outputs are Critical Yield
Required (single and joint life, capped at 50% for display), death benefits, hurdle rate (return to
match the starting pension with no increases, spouse's pension or guarantee), PCLS, money-purchase
underpin, PPF yields (revalue to calculation date, then PPF revaluation and caps, post-97 increases
CPI to 2.5%, pre-97 none), the TVC, Income Options (single or joint-life 50% with a five-year
guarantee, level or indexed, target age from ONS life tables) and an Income Modeller comparing DB
income against drawdown drawing on other assets with five-year tables and stress tests [35][36]. An
Abridged Advice option produces a one-page Word document and can continue into full advice without
re-keying [35]. The critical yield uses an annuity interest rate "per COBS 13 Annex 2 driven by
calculation date"; day-one figures are revalued from leaving to calculation date; where the term to
retirement is under four years only a day-one result is produced [36]. The APTA/TVC tool launched 2
July 2018 ahead of the rules and gained a bulk upload in September 2019 [155].

**Cashflow & Drawdown Reviews.** Inputs are expenditure, guaranteed incomes (State Pension is
derived automatically from date of birth and sex), assets (owner, plan type, crystallised and
uncrystallised values, contributions with indexation, per-asset growth rates, estimated annual
charge, "fund retained" and "include in transfer comparison"), and target incomes in £ or % of fund,
gross or net, with tax-free-cash rules (UFPLS 25% of each payment, all TFC first, or user-defined)
[37]. Income streams are ordered by drag and drop; growth is deterministic with stress tests either
as a pre-built or custom market event (2020 Covid-19, 2008 crash, a Morningstar index) or a
randomiser between user-set bounds; graphs show target versus actual and target versus sustainable
income (the income that exhausts the fund at the target age) with year-on-year tables [37]. Annuity
quotes come live from Assureweb using the adviser's own credentials [42][60]. No stochastic or Monte
Carlo capability was found, and one paraplanner reported a hard-coded 16-year post-benefit life
expectancy assumption in an older version [128].

**Portfolio Insight.** Fund and MPS research filters by universe (MPS, mutual, pension, bespoke),
legal structure, plan availability, Morningstar star and Medalist ratings, sector, total cost and
cost excluding transaction fees, asset and regional content, attributes (ESG, index, Sharia and so
on), cumulative performance, alpha, beta, Sharpe, R-squared and standard deviation, fund size,
active or passive, style and share type; outputs are saved searches, starred funds, factsheet PDFs,
a session "Audit Report", a portfolio x-ray and a comparison of up to three portfolios across
performance, risk, asset allocation versus target, top-ten holdings, product availability and
projected values [38][166]. Data is the Morningstar feed of 70,000+ funds plus 1,500+ model
portfolios from 100+ DFMs [20][27].

**Other outputs.** New Pension Projections default to 2%, 5% and 8% monetary or -0.5%, 2.5% and
5.4% inflation-adjusted growth [48]; Investment Switching and New Investment Review estimate tax by
client band and offer retain, remodel or switch outcomes against allocation templates [47];
Drawdown Switching captures crystallised values, current income and GAD-maximum settings [31]; Short
Reports (July 2025) are a three-page pension-switching summary whose sample shows a cost comparison
at a single growth rate, required growth of 2.63% against 2.40%, total charges of 1.92% versus
1.61% and a death-benefit comparison [40]. Consumer Duty support consists of the features filter,
links to provider company profiles and fair-value assessments and a charge comparison across
platform, MPS and fund charges [46]. No client-facing portal was found.

## 4. Integration and data ecosystem

**Back offices.** Live integrations are Intelliflo Office (iO store app since September 2018),
Iress Xplan, Plannr (April 2024, cashflow inputs), True Potential and, since 5 February 2026, 4admin;
Time4Advice, Advisory AI, ZeroKey and Finplan are "coming soon" [20][24][26][61]. The iO integration
is two-way: client details and ceding schemes are pulled in ("Link Plan from iO") and every module's
print options include an "Upload Report to iO" tick box [49]; Xplan offers client import and merge,
plan linking and report upload as document notes [31]. Paradigm also lists Iress Adviser Office [19].
No public detail exists on the True Potential connection [59].

**Origo Integration Hub (OIH).** Operated by Origo Services Ltd, Edinburgh, OIH is a hub-and-spoke
exchange with six services: Account Opening and trading instructions, Contract Enquiry (single)
valuations, Bulk Valuations, Bulk Transaction History, Remuneration and Transfer Tracking [50]. It had
42 organisations in August 2021 (Selectapension named), 100+ live connections by September 2023 and
65+ organisations when True Potential joined in August 2026 [53][54]. The technical model, from the
public software-supplier onboarding guides, is: single valuations are synchronous HTTP POSTs of
Criterion-standard XML ("Contract Enquiry Request", Pension/Bond/CIV/Wrap standards, MTG v2.1
headers, 36-character GUID message ids) to `https://oih.origoservices.com/api/getValuation` (UAT at
`oihuat`), Content-Type text/xml or application/xml, UTF-8, optional gzip, a 30-second hub timeout
and OIH00nnn error codes (00403 schema failure, 00300 hub error, 00400 invalid provider response,
00428 provider timeout) [51]. The supplier authenticates with an Organisational Unipass certificate
and embeds the adviser's Unipass Identity X509 data so the provider can authorise the adviser as
servicing agent [51][177]. Bulk valuations, transaction history and remuneration are daily CSV or XML
files (at most one per adviser firm per service per day, available after 7am) pulled over SFTP from
`oih-sftp.origoservices.com`, with standardised `<tpsdata>` extension blocks [52]. Onboarding is a
seven-step path from an analyst call through UAT stubs, end-to-end testing and dual go-live
approval to a commercial contract [51]. Criterion standards, including a Platform Account Opening
standard, are now published independently at criterion.org.uk [55]. Selectapension's confirmed use
of OIH is the account-opening path behind Quote & Apply (Aegon Platform SIPP first, pre-populating
ceding scheme, remuneration and fund selections from the results screens of all five tools) [25];
it is not listed among Aegon's OIH valuation-feed partners, so which other services it is live on is
an open question [157][160].

**Intelliflo API.** OAuth2 and OpenID Connect against `https://identity.gb.intelliflo.net/core/connect`
(authorization-code for user consent, a `tenant_client_credentials` grant with `tenant_id` for
server-to-server) plus a mandatory `x-api-key` header; base `https://api.gb.intelliflo.net` with
`/v2` resources for clients (filter by name, adviser, NI number, reference; `top` max 500), plans
(`/clients/{id}/plans`, JSON Patch, latest valuation embedded), holdings time series, assets and
valuations, documents (signed `x-iflo-object-location`), bulk valuation batches with plan-matching
headers, funds, providers, advisers and WebSub webhooks with `appinstalled` events [56][57][58][59].
Scopes observed include `client_data`, `client_financial_data`, `firm_data`, `fund_data`,
`valuation_batch`, `hub`, `apps` and `offline_access` [57][58]. The platform has 250+ endpoints,
about 1.5 million calls a week and 80+ store apps; apps are certified and distributed through the
intelliflo store [60].

**Iress Open.** A productised subset of the Xplan API with Swagger at `api.iressopen.co.uk` (v3
current, v2 deprecated, UAT at `api.uat.iressopen.co.uk`); a third party needs an API key, the
firm's Xplan site URL, credentials and developer-community access; OAuth2 is supported and the
end user grants access through an Xplan login redirect [62]. Paths follow `/client/{clientId}/<Resource>`
(Address, Asset and so on) with `x-Iress-RequestId` headers and a 502 when Xplan is unavailable;
standard integrations expose 350+ fields via a backend-for-frontend, and documents are written back
as client Notes (Defaqto's guide notes the Assets API does not carry contributions) [62][63][65].
Iress lists 172 Xplan integrations including Selectapension [64].

**Morningstar.** Selectapension's feed brings "past performance, product availability and charges
for over 70,000 pension and investment funds, as well as MPS from circa 90 DFM providers" [20]. The
developer route is Morningstar Direct Web Services: a JWT from `POST /token/oauth` (Basic auth,
60-minute validity) on regional hosts, then `/direct-web-services/v1/investments`,
`investment-details/{ids}`, screener, time-series and portfolio-analysis endpoints, licensed
through an account manager **[likely]** [66][68]. The Security Details datapoint catalogue covers
ISIN and SEDOL, KIID ongoing charge, the MiFID cost block, Morningstar Category, star ratings
(overall, 3, 5 and 10 year), Medalist rating, trailing and calendar returns, alpha and beta, asset
and regional allocation, SRRI, documents and sustainability scores [67]. Morningstar's UK Managed
Portfolio Database holds about 900 portfolios with fees and total cost [69]; bulk licensed data is
delivered as CSV, XLSX, JSON, API or Python with monthly database production [70]. Alternatives are
FE fundinfo (100,000+ funds, 300,000 share classes, REST API and files, Crown ratings) and Defaqto
Data Services (45,000 products, refreshed daily, API or feed) [71][72]. No evidence was found of
Selectapension using FE or Defaqto data [75].

**Other data.** AKG supplies with-profits fund ratings and company profiles [19][31]; Assureweb
(iPipeline) supplies live annuity quotes keyed to each adviser's own credentials **[likely]** [60].

## 5. Regulatory framework and calculation methodology

**DB transfers: APTA and TVC (COBS 19.1).** Before recommending a transfer the firm must determine
the proposed arrangement, carry out an appropriate pension transfer analysis and produce a transfer
value comparator, except where the only safeguarded benefit is a guaranteed annuity rate
(19.1.1CR) [73]. The starting assumption is that a transfer is unsuitable, and that a transfer to a
non-qualifying scheme is less suitable than the default arrangement of an available workplace
scheme (19.1.6G) [73]. APTA compares ceding, proposed and workplace-default arrangements per Annex
4A and 4C (19.1.2BR); other analysis such as stochastic cashflow modelling is allowed only if
outcomes at the 50th percentile are no less conservative (19.1.2CR) [73]. The TVC compares the
transfer value with "the estimated value needed today to purchase the future income benefits
available under the ceding arrangement using a pension annuity" and must be given in the Annex 5
format; a client past NRA uses the CETV retirement age, and an unreduced early retirement without
consent uses that earlier age (19.1.3AR) [73].

The TVC method is fully prescribed. Annex 4B: (1) revalue benefits to the date they would normally
be paid; (2) determine the cost of a pension annuity at that date; (3) discount to the calculation
date [75]. Annex 4C numbers: revaluation in deferment at RPI 3.0%, average earnings or section 148
orders 3.5%, LPI(RPI) 3.0%, LPI(CPI) and CPI 2.0% (1R(4)); annuity interest rates are the average of
the previous three months' intermediate rates from COBS 13 Annex 2 3.1R(6), separately for
RPI-linked and level or fixed-increase annuities, with CPI-linked annuities at the RPI-linked rate
plus 1.0% (1R(2)(a)-(c)); post-retirement LPI(RPI) with a cap at or below 3.5% or a floor at or
above 3.5% uses the fixed-increase rate at the cap or floor, and LPI(CPI) uses the fixed rate when
the cap is at or below 2.5% or the floor at or above 3.0% (1R(2)(d)-(e)); mortality is PMA16/PFA16
year-of-birth with CMI (20YY-2)_M/F_[1.25%] improvements in equal parts, the annuity expense
allowance is 4.0%, and the spouse is assumed three years younger for a male member and three years
older for a female member (1R(2)(f)-(h)); the pre-retirement discount rate is the FTSE Actuaries
fixed-coupon gilt yield for the term band (up to 5, 5-10, 10-15, over 15 years) less a 0.4% p.a.
product charge, refreshed on the 6th of each month from the yield on the 15th of the previous month
(2R) [76]. PS20/6 cut that charge from 0.75% to 0.4% from 1 October 2020 "to reflect the lower costs
of investing solely in gilts" [78]; the rules originally took effect on 1 October 2018 under PS18/6
[79]. Annex 5 prescribes the wording ("It could cost you £[Y] to obtain a comparable level of
income from an insurer. This means the same retirement income could cost you £[Y-X] more by
transferring."), a two-bar chart whose y-axis starts at £0, and three notes on page two [77]. FG21/3
restates the method, says assumptions vary month to month so the calculation-date set must be used,
and stresses that a TVC loss does not preclude a transfer nor a notional gain make one suitable [80].

APTA content (Annex 4A): rates of return must reflect the assets the client would actually hold;
all charges from transfer and subsequent access (product, platform, adviser initial, ongoing and at
crystallisation, withdrawal) are included except adviser charges paid by a third party or payable
regardless of the transfer; tax and State benefits are taken into account; the plan runs a
reasonable period beyond average life expectancy; death benefits are compared fairly at present and
future dates; and any cashflow model must be in real terms at the 2.0% CPI assumption, use
reasonable tax assumptions and include stress tests [74]. Critical yield was the pre-2018 TVAS metric
("the rate of return... necessary to reproduce the safeguarded benefits being given up, assuming
the purchase of an annuity", CP17/16 4.1) [81]; PS18/6 left it "for firms to decide whether a
critical yield approach remains valid" [79], FG21/3 gives an APTA consisting of multiple critical
yields plus a TVC as poor practice [80], and the FCA warns against recommending on the basis of a
critical yield below a firm threshold [102]. Abridged advice (COBS 19.1A) may only recommend
remaining or state that a view needs full advice, must not include APTA or TVC, must be given or
checked by a pension transfer specialist, and its fee is offset against full advice [90].

**Contingent charging (COBS 19.1B, from 1 October 2020).** Both the methodology and total value of
adviser charges must not vary with whether a transfer is recommended or proceeds (19.1B.3R);
carve-outs exist only where the client cannot otherwise pay and is in serious ill-health (life
expectancy below 75 in most cases) or serious financial difficulty (missed credit or bill payments
in three of the last six months), the contingent charge may not exceed the non-contingent one, and
evidence is retained indefinitely (19.1B.9R-17R) [89]. The FCA's 2025 evaluation examined market
effects but not suitability [161].

**Projections and charges disclosure (COBS 13 Annexes 2-4).** Standardised deterministic
projections show lower, intermediate and higher rates, rounded down to three significant figures,
and for personal and stakeholder pensions in real terms at the intermediate inflation rate with an
annuity per 3.1R (1.1R-1.2R) [82]. Maximum nominal rates are 2%, 5% and 8% for pensions, tax-exempt
wrappers and investment-linked annuities and 1.5%, 4.5% and 7.5% for other products, with a 3%
differential and an intermediate rate that must reflect the underlying investments (2.3R) [82].
Inflation assumptions (2.5R, updated 19 November 2025) are price inflation 0.00%, 2.00% and 4.00%,
earnings at least 1.5%, 3.5% and 5.5%, and RPI-linked items 1.00%, 3.00% and 5.00% [82].
Contributions accumulate net of charges compounded annually; charges must include everything after
investment except the firm's dealing costs, and must not be assumed to fall (2.2R, 2.6R, 2.7G) [82].
The future annuity basis (3.1R) is PMA16/PFA16 with CMI (20YY-2) improvements, joint life with the
male three years older, 4% expenses, monthly in advance with a five-year guarantee, and rates of
Y+1.5%/Y+3.5%/Y+5.5% for level or fixed-increase annuities and Y-1%/Y/Y+1% for RPI or LPI-linked,
where Y = 0.5 x (ILG0 + ILG5) - 0.5, rounded to the nearest 0.2%, from FTSE Actuaries index-linked
real yields on the preceding 15 February [82].

Reduction in yield is algebraic. For personal and stakeholder pensions, product RIY A = B - C where
B is the intermediate rate (net of inflation where appropriate) and C, to the nearest 0.1%, is the
rate that reproduces the charged projection with charges removed, computed without adviser charges;
total RIY D = B - E includes all charges; the presentation is "product charges reduce investment
growth after price inflation from B% to C%" and "all charges reduce... from B% to E%" (Annex 4
3.1R-3.3R) [84]. Other packaged products use the same construction per fund or per contribution,
disregarding mortality charges (Annex 3 3.1R-3.4R) [83]. The effect-of-charges table for pensions
has columns for year-end, payments in, withdrawals, value before charges, value with only plan and
investment charges and value after all charges, at least at years 1, 3, 5 and retirement (each of
the first ten years for drawdown or UFPLS), each column being a projection at the intermediate rate
(Annex 4 2.2R); "effect of deductions to date" is the gross fund less what you might get back
(Annex 3 2.2R note 5) [83][84]. UK PRIIPs KIDs express costs as a reduction in yield equal to the
difference between the IRR without costs and the IRR with them over the holding period [98]; the
Consumer Composite Investments regime that replaces them (optional from 6 April 2026, mandatory
from 8 June 2027) uses a headline ongoing-costs figure and drops RIY [99].

**Pension switching without safeguarded benefits.** There is no prescribed calculation. Suitability
sits under COBS 9 and COBS 19.2.2R, which requires the report to explain why a personal pension is
at least as suitable as a stakeholder pension and as additional contributions to an available
occupational scheme, and why a transfer to a non-qualifying scheme is more suitable than the
workplace default [85]. The FSA's February 2009 template and notes define four unsuitable outcomes:
(1) a switch to a more expensive pension than the existing one or a stakeholder without good reason
(exit penalties, initial and ongoing costs), (2) loss of benefits such as guaranteed annuity rates,
(3) a mismatch with attitude to risk and circumstances, and (4) a need for ongoing review not
explained or put in place; "the default should always be to move to a low-cost option", each
ceding scheme is assessed on its own merits, and the KFI must reflect the funds actually recommended
[86]. The template's data section records, per ceding scheme, transfer and fund values, ongoing
charges, projection to retirement and guarantees, and for the receiving scheme the term, non-fund
charges, projection, RIY and adviser fees [87]. The underlying 2008 thematic review found unsuitable
advice in 16% of 500 files, 79% of it involving extra cost without good reason **[likely]** [88].
Royal London's CPD material lists the charge types to compare (AMC, TER, RIY, loyalty bonus,
large-fund discount, policy fee, bid-offer spread), performance over 1, 3 and 5 years, financial
strength and death benefits, in a ceding/workplace/new-scheme grid [88]. A DC critical yield is
therefore an industry convention: Quilter's calculator projects the receiving plan at the ceding
provider's real rates and solves for the rate that matches the ceding projection, converting nominal
to real by (1 + nominal)/1.02 - 1 [148]; the generic definition is the compound growth that takes
the fund to a required amount over the term after charges [149].

**Suitability reports.** COBS 9.4.7R requires the client's demands and needs, why the transaction is
suitable and any disadvantages; pension transfer reports go to the client in good time before the
transaction and personal pension reports within 14 days after [91]. COBS 9.4.11R adds a one-page
summary for transfers: the recommendation with sign-off, abridged or full advice, ongoing services
with monthly and annual cash-terms charges, the 19.1.6G(4)(b) risks, all charges in cash terms
including first-year charges against the ceding scheme and workplace default, the initial advice
cost and the number of months (rounded up) to pay it from the revalued monthly ceding income
(benefits revalued per Annex 4B then discounted at 2.0% CPI) [91]. For MiFID business COBS 9A.3
requires a durable-medium report before the transaction and a statement on periodic reviews [92].
FCA expectations for cashflow modelling add: consistent real or nominal terms, all product and
adviser charges, stress tests (asset fall at start of withdrawals, lower-percentile outcomes, higher
withdrawals), projection beyond average life expectancy and both partners' survival [100][101].

**Consumer Duty.** PRIN 2A.4 (in force 31 July 2023 for open products, 31 July 2024 for closed)
defines fair value as a reasonable relationship between price and benefits and requires the
assessment to consider the expected total price over the product lifetime including entry charges,
annual management charges and contingent fees, with regard to comparable market rates [93][94]. The
FCA's February 2025 review of ongoing advice found reviews delivered in about 83% of cases and
reminded firms of PRIN 2A.4 [95].

**SMPIs: AS TM1 v5.2.** Effective for illustrations dated on or after 6 April 2026 with no change to
the actuarial assumptions from v5.1 [96][97]. Nominal accumulation rates by fund volatility group
are 2%, 4%, 6% and 7% for annualised five-year monthly-return volatility below 5%, 5-10%, 10-15% and
15% or more (C.2.4, C.2.11), with a 0.5% hysteresis before a fund changes group; volatility is the
standard deviation of 60 monthly returns to the 30 September before the illustration year (C.2.8);
inflation and earnings are 2.5% compound (C.2.16-17); the annuity is single life, level, monthly in
advance with a five-year guarantee, priced from the FTSE Actuaries 15-year fixed-interest yield at
15 February rounded to 0.2%, with 4% expenses and PMA16/PFA16 mortality (C.3) [96]. SMPIs are
required by the 2013 Disclosure Regulations Schedule 6 [158]. v5.0 (October 2023) used 1%, 3%, 5%
and 7% [96].

## 6. UK tax and pension parameters 2026/27

All values verified on 6 September 2026 against gov.uk, gov.scot, HMRC manuals, DWP and
legislation.gov.uk; they are encoded in `data/tax-years/2026-27.json`.

| Parameter | 2026/27 value | Source |
|---|---|---|
| Personal allowance; taper | £12,570; £1 per £2 of adjusted net income over £100,000, nil at £125,140 | [103] |
| Rest-of-UK bands | 20% to £37,700 taxable; 40% to £125,140 gross; 45% above; frozen to 5 April 2031 | [103][123] |
| Scottish bands (gross) | 19% to £16,537; 20% to £29,526; 21% to £43,662; 42% to £75,000; 45% to £125,140; 48% above | [104] |
| Savings | Starting rate band £5,000; PSA £1,000 / £500 / £0; rates rise to 22/42/47% from 6 April 2027 | [105][121] |
| Dividends | Allowance £500; 10.75% / 35.75% / 39.35% | [106] |
| Capital gains | AEA £3,000 (£1,500 trusts); 18% within basic-rate band, 24% above; BADR 18% | [107][169] |
| NI Class 1 employee | PT £12,570; UEL £50,270; 8% then 2%; LEL £6,708 | [108] |
| NI Class 1 employer | 15% above £5,000; Employment Allowance £10,500 | [108] |
| Annual allowance | £60,000; MPAA £10,000 (no carry forward); taper £1 per £2 of adjusted income over £260,000 when threshold income exceeds £200,000, floor £10,000 at £360,000 | [109][110][111] |
| Carry forward; relievable amount | Three previous years, current year first then earliest; max(£3,600, 100% relevant UK earnings) | [170][171] |
| Relief at source | 20% at source; further 20% / 25% via Self Assessment (Scottish 1% to 28%) | [168] |
| Lump sum allowance; LSDBA | £268,275; £1,073,100; tax-free cash normally 25% within the LSA | [112][113][175] |
| UFPLS; first flexible payment | 25% tax free, 75% as income; emergency code 1257L on a month-1 basis **[likely]**; reclaim via P55/P53Z/P50Z | [124][159] |
| Normal minimum pension age | 55; 57 from 6 April 2028 (affects those aged 55-56 on 5 April 2028) | [114][176] |
| New State Pension | £241.30 a week (£12,547.60 a year), +4.8% triple lock on earnings; 35 qualifying years, minimum 10 | [115][116][172] |
| Basic State Pension | £184.90 a week (Category A/B) | [116] |
| State Pension age | 66 rising to 67 for births 6 April 1960 to 5 March 1961 (phased May 2026 to March 2028); 67 to 5 April 1977; 68 phased 2044-46, under review | [117] |
| ISAs | £20,000 overall; LISA £4,000; JISA £9,000; cash ISA £12,000 for under-65s from 6 April 2027 | [118][119] |
| Announced changes | Pensions into IHT estates from 6 April 2027 (Finance Act 2026); NICs on salary-sacrificed pension contributions above £2,000 from 6 April 2029; no new measures in the Spring Forecast 2026 | [120][122][173][174] |

## 7. Competitive landscape and the must-have feature list

The market is layered and no vendor spans all of it: switching and DB engines (O&M Profiler ESP,
Selectapension, Defaqto Engage's switching modules, FE fundinfo's ex-AdviserAsset tools, Synaptic's
ex-ante RIY), cashflow modellers (Voyant, FE CashCalc, Timeline, Dynamic Planner Cash Flow,
intelliflo planning, Prestwood Truth), risk profilers (Dynamic Planner most used at 23-25%, Defaqto
19%), research (FE Analytics 300,000 instruments, Defaqto 21,000, Synaptic 140,000), engagement
(Money Alive, moneyinfo) and CRMs (intelliflo at about 46% share, Xplan, Plannr at £140 a month)
[129][131][134][140][142][147].

| Vendor and product | Coverage | Public price (ex VAT) | Notes |
|---|---|---|---|
| Iress O&M Profiler ESP | Switching (pension, DB, bond, ISA, GIA, wrap, drawdown), drawdown three-way, cashflow incl. APTA, TVC, risk profiling; Morningstar and AKG data | £45 / £75 / £120 a month for 1/2/3 modules, £20 extra user, £100 setup | "Incredibly powerful" but "really tricky to use"; roughly five times Selectapension's price [125][126][128] |
| Selectapension | Full switching stack, APTA/TVC, cashflow (deterministic), Portfolio Insight | Not public; bureau £225-£250 a report | Strong support; output "limited and frustrating to manipulate" (older thread) [19][44][128] |
| Defaqto Engage | Research 21,000+ funds and products, stochastic cashflow (Hymans Robertson), risk, switching and RIY, suitability reports; iO, Xplan, Plannr | Switching add-on £30 a user a month; base licence not public | No critical yield or TVC [129][130] |
| Synaptic Pathways | Salesforce-based; Moody's stochastic; 140,000 funds; ex-ante RIY; CIP management | £175 a user a month enterprise; £25-£35 basics | [131][132] |
| FE fundinfo (Analytics, CashCalc, Pension Switching & Platform Due Diligence) | 300,000 instruments; RIY and fee comparison; deterministic, stochastic and tax-aware cashflow | Analytics about £175 a month (2022); CashCalc £75 a month | Reliability and navigation complaints in 2022 [133][134][135][136] |
| Voyant AdviserGo | Comprehensive cashflow with Monte Carlo and historic returns | £175 a seat a month | "A bit too complex" [137][143] |
| Timeline | Cashflow stress-tested on 100+ years of history, LOAs, IHT planner | £142 a month unlimited clients | Historical, not Monte Carlo, in the UK product [138] |
| Dynamic Planner | Risk profiling, stochastic cashflow (5th/50th/95th), reviews | £60 / £124 / £179 a month + £130 setup | "Easiest to navigate" but simplified tax and opaque assumptions [139][141] |
| intelliflo planning | Cashflow bundled free into intelliflo office | Included | Stochastic capability unconfirmed [142] |

Two observations shape the build. First, a September 2026 review notes that Dynamic Planner,
Voyant, CashCalc and Timeline all leave suitability-report drafting outside the tool [144], while
Selectapension and O&M generate the analysis report but not the suitability report itself. Second,
the calculation frontier is stochastic modelling and tax fidelity: Defaqto, Synaptic, Dynamic
Planner, CashCalc and Voyant have stochastic engines and Selectapension does not [129][131][133][139].

**Must-have feature list for a unified rebuild** (derived from the competitor facts, the FSA
template, the PFS and Genovo report structures and the RIAAT capture fields) [86][88][145][146]:

1. One client and plan data model with two-way CRM sync (intelliflo, Xplan, Plannr) and platform or
   Origo valuation feeds.
2. Existing-plan capture that supports every legacy charge shape (AMC, policy fees with indexation,
   allocation rates, bid-offer spread, loyalty bonus, large-fund discount, exit penalties) and
   guarantee flags (GAR, guaranteed growth, with-profits MVR and terminal bonus, protected tax-free
   cash, protected pension age).
3. A switching engine producing standardised projections at the COBS 13 rates in real terms, RIY per
   plan and per fund, effect-of-charges tables, a reproducible DC critical yield with headroom and
   break-even year, consolidation with redirected contributions, ceding versus workplace versus new
   comparison, and a per-plan switch, consider, retain or refer verdict mapped to the four FSA
   unsuitable outcomes.
4. A DB module with tranche revaluation, TVC in Annex 5 format, APTA income and death-benefit
   comparisons in real terms with stress tests, critical yields A and B and a drawdown hurdle rate
   labelled as not required by the FCA, workplace-default comparison, one-page summary figures and
   a contingent-charging check.
5. Drawdown versus annuity versus UFPLS three-way comparison with annuity pricing and, ideally, live
   quotes.
6. Deterministic and stochastic cashflow with percentiles, historical and parametric stress tests,
   projection beyond average life expectancy, full income tax, NI, CGT and allowance logic.
7. Fund, MPS and DFM research with Morningstar-class data, ex-ante and ex-post MiFID cost disclosure
   and Consumer Duty fair-value evidence.
8. One-click analysis and suitability reports (PFS structure: executive summary, objectives, risk,
   recommendations, reasons why, disadvantages, charges in £ and %, replacement business) in Word
   and PDF with an audit trail, versioned assumptions and RIAAT-aligned data capture.
9. Annual-review workflow and an open API.

## 8. Technology and engineering notes

This section is from direct inspection on 6 September 2026 plus public profiles; Selectapension
publishes no engineering blog, job adverts or stack description, and LinkedIn, Reed and Adzuna show
no vacancies posted by the company (the Reed hits are agencies seeking paraplanners with
"Selectapension experience", which is itself evidence that the product is a standard paraplanner
skill) [16][151]. Glassdoor and Indeed company pages were blocked, so employee sentiment about the
engineering culture is unknown.

**Application stack (observed).** `app.selectapension.com` responds over HTTP/2 with
`server: Microsoft-IIS/10.0` and `x-powered-by: ASP.NET`, sets an `ASP.NET_SessionId` cookie and an
ASP.NET anti-forgery `__RequestVerificationToken`, and its pages are ASP.NET Web Forms: `.aspx`
endpoints under `/cms/members/` (for example `/cms/members/default.aspx`), `__VIEWSTATE` fields, an
`aspnetForm`, nested master-page control ids (`ctl00_ctl00_membersMasterHead`) and an ISO-8859-1
charset [150]. The front end loads jQuery 1.9.1 and jQuery UI 1.10.2 (2013 releases), Bootstrap with
Font Awesome 4.7.0 from bootstrapcdn, a Vue runtime (`/js/VueJS/vue.min.js`) and hand-written
scripts (`validation.js`, `common.js`, `wspace.js`, `resizer.js`), all cache-busted with
`?v=2026.9.0.0`, which suggests calendar-versioned monthly releases (the September 2026 build was
live on the day of inspection) [150]. The `/cms/` prefix with `wlwmanifest.aspx` and `rsd.aspx`
discovery endpoints indicates an in-house .NET CMS rather than a packaged one; a request for
`/robots.txt` returns the application shell, so unknown paths fall through to the app. Security
headers are reasonable: HSTS with preload, `X-Frame-Options: DENY` and a `frame-ancestors 'self'`
CSP [150]. Menu paths visible in the shell include `members/userarea`, `members/Invoices`,
`members/docproviders` and `members/infoctrl`, consistent with a single monolith serving analysis,
documents and billing [150]. The Selectapension iO app was "built on the Intelliflo Developer
Platform public APIs" [17], and the Origo, Iress Open and Morningstar connections described in
section 4 imply XML-over-HTTPS with Unipass client certificates, OAuth2 REST and a licensed data
feed respectively.

**Hosting.** The application IP (51.132.215.118) belongs to AS8075 Microsoft Corporation, geolocated
to London, so the app runs on Azure, most probably UK South; whether as IaaS virtual machines or App
Service could not be determined [152]. The marketing site is WordPress 7.1 with a custom
`selectapension` theme on a separate nginx and Plesk host (185.132.40.136) with Google Tag Manager,
and `go.selectapension.com` (demo booking, user guides, training) is a CNAME to Salesforce Pardot
(Account Engagement) [150]. User guides are PDFs and DOCX files served from the WordPress uploads
folder [31].

**Team.** Average headcount was 22 in 2024 (19 employees plus three directors) against 29 in 2023,
with staff costs of about £1.03m, an average of roughly £47,000 per head including sales, support,
training and report-writing staff [7]. A single "Head of Technical Development" is the only named
technology leader **[likely]** [15]; the pattern of 2013-era client libraries alongside a Vue runtime
and a monthly version stamp points to a small team incrementally modernising a Web Forms monolith
rather than replatforming. Constellation's model (decentralised, buy-and-hold, each business run by
its own manager) means no group-wide stack is imposed; Ibcos, the immediate parent, publishes no
technical detail beyond an invitation to send a CV [11][12].

**How vendors keep charge data current.** None of the switching vendors publishes an update
frequency for product charges. Selectapension says charges are "pulled directly from providers"
[43], runs a Provider Services programme through which pension providers, platforms and DFMs are
listed and given a "Market Intelligence" analytics suite on adviser usage of their products [45],
and lets firms maintain their own charge structures through Bespoke Plans and per-case edits to a
Generic Pension Plan [33][39]. Fund-level charges and availability come from the Morningstar feed
[20]. O&M states that it "maintains a Partnership Programme with all providers whose products appear
on the system" with mutual accuracy commitments and "a team of O&M researchers collating data from
each of our partners" [127]. Defaqto Data Services monitors 45,000 products and 3.6 million features
refreshed daily [72]; Morningstar's licensed database is produced monthly [70]; FE fundinfo processes
about 7 million prices a month [71]. The practical pattern is provider-supplied charge sheets
maintained by a small data team, with an as-at date per product and an escape hatch for the adviser
to key or override charges per case, which is exactly what SwitchPoint's `ProductChargeVersion`,
`DataQuality` and `SourceUrl` fields are for.

## 9. How SwitchPoint maps to all of this

| Selectapension capability | SwitchPoint module / file | Status |
|---|---|---|
| Client record, link from iO/Xplan/Plannr | `src/SwitchPoint.Domain/Clients/Client.cs`, `ExternalReference.cs`; `IBackOfficeConnector` port; `POST /integrations/{connector}/import` | Domain done; connectors planned in Infrastructure |
| Existing plan capture (values, contributions, death benefit, retirement ages) | `Domain/Schemes/Scheme.cs`, `Contribution.cs`, `Guarantees.cs`, `Holding.cs`; `SchemeWrite` DTO | Done |
| Provider and product database (150+ products, availability, large-fund discounts) | `Domain/Market/Provider.cs`, `Product.cs`, `ProductChargeVersion.cs`, `WrapperTypes.cs`; `data/providers.json` seed; `GET /products` | Domain done; seed data and catalogue repository pending |
| Bespoke Plans charge engine (tiers, indexation, allocation rate, bonuses, fixed fees, exit penalties) | `Domain/Charges/ChargeSchedule.cs`, `TieredCharge.cs`, `FixedCharge.cs`, `Indexation.cs`, `LargeFundDiscount.cs`, `ExitPenaltySchedule.cs`, `AdviserCharge.cs`, `DealingCharges.cs` | Done (tested) |
| Projections, monetary or inflation-adjusted, 2/5/8% headline rates | `Calculation/Projection/ProjectionEngine.cs`, `Numerics/RateMath.cs`; `Domain/Assumptions/AssumptionSet.cs`; `data/fca-assumptions.json` | Done |
| Reduction in yield and effect-of-charges table | `Calculation/Riy/ReductionInYieldCalculator.cs`; `POST /calculations/riy` | Done |
| Rate of Return Required, consolidation, redirection of contributions, per-plan verdict | `Calculation/CriticalYield/CriticalYieldCalculator.cs`, `Numerics/RootFinder.cs`; `Domain/Analysis/PensionSwitchAnalysis.cs`; `POST /calculations/pension-switch` | Done; verdict rules in `critical-yield.md` |
| Day-one capital value of death benefits (switching) | `PensionSwitchResultDto` has no death-benefit field | Gap: add to result and report |
| Guarantee handling (GAR, with-profits MVR, protected TFC) | `Domain/Schemes/Guarantees.cs`; `anyGuaranteesFlagged`, `Refer` verdict | Done (flags); AKG strength data not sourced |
| DB scheme capture (tranches, revaluation, escalation, GMP, bridging, PCLS factors, funding status) | `Domain/Schemes/DefinedBenefitScheme.cs`, `DbTranche.cs`, `RevaluationRule.cs`, `EscalationRule.cs`, `IndexBasis.cs` | Done |
| TVC (Annex 4B/4C/5), critical yields A/B, hurdle rate, APTA income and death-benefit comparison, one-page summary figures | `Calculation/DbTransfer/DbTransferCalculator.cs`, `Annuities/AnnuityPricer.cs`, `Mortality/GompertzMakehamLifeTable.cs`, `ILifeTable.cs`; `Domain/Analysis/DbTransferAnalysis.cs` | Done; mortality is an ONS-calibrated approximation to PMA16/PFA16 until a CMI licence is dropped in |
| PPF yields and money-purchase underpin | none | Gap |
| Abridged advice one-pager; contingent charging record | `DbTransferAnalysisWrite.chargeBasis` and `contingentChargingCarveOut`; report template | Data captured; abridged template pending in `SwitchPoint.Reports` |
| Income Options: annuity single/joint, level/indexed, ONS target age | `AnnuityPricer.cs`; `StatePension/StatePensionCalculator.cs` | Done for illustrative pricing; live Assureweb quotes out of scope |
| Cashflow with tax-free-cash rules, income ordering, sustainable income, stress events | `Calculation/Cashflow/CashflowEngine.cs`, `Tax/UkTaxCalculator.cs`, `Tax/PensionAllowanceCalculator.cs`; `Domain/Analysis/CashflowPlan.cs`; `POST /calculations/cashflow` | Done (deterministic) |
| Stochastic modelling (not offered by Selectapension) | `Calculation/MonteCarlo/MonteCarloSimulator.cs`, `Numerics/Xoshiro256StarStar.cs`; `Domain/Assumptions/CapitalMarketAssumptions.cs`, `AssetClass.cs`; `POST /calculations/cashflow/stochastic` | Done, with the COBS 19.1.2CR conservativeness check |
| Investment Switching tax estimate by band | `Tax/UkTaxCalculator.cs`, `Tax/TaxYearParameters.cs`; `data/tax-years/2026-27.json`; `POST /calculations/tax` | Done |
| Portfolio Insight, fund and MPS research, portfolio upload | `Domain/Market/Fund.cs`, `ModelPortfolio.cs`, `AssetAllocation.cs`; `IFundDataProvider` (Morningstar); `GET /funds`, `/model-portfolios` | Domain done; Morningstar sync and screening UI pending |
| Reports: Word/PDF, archived, upload to back office | `src/SwitchPoint.Reports` (QuestPDF, OpenXML); `Domain/Analysis/Report.cs`; `POST /reports` | Pending |
| Session audit report; regulator-proof analysis record | `Domain/Audit/AuditEvent.cs`, `HashChainVerifier.cs`; `GET /audit/verify`; report embeds SHA-256 | Domain done; SwitchPoint goes further with a hash chain |
| Quote & Apply via Origo Hub | `IValuationProvider` (Origo Contract Enquiry) only | Account opening not in scope; valuations planned |
| Firm settings, assumption sets, FCA-standard versus firm-editable | `Domain/Tenancy/Firm.cs`, `UserRole.cs`; `Domain/Assumptions/AssumptionSet.cs`; `/assumption-sets` | Done |
| Consumer Duty charge comparison | `Domain/Charges/ChargeBreakdown.cs`; `RiyDto.totalCharges` | Done; fair-value assessment links not modelled |

## 10. Open questions

1. Selectapension's subscription price points and whether licences are per user or per firm; the
   prices-and-packages page is gone and the only figures are a historic forum estimate and the
   bureau price [19][44][128].
2. The current provider and product counts (150/50, 130/70 or 200/40 depending on the page) and how
   often provider charge data is refreshed [16][19][163].
3. Which Origo Integration Hub services Selectapension is live on beyond account opening, and whether
   providers other than Aegon were added to Quote & Apply after 2020 [25][157][160].
4. Whether the Intelliflo app uses the authorization-code or tenant-client-credentials flow and
   which scopes it requests; Intelliflo's developer portal is login-gated [56].
5. Morningstar licensing cost and redistribution rights, and whether Selectapension consumes Direct
   Web Services or a bulk file feed [66][70].
6. The exact TVC gilt yields, COBS 13 "Y" and annuity rates in use: the Handbook publishes the
   method, not the numbers, so SwitchPoint must source FTSE Actuaries index values monthly and
   annually [76][82].
7. The effective date of the COBS 13 Annex 2 2.5R change to 2.00% price inflation and 3.00% RPI,
   which matters for reproducing historic illustrations [82].
8. Whether Selectapension's cashflow engine has any stochastic mode, whether the 16-year
   life-expectancy assumption survives, and whether the DB FAQ's lifetime-allowance text has been
   updated for the LSA/LSDBA regime [36][37][128].
9. Adrian Malin's appointment date as MD, whether Andy McCabe retains any role, and the FY2025
   accounts due 30 September 2026, given the revenue trend [2][153].
10. The Azure hosting model (VMs versus App Service), deployment cadence and test practice, none of
    which is observable from outside [150].
11. Emergency tax code 1257L for 2026/27 and the alternative annual allowance wording in PTM056510
    should be re-read from the primary documents before encoding [110][124].

## 11. Sources

1. Companies House, Selectapension Limited overview — https://find-and-update.company-information.service.gov.uk/company/05075441
2. Companies House, filing history — https://find-and-update.company-information.service.gov.uk/company/05075441/filing-history
3. Companies House, officers — https://find-and-update.company-information.service.gov.uk/company/05075441/officers
4. Companies House, persons with significant control — https://find-and-update.company-information.service.gov.uk/company/05075441/persons-with-significant-control
5. Companies House, Selectapension (2013) Limited — https://find-and-update.company-information.service.gov.uk/company/08607953
6. Companies House, Selectapension (2013) Limited PSC — https://find-and-update.company-information.service.gov.uk/company/08607953/persons-with-significant-control
7. Selectapension Limited FY2024 audited accounts (PDF) — https://find-and-update.company-information.service.gov.uk/company/05075441/filing-history/MzQ5MjU3OTI3MmFkaXF6a2N4/document?format=pdf
8. Selectapension Limited FY2023 audited accounts (PDF) — https://find-and-update.company-information.service.gov.uk/company/05075441/filing-history/MzQzNzUzODY0NGFkaXF6a2N4/document?format=pdf
9. International Adviser, UK pension platform sold to Canadian tech firm (Dec 2020) — https://international-adviser.com/uk-pension-platform-sold-to-canadian-tech-firm/
10. Professional Paraplanner, Three sell-offs in as many days (Dec 2020) — https://professionalparaplanner.co.uk/three-sell-offs-in-as-many-days-novia-selectapension-and-lv/
11. Ibcos, About — https://www.ibcos.co.uk/about
12. Perseus Group (Constellation Software) — https://csiperseus.com/
13. Money Marketing, Andy McCabe on taking the plunge — https://www.moneymarketing.co.uk/analysis/selectapensions-andy-mccabe-on-taking-the-plunge-and-unintended-rdr-effects/
14. Selectapension, About us — https://selectapension.com/about-us/
15. RocketReach, Selectapension management — https://rocketreach.co/selectapension-management_b5e7ed30f42e6426
16. LinkedIn, Selectapension company page — https://uk.linkedin.com/company/selectapension
17. Money Marketing, Intelliflo and Selectapension partnership (2018) — https://www.moneymarketing.co.uk/news/intelliflo-selectapension-partnership-transfers/
18. Selectapension, The importance of planning ahead for retirement (Nov 2024) — https://selectapension.com/the-importance-of-planning-ahead-for-retirement-in-the-uk/
19. Paradigm, Selectapension strategic partner page — https://www.paradigm.co.uk/compliance/strategic-partners/Selectapension.html
20. Selectapension, Integrations — https://selectapension.com/solutions/integrations/
21. Selectapension, Home — https://selectapension.com/
22. Selectapension, Solutions — https://selectapension.com/solutions/
23. Selectapension, Portfolio Insight Fund & MPS Research (30 Jul 2026) — https://selectapension.com/portfolio-insight-fund-mps-research/
24. Selectapension, 4admin integration now available (Feb 2026) — https://selectapension.com/selectapension-4admin-integration-now-available/
25. Professional Paraplanner, New Selectapension tool speeds quote and illustration process (Nov 2020) — https://professionalparaplanner.co.uk/new-selectapension-tool-speeds-quote-and-illustration-process/
26. Professional Adviser, Plannr partners to integrate cashflow tool (Apr 2024) — https://www.professionaladviser.com/news/4196407/plannr-partners-integrate-cashflow-tool-crm
27. Selectapension, MPS portfolios now available (Nov 2024) — https://selectapension.com/the-mps-portfolios-now-available-on-selectapension/
28. Selectapension, Short Reports now available (Jul 2025) — https://selectapension.com/new-feature-short-reports-now-available-in-selectapension/
29. Money Marketing, Firm behind Selectapension's DB transfer suspension (2017) — https://www.moneymarketing.co.uk/analysis/firm-behind-selectapensions-db-transfer-suspension/
30. Selectapension, Switching — https://selectapension.com/solutions/switching/
31. Selectapension, User guides index — https://go.selectapension.com/userguides
32. Selectapension, Pension Switching User Guide (Aug 2024) — https://selectapension.com/wp-content/uploads/2026/01/Pension_Switching_User_Guide_240801.pdf
33. Selectapension, FAQ Pension Switching — https://selectapension.com/wp-content/uploads/2026/01/FAQ_____Pension_Switching.pdf
34. Selectapension, Funds Functionality User Guide — https://selectapension.com/wp-content/uploads/2026/01/Funds_Functionality_User_Guide_240801.pdf
35. Selectapension, DB Transfer User Guide — https://selectapension.com/wp-content/uploads/2026/01/DB_Transfer_User_Guide_240801.pdf
36. Selectapension, FAQ Defined Benefit Transfer — https://selectapension.com/wp-content/uploads/2026/01/FAQ_____Defined_Benefit_Transfer.pdf
37. Selectapension, Cashflow User Guide (Jan 2026) — https://selectapension.com/wp-content/uploads/2026/01/Cashflow-Userguide-Jan-2026.pdf
38. Selectapension, Portfolio Insight User Guide (Jul 2026) — https://selectapension.com/wp-content/uploads/2026/07/Portfolio-Insight-User-Guide.docx
39. Selectapension, Bespoke Plan User Guide — https://selectapension.com/wp-content/uploads/2026/01/Bespoke_Plan_Userguide.pdf
40. Selectapension, Short report sample (Jul 2025) — https://selectapension.com/wp-content/uploads/2025/07/short-report-sample-1.pdf
41. Selectapension, Defined Benefit Transfers — https://selectapension.com/solutions/defined-benefit-transfers/
42. Selectapension, Cashflow and Drawdown Reviews — https://selectapension.com/solutions/cashflow-and-drawdown-reviews/
43. Selectapension, Switching software — https://selectapension.com/solutions/switching-software/
44. Selectapension, Report writing — https://www.selectapension.com/report-writing/
45. Selectapension, Provider services — https://www.selectapension.com/provider-services/
46. Selectapension, Are you Consumer Duty ready — https://selectapension.com/are-you-consumer-duty-ready/
47. Selectapension, Investment Switching User Guide — https://selectapension.com/wp-content/uploads/2026/01/Investment_Switching_User_Guide.pdf
48. Selectapension, New Pension Projections User Guide — https://selectapension.com/wp-content/uploads/2026/01/New_Pension_Projections_User_Guide_240801.pdf
49. Selectapension, iO user guide for advisers — https://selectapension.com/wp-content/uploads/2026/01/iO_userguide_for_Advisers.pdf
50. Origo, Origo Integration Hub — https://origo.com/origo-services/origo-integration-hub
51. Origo, OIH Valuations Onboarding Guide for software suppliers v1.1 (PDF) — https://origo.com/assets/components/common/OIH-Valuations-Onboarding-Guide-SS-v1.1.pdf
52. Origo, OIH Bulk Services Onboarding Guide for software suppliers v1.3 (PDF) — https://origo.com/assets/components/common/OIH-Bulk-Services-Onboarding-Guide-SS-v1.3.pdf
53. Origo, Integration Hub signs an extra 20 organisations (2021) — https://origo.com/news-and-press-releases/origo-integration-hub-signs-an-extra-20-organisations-to-take-the-total-number-to-42-with-more-in-the-pipeline
54. Finovate, True Potential joins Origo's Integration Hub (Aug 2026) — https://finovate.com/true-potential-joins-origos-integration-hub-giving-advisers-greater-access-to-valuation-data/
55. Origo, Criterion standards — https://origo.com/criterion-standards
56. Intelliflo developer portal, Authentication — https://developer.gb.intelliflo.net/docs/Authentication
57. Intelliflo public API v2 Swagger (JSON) — https://s3-eu-west-2.amazonaws.com/api-swagger-content.prd-gb-01.intelliflo.net/1739987884public-v2.json
58. Power Planner, Working with iO — https://www.powerplanner.solutions/blog/working-with-io/
59. intelliflo-python, Bulk valuations API docs — https://github.com/strathausen/intelliflo-python/blob/main/docs/BulkvaluationsApi.md
60. Intelliflo, Demystifying integrations — https://www.intelliflo.com/insights/thought-leadership/demystifying-integrations/
61. Intelliflo, Selectapension integrated partner page — https://www.intelliflo.com/partners/integrated-partners/selectapension/
62. Iress Open Standard API Swagger 3.0 — https://api.iressopen.co.uk/swagger/3.0/swagger.json
63. Iress Community, What is Iress Open — https://community.iress.com/t5/Xplan-Integrations/What-is-Iress-Open-Understanding-our-integration-tool-types-and/ta-p/27257
64. Iress, Xplan integrations — https://www.iress.com/software/financial-advice/xplan-integrations/
65. Defaqto, Xplan Integration Guide (Jan 2024, PDF) — https://www.defaqto.com/7317/3260/9114/Xplan_Integration_Guide_Jan_24.pdf
66. Morningstar Direct Web Services, Authentication — https://developer.morningstar.com/direct-web-services/documentation/documentation/get-started/authentication
67. Morningstar Direct Web Services, Security Details API datapoints (XLSX) — https://developer.morningstar.com/content/hidden-from-navigation/morningstar-direct-web-services-security-details-api.xlsx
68. Morningstar newsroom, Direct Web Services launch (Dec 2023) — https://newsroom.morningstar.com/news/news-details/2023/Morningstar-Direct-Web-Services-Brings-Sophisticated-Investment-Data-Research-and-Calculation-APIs-to-Power-Firms-Digital-Platforms/default.aspx
69. Investment Week, Morningstar launches UK Managed Portfolio Database (2022) — https://www.investmentweek.co.uk/news/4053878/morningstar-launches-uk-managed-portfolio-database
70. Morningstar, Data feeds — https://www.morningstar.com/business/products/direct/data-feeds
71. FE fundinfo, Data partners — https://www.fefundinfo.com/who-we-serve/data-partners
72. Defaqto, Data Services — https://www.defaqto.com/solutions/Data-Services
73. FCA Handbook, COBS 19.1 — https://www.handbook.fca.org.uk/handbook/COBS/19/1.html
74. FCA Handbook, COBS 19 Annex 4A — https://www.handbook.fca.org.uk/handbook/COBS/19/Annex4A.html
75. FCA Handbook, COBS 19 Annex 4B — https://www.handbook.fca.org.uk/handbook/COBS/19/Annex4B.html
76. FCA Handbook, COBS 19 Annex 4C — https://www.handbook.fca.org.uk/handbook/COBS/19/Annex4C.html
77. FCA Handbook, COBS 19 Annex 5 — https://www.handbook.fca.org.uk/handbook/COBS/19/Annex5.html
78. FCA, PS20/6 Pension transfer advice: feedback and final rules (PDF) — https://www.fca.org.uk/publication/policy/ps20-06.pdf
79. FCA, PS18/6 Advising on pension transfers (PDF) — https://www.fca.org.uk/publication/policy/ps18-06.pdf
80. FCA, FG21/3 Advising on pension transfers (PDF) — https://www.fca.org.uk/publication/finalised-guidance/fg21-3.pdf
81. FCA, CP17/16 Advising on pension transfers (PDF) — https://www.fca.org.uk/publication/consultation/cp17-16.pdf
82. FCA Handbook, COBS 13 Annex 2 — https://www.handbook.fca.org.uk/handbook/COBS/13/Annex2.html
83. FCA Handbook, COBS 13 Annex 3 — https://www.handbook.fca.org.uk/handbook/COBS/13/Annex3.html
84. FCA Handbook, COBS 13 Annex 4 — https://www.handbook.fca.org.uk/handbook/COBS/13/Annex4.html
85. FCA Handbook, COBS 19.2 — https://www.handbook.fca.org.uk/handbook/COBS/19/2.html
86. FSA, Pension switching suitability assessment template notes (Feb 2009, PDF) — https://www.fca.org.uk/publication/archive/fsa-pension-switching-template-notes.pdf
87. FSA, Pension switching suitability assessment template (Feb 2009, PDF) — https://www.fca.org.uk/publication/archive/fsa-pension-switching-template.pdf
88. Royal London, Pension switching: achieving good outcomes for clients (PDF) — https://adviser.royallondon.com/globalassets/docs/adviser/misc/pension-switching-achieving-good-outcomes-for-clients.pdf
89. FCA Handbook, COBS 19.1B — https://www.handbook.fca.org.uk/handbook/COBS/19/1B.html
90. FCA Handbook, COBS 19.1A — https://www.handbook.fca.org.uk/handbook/COBS/19/1A.html
91. FCA Handbook, COBS 9.4 — https://www.handbook.fca.org.uk/handbook/COBS/9/4.html
92. FCA Handbook, COBS 9A.3 — https://www.handbook.fca.org.uk/handbook/COBS/9A/3.html
93. FCA Handbook, PRIN 2A.4 — https://www.handbook.fca.org.uk/handbook/PRIN/2A/4.html
94. FCA, PS22/9 A new Consumer Duty — https://www.fca.org.uk/publications/policy-statements/ps22-9-new-consumer-duty
95. FCA, Multi-firm review of ongoing financial advice services (Feb 2025) — https://www.fca.org.uk/publications/multi-firm-reviews/ongoing-financial-advice-services
96. FRC, AS TM1 Statutory Money Purchase Illustrations v5.2 (PDF) — https://www.frc.org.uk/documents/8999/AS_TM1_Statutory_Money_Purchase_Illustrations_v5.2.pdf
97. FRC, AS TM1 assumptions unchanged following annual review (Feb 2026) — https://www.frc.org.uk/news-and-events/news/2026/02/frc-confirms-as-tm1-assumptions-unchanged-following-annual-review/
98. legislation.gov.uk, UK PRIIPs RTS Annex VI — https://www.legislation.gov.uk/eur/2017/653/annex/VI/2019-11-28
99. FCA, PS25/20 Consumer Composite Investments (PDF) — https://www.fca.org.uk/publication/policy/ps25-20.pdf
100. FCA, Undertaking cashflow modelling to demonstrate suitability — https://www.fca.org.uk/firms/undertaking-cashflow-modelling-demonstrate-suitability-retirement-related-advice
101. FCA, Retirement income advice: good practice and areas for improvement (TR24/1) — https://www.fca.org.uk/publications/good-and-poor-practice/retirement-income-advice-good-practice-areas-improvement
102. FCA, Advising on pension transfers: our expectations — https://www.fca.org.uk/news/news-stories/advising-pension-transfers-our-expectations
103. gov.uk, Income Tax rates and Personal Allowances — https://www.gov.uk/income-tax-rates
104. gov.scot, Scottish Income Tax rates and bands 2026 to 2027 — https://www.gov.scot/publications/scottish-income-tax-rates-and-bands/pages/2026-to-2027/
105. gov.uk, Tax on savings interest — https://www.gov.uk/apply-tax-free-interest-on-savings
106. gov.uk, Tax on dividends — https://www.gov.uk/tax-on-dividends
107. gov.uk, Capital Gains Tax rates — https://www.gov.uk/capital-gains-tax/rates
108. gov.uk, Rates and thresholds for employers 2026 to 2027 — https://www.gov.uk/guidance/rates-and-thresholds-for-employers-2026-to-2027
109. gov.uk, Tax on your private pension: annual allowance — https://www.gov.uk/tax-on-your-private-pension/annual-allowance
110. HMRC PTM056510, Money purchase annual allowance — https://www.gov.uk/hmrc-internal-manuals/pensions-tax-manual/ptm056510
111. gov.uk, Work out your tapered annual allowance — https://www.gov.uk/guidance/pension-schemes-work-out-your-tapered-annual-allowance
112. HMRC PTM171000, Lump sum allowance — https://www.gov.uk/hmrc-internal-manuals/pensions-tax-manual/ptm171000
113. HMRC PTM172000, Lump sum and death benefit allowance — https://www.gov.uk/hmrc-internal-manuals/pensions-tax-manual/ptm172000
114. HMRC PTM062210, Normal minimum pension age — https://www.gov.uk/hmrc-internal-manuals/pensions-tax-manual/ptm062210
115. gov.uk, The new State Pension: what you'll get — https://www.gov.uk/new-state-pension/what-youll-get
116. DWP, Benefit and pension rates 2026 to 2027 (PDF) — https://assets.publishing.service.gov.uk/media/69931706ceeaa48d377f6bd5/Benefit-and-pension-rates-2026-2027.pdf
117. gov.uk, State Pension age timetable — https://www.gov.uk/government/publications/state-pension-age-timetable/state-pension-age-timetable
118. gov.uk, Individual Savings Accounts — https://www.gov.uk/individual-savings-accounts
119. gov.uk, ISA reform 2027 anti-circumvention rules factsheet — https://www.gov.uk/government/publications/fiscal-events-2026-factsheets/isa-reform-2027-anti-circumvention-rules-factsheet
120. gov.uk, Technical note: inheritance tax on pensions — https://www.gov.uk/government/publications/inheritance-tax-on-pensions-technical-note/technical-note-inheritance-tax-on-pensions
121. gov.uk, Change to tax rates for property, savings and dividend income — https://www.gov.uk/government/publications/changes-to-tax-rates-for-property-savings-and-dividend-income/change-to-tax-rates-for-property-savings-and-dividend-income-technical-note
122. legislation.gov.uk, National Insurance Contributions (Employer Pensions Contributions) Act 2026 — https://www.legislation.gov.uk/ukpga/2026/15/2026-04-29
123. gov.uk, Maintaining income tax and NICs thresholds until 5 April 2031 — https://www.gov.uk/government/publications/maintaining-income-tax-and-equivalent-national-insurance-contributions-thresholds-until-5-april-2031/income-tax-maintaining-the-personal-allowance-and-the-basic-rate-limit-for-income-tax-and-equivalent-national-insurance-contributions-thresholds-unt
124. HMRC, P9X tax codes to use from 6 April 2026 (PDF) — https://assets.publishing.service.gov.uk/media/6996e9b3b33a4db7ff889e08/P9X_2026_Tax_codes_to_use_from_6_April_2026.pdf
125. Iress, O&M Systems integration: all systems go (2020) — https://www.iress.com/blog/2020/05/om-systems-integration-all-systems-go/
126. Iress, O&M Profiler ESP pricing plans (PDF) — https://www.iress.com/media/documents/OM_Profiler_ESP_Pricing_Plans.pdf
127. Iress, O&M Profiler ESP product page — https://www.iress.com/software/financial-advice/om-profiler-esp/
128. Paraplanners Assembly, Retirement income modelling: O&M vs Selectapension — https://thebigtent.paraplannersassembly.co.uk/discussion/15/retirement-income-modelling-o-m-vs-selectapension-vs-something-else
129. Defaqto, Engage — https://www.defaqto.com/solutions/engage
130. Defaqto, Product and Platform Switching — https://www.defaqto.com/landing-page/product-and-platform-switching
131. Synaptic, Pathways — https://www.synaptic.co.uk/solutions/pathways
132. Salesforce AppExchange, Synaptic Pathways listing — https://appexchange.salesforce.com/appxListingDetail?listingId=a0N3A00000G0tmrUAB
133. FE fundinfo, FE CashCalc — https://www.fefundinfo.com/products/financial-advisers/fe-cashcalc
134. FE fundinfo, FE Analytics — https://www.fefundinfo.com/products/financial-advisers/fe-analytics
135. FE fundinfo, Pension Switching and Platform Due Diligence — https://www.fefundinfo.com/products/financial-advisers/pension-switching-and-platform-due-diligence
136. Money Marketing, Advisers report technical issues with FE Analytics (2022) — https://www.moneymarketing.co.uk/news/advisers-report-technical-issues-with-fe-analytics/
137. Voyant UK, Pricing — https://planwithvoyant.com/uk/resources/pricing
138. Timeline, Planning — https://www.timeline.co/planning
139. Dynamic Planner, Pricing — https://dynamicplanner.com/pricing/
140. Professional Adviser, Dynamic Planner named most used risk profiling tool (Jan 2024) — https://www.professionaladviser.com/news/4161866/dynamic-planner-named-most-risk-profiling-tool-advisers
141. The Paraplanners, Review of Dynamic Planner Cash Flow (Nov 2020, PDF) — https://theparaplanners.com/wp-content/uploads/2020/11/The-Paraplanners-Review-of-Dynamic-Planner-Cash-Flow-1.pdf
142. Money Marketing, Intelliflo to bundle cashflow modelling into core offering (2023) — https://www.moneymarketing.co.uk/news/exclusive-intelliflo-to-bundle-cashflow-modelling-into-core-offering/
143. Paraplanners Assembly, Cashflow planning modellers — https://thebigtent.paraplannersassembly.co.uk/discussion/1089/cashflow-planning-modellers
144. AdvisoryAI, Best financial planning software for UK advice firms 2026 (Sep 2026) — https://advisoryai.com/blog/best-financial-planning-software-for-uk-advice-firms-2026
145. PFS, Paraplanning suitability report writing guide (PDF) — https://media.umbraco.io/ciigroup-dxp/51rbewy1/pfs-suitability-report-writing-guide-digital.pdf
146. Genovo, How to write a pension consolidation report — https://www.genovo.co.uk/how-to-write-a-report-consolidating-one-or-more-existing-pension-plans-into-another-existing-pension-plan/
147. Money Marketing, Tech firm Plannr launches CRM for advisers (2024) — https://www.moneymarketing.co.uk/news/tech-firm-plannr-launches-crm-for-advisers/
148. Quilter, Money purchase critical yield calculator — https://www.quilter.com/solutions/products/pensions/money-purchase-critical-yield-calculator/
149. Hubwise knowledge base, Understanding critical yields — https://knowledgebase.hubwise.co.uk/support/solutions/articles/76000086027-understanding-critical-yields
150. Selectapension application shell, HTTP headers, markup and DNS inspected 6 Sep 2026 — https://app.selectapension.com/
151. Reed, Selectapension jobs search — https://www.reed.co.uk/jobs/selectapension-jobs
152. ipinfo.io, 51.132.215.118 — https://ipinfo.io/51.132.215.118/json
153. Endole, Selectapension brand insight — https://open.endole.co.uk/insight/brand/134117-selectapension
154. Adviser Business Review, How to set up a company like Selectapension (offline) — https://adviserbusinessreview.com/take-set-company-like-selectapension/
155. FT Adviser, Selectapension adds to pension transfer tool (Sep 2019) — https://www.ftadviser.com/pensions/2019/09/03/selectapension-adds-to-pension-transfer-tool/
156. Money Marketing, Selectapension launches cashflow tool (2020) — https://www.moneymarketing.co.uk/news/selectapension-launches-cashflow-tool/
157. Aegon, ARC back-office integration — https://www.aegon.co.uk/adviser/our-solutions/savings-for-individuals/aegon-retirement-choices/back-office-integration
158. legislation.gov.uk, Occupational and Personal Pension Schemes (Disclosure of Information) Regulations 2013, Schedule 6 — https://www.legislation.gov.uk/uksi/2013/2734/schedule/6
159. gov.uk, Claim back tax on a flexibly accessed pension overpayment (P55) — https://www.gov.uk/guidance/claim-back-tax-on-a-flexibly-accessed-pension-overpayment-p55
160. Origo, Introduction to Origo Integration Hub (PDF) — https://origo.com/assets/components/common/Intro-to-Origo-Integration-Hub.pdf
161. FCA, Evaluation Paper 25/1: ban on contingent charging — https://www.fca.org.uk/publications/corporate-documents/evaluation-paper-25-1-ban-contingent-charging-other-remedies
162. Money Marketing, Selectapension launches MiFID II advice tool (2019) — https://www.moneymarketing.co.uk/news/selectapension-launches-mifid-ii-advice-tool/
163. NextWealth, Selectapension directory entry — https://nextwealth.co.uk/companies/selectapension/
164. Selectapension, Training — https://go.selectapension.com/training
165. Selectapension, Portfolio Insight is now available (Apr 2025) — https://selectapension.com/portfolio-insight-is-now-available/
166. gov.uk, Tax on your private pension: pension tax relief — https://www.gov.uk/tax-on-your-private-pension/pension-tax-relief
167. gov.uk, Capital Gains Tax allowances — https://www.gov.uk/capital-gains-tax/allowances
168. HMRC PTM055100, Carry forward — https://www.gov.uk/hmrc-internal-manuals/pensions-tax-manual/ptm055100
169. HMRC PTM044100, Contributions: tax relief for members — https://www.gov.uk/hmrc-internal-manuals/pensions-tax-manual/ptm044100
170. TheyWorkForYou, Written statement on benefit and pension uprating (26 Nov 2025) — https://www.theyworkforyou.com/wms/?id=2025-11-26.hcws1101.h
171. gov.uk, Budget 2025 overview of tax legislation and rates — https://www.gov.uk/government/publications/budget-2025-overview-of-tax-legislation-and-rates-ootlar/budget-2025-overview-of-tax-legislation-and-rates-ootlar
172. gov.uk, Spring Forecast 2026 speech — https://www.gov.uk/government/speeches/spring-forecast-2026-speech
173. gov.uk, Tax on your private pension: lump sum allowance — https://www.gov.uk/tax-on-your-private-pension/lump-sum-allowance
174. HMRC, Pension schemes newsletter 180 (Apr 2026) — https://www.gov.uk/government/publications/pension-schemes-newsletter-180-april-2026/newsletter-180-april-2026
175. Unipass Identity — https://www.unipass.co.uk/
176. Selectapension, Sole trader financial advisers in 2026 — https://selectapension.com/sole-trader-financial-advisers-in-2026/
177. Professional Adviser, Selectapension unveils Pension Monster (2016) — https://www.professionaladviser.com/news/2470464/selectapension-unveils-pension-monster-robo-offering
178. Selectapension, User guides Q&A (Assureweb settings) — https://www.selectapension.com/user-guides-qanda/
179. True Potential, Back-office system — https://www.truepotential.co.uk/financial-adviser/back-office-system/
