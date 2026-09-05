# Domain model

- `Brand` owns contact/classification data, one `BrandEconomics`, evaluation history, deals, and monthly performance.
- `BrandEvaluation` is a resumable draft until analysis. Analysis records confidence, partnership score, decision, recommendation, structured conditions, `RuleSetId`/version, and four immutable JSON snapshots.
- `RuleSet` contains typed `Rule` rows and moves `Draft → Published → Archived`. Published versions are immutable; cloning creates the next draft version.
- `Scenario` stores named commercial assumptions, commission tiers, calculated output, and preferred state under an evaluation.
- `Deal` stores exact commercial structure, baseline method/period, terms, conditions, lifecycle status, and frozen source snapshots.
- `MonthlyPerformance` stores base source figures and backend-derived revenue, waterfall, KPI, commission, and OVO profitability fields. `CommissionAdjustment` is an append-only exceptional adjustment with reason and actor.
- `GeneralSettings` contains global operating defaults. `AuditRecord` captures actor, action, entity, old/new JSON, timestamp, and reason.

Important enums include all eight deal types, evaluation/deal/monthly-close lifecycles, confidence levels, typed rule fields/operators/effects, and commission state.
