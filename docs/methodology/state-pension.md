# State Pension

## State Pension age
From the gov.uk timetable (Pensions Act 2014 and 2007 as amended):
- Born before 6 April 1960: 66.
- Born 6 April 1960 – 5 March 1961: 66 years + n months, where n = 1 for births 6 Apr–5 May 1960,
  2 for 6 May–5 Jun 1960, ... 11 for 6 Feb–5 Mar 1961.
- Born 6 March 1961 – 5 April 1977: 67.
- Born 6 April 1977 – 5 March 1978: 67 years + n months in the same monthly steps.
- Born 6 April 1978 or later: 68 (subject to the ongoing SPA review; flagged as `SubjectToReview`).
`StatePensionAgeFor(dateOfBirth)` returns (years, months) and the exact SPA date; when the
computed date does not exist (e.g. 30 February) it rolls to the first day of the next month, as
DWP does.

## Entitlement
New State Pension full rate 2026/27: £241.30 a week (£12,547.60 a year). Entitlement =
`full × min(1, qualifyingYears / 35)` with a minimum of 10 qualifying years, unless the client
supplies a State Pension forecast amount, which takes precedence. Uprating in projections follows
the assumption set (`StatePensionIncrease`, default = earnings assumption 3.5% nominal, 2.5% real-
equivalent when the plan is in real terms — the triple lock guarantees at least 2.5%).
