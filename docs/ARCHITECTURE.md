# Architecture

OVO Growth OS is a modular monolith. The Next.js client calls an authenticated .NET API; the API owns orchestration and EF Core persistence; the dependency-free domain project owns deterministic business calculations. The dependency direction is `Web → API → Domain`, and PostgreSQL is the source of truth.

Feature routes are grouped around brands, evaluations, rulesets, scenarios, deals, monthly performance, commissions, dashboard, settings, and audit. EF Core is used directly rather than hidden behind a generic repository. Financial and policy code accepts explicit inputs and returns serializable results, which keeps it unit-testable.

Historical integrity is snapshot-based. An analyzed evaluation freezes its inputs, published ruleset, calculation outputs, and recommendation. A generated deal freezes evaluation, rule, financial, commission, and condition data. Later ruleset or setting changes therefore do not rewrite an earlier decision.

JWT policies separate read, evaluation, operations, and administration access. The current development identity is configuration-backed; the policy boundary is ready for a real OIDC/Identity provider, but user lifecycle management is not implemented.
