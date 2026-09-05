# Roadmap

The persisted operating flow, versioned rules, scenarios, deal choice, monthly close, audit, settings, role policies, DB dashboard, and regression tests are implemented.

Remaining production work is intentionally explicit:

1. Replace the single development admin with OIDC/ASP.NET Core Identity, managed users, refresh/revocation, and tenant boundaries.
2. Add CSV import preview/mapping and provider adapters for Shopify/ad platforms when a real integration is scheduled.
3. Add condition fulfilment/waiver actions, contract document generation, renewal/termination workflows, and invoice-provider integration.
4. Add pagination/filtering to high-volume evaluation, deal, performance, commission, and audit lists.
5. Add PostgreSQL/Compose end-to-end CI, migration backup/restore rehearsal, observability/alerts, and security hardening.
6. Add print/PDF report templates and broader historical cohort/forecast reporting.
