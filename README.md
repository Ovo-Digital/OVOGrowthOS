# OVO Growth OS

OVO Digital's persisted operating system for evaluating e-commerce partnerships, selecting commercial terms, recording monthly performance, and closing commissions. The implemented path is:

`lead/brand → evaluation draft → rules-based recommendation → saved scenarios → deal comparison → accept/activate → monthly performance → commission close → portfolio dashboard`

## Stack and boundaries

- `apps/api`: .NET 10 minimal API, EF Core 10, PostgreSQL 17, JWT, OpenAPI, Serilog
- `apps/api/src/OvoGrowthOS.Domain`: dependency-free rules, recommendation, financial, scenario, deal-comparison, commission, close, and portfolio-risk engines
- `apps/api/tests`: domain regression tests plus authenticated HTTP/persistence integration tests
- `apps/web`: Next.js 16 App Router, React 19, TypeScript, Tailwind 4, TanStack Query, Recharts
- `docs`: architecture, domain, financial formulas, workflows, API, and remaining limitations

PostgreSQL is authoritative. Percentages are fractional decimals (`0.05` = 5%); financial values use `decimal(18,4)`. The browser never makes the final recommendation or commission decision.

## Run

With Docker Desktop:

```bash
cp .env.example .env
docker compose up --build
```

Or locally with .NET 10, Node.js 22+, and the configured Supabase PostgreSQL database:

```bash
dotnet restore OvoGrowthOS.sln
dotnet run --project apps/api/src/OvoGrowthOS.Api
cd apps/web && npm ci && npm run dev
```

The local API connection is stored with .NET User Secrets under
`ConnectionStrings:Database`; no database password is committed to this repository.
The API uses Supabase's session pooler on port 5432, which is appropriate for a
persistent local backend. Startup applies pending EF migrations and idempotent seed
data automatically.

Open `http://localhost:3000`; Swagger is at `http://localhost:8080/swagger` under Compose. Development login: `admin@ovodigital.com` / `ChangeMe123!`.

## Persistence and seed

The API applies its migration chain at startup. EF migration history and all application tables live in the private `growth` schema; Supabase's `anon` and `authenticated` Data API roles have no privileges on it. The workflow migration preserves existing brands, evaluations, deals, audit values, and the old unversioned rules as `LegacyRules`; the active engine uses versioned `RuleSets` and `RuleDefinitions`. Seed data creates a published default ruleset, settings, three example brands, one active agreement, and one paid monthly close.

Create future migrations with:

```bash
dotnet ef migrations add MigrationName --project apps/api/src/OvoGrowthOS.Api --startup-project apps/api/src/OvoGrowthOS.Api --output-dir Data/Migrations
```

## Verification

```bash
dotnet test OvoGrowthOS.sln
cd apps/web && npm run lint && npm run build
```

Current verification: 26 domain tests and 4 authenticated API integration tests pass; frontend lint and production build pass; the complete migration chain generates valid PostgreSQL SQL. The API has also completed a live startup, migration check, and idempotent seed run against the configured Supabase PostgreSQL database.

## Security status

JWT expiration is enforced in API and UI. Policies are `ReadAccess` (Admin/Partner/Analyst), `EvaluationWrite` (all three), `OperationsWrite` (Admin/Partner), and `AdminOnly`. The configuration-backed development admin is intentionally not a production identity store; replace it with OIDC or ASP.NET Core Identity before deployment.

See [architecture](docs/ARCHITECTURE.md), [API](docs/API.md), [monthly close](docs/MONTHLY_CLOSE.md), and [roadmap](docs/ROADMAP.md).

## AI-assisted development

Codex, Claude, Cursor and other coding assistants must read [AGENTS.md](AGENTS.md) and [the shared AI development rules](docs/AI_GELISTIRME_KURALLARI.md) before changing the project. Task-specific skills live under `.agents/skills`; compatible discovery adapters are provided under `.claude/skills` and `.cursor/skills`. User-facing changes must update `OVO_GROWTH_OS_KULLANIM_REHBERI.md` in the same change.
