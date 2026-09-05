# Financial engine

`CommissionCalculator` and `DealCommissionCalculator` implement flat revenue share, marginal tiered revenue share, retainer plus share, minimum fee plus share, incremental share, retainer plus incremental share, contribution-profit share, and fixed retainer. Tier results include a per-bracket breakdown, pre-minimum fee, applied minimum/retainer, adjustments, and effective rate.

Marginal tiers charge only revenue inside each bracket. For ₺2M at 8% / 6% / 4.5% brackets the result is ₺122,500; this is a regression test.

The contribution waterfall is:

`gross sales − VAT − refunds − cancellations − chargebacks + paid shipping − gift-card topups = net/commissionable revenue`

Then COGS and variable costs produce gross and pre-marketing contribution; ad spend produces contribution before OVO; commission produces brand contribution profit. Derived KPIs include AOV, conversion, MER, CAC, return rate, brand contribution margin, OVO gross profit/margin, break-even MER, allowable ad spend, and setup payback. Division-by-zero returns zero rather than NaN/Infinity. Backend decimal values are the final authority.
