# Business rules

The seeded published `OVO Default Rules` version contains the initial policy:

- Gross margin below 25% forbids pure revenue share; below 30% favors retainer plus low share.
- Margin bands 30–40%, 40–50%, 50–60%, and 60%+ progressively allow hybrid/tiered and higher share structures.
- Existing monthly revenue at or above the configured threshold favors baseline/incremental pricing.
- Minimum monthly fee is at least internal delivery cost times the configured multiplier.
- Return rate above 25%, stock below 30 days, weak founder cooperation, weak operational readiness, and weak PMF are explicit risk signals.
- The dedicated composite condition `gross margin < 30% AND current ad spend = 0 AND PMF ≤ 2` rejects the opportunity.
- Material unknown financial inputs return `NeedMoreData`, list missing inputs, and prevent a synthetic commercial recommendation.
- Confidence weights are Verified 100, ProvidedByBrand 75, Estimated 40, Unknown 0.

All thresholds that are represented as typed rules are read from the active published ruleset. Global targets and multipliers come from `GeneralSettings`.
