# Monthly close

Monthly performance accepts only base operational figures: gross sales, VAT, refunds/cancellations/chargebacks, paid shipping/gift-card topups, order/customer/session counts, COGS and variable cost categories, and channel ad spend. Backend calculation derives net and commissionable revenue, waterfall contribution, AOV, conversion, MER, CAC, return rate, fee breakdown, effective rate, OVO profit, and margins using the active deal snapshot.

Lifecycle: `Draft → UnderReview → Approved → Locked → Invoiced → Paid`. Locked and later periods cannot be edited. Admin may unlock to Approved only with a reason; the action is audited. Exceptional commission adjustments require a reason and actor and are included transparently in the saved breakdown. Invoice and payment transitions update both period and commission status.

The unique `(BrandId, Year, Month)` index prevents duplicate brand periods. Audit records cover creation, edit, transitions, unlock, adjustments, invoice, and payment.
