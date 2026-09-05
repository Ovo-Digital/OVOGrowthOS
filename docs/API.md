# API

All routes except login, health, and Swagger require a bearer JWT. Responses use string enums and errors use Problem Details.

- Auth: `POST /api/auth/login`, `GET /health`
- Brands: `GET|POST /api/brands`, `GET /api/brands/{id}`
- Evaluations: `GET|POST /api/evaluations`, `GET|PUT /api/evaluations/{id}`, `POST .../{id}/analyze`, `POST .../{id}/approve`
- Rules: list/detail/create/clone; add or edit draft rules; publish/archive under `/api/rulesets`
- Scenarios: list/calculate/save/duplicate/rename/prefer/delete under `/api/evaluations/{evaluationId}/scenarios`
- Deals: list/detail, generate from approved evaluation, compare, accept, activate under `/api/deals`
- Performance: list/detail/create/update, submit/approve/lock/admin-unlock, adjustments, invoice, and pay under `/api/performance`
- Commissions: `GET /api/commissions`
- Operations: `GET /api/dashboard`, `GET|PUT /api/settings`, `GET /api/audit`

`GET /api/dashboard` is derived from persisted latest-period performance and history. It returns portfolio revenue/profit/margin, MER, commission states, investment exposure, concentration measures, brand health, and a 12-period trend.

Policies: Analyst can read and create/update/analyze evaluations and scenarios; Partner can also create/activate deals and operate close; Admin additionally manages rules, settings, and unlocks.
