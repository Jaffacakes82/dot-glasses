# DOT Glasses

DOT Glasses provides affordable prescription eyewear across East Africa. This repository holds
two front ends sharing one backend data model:

1. **Admin & MI Portal** — server-rendered MVC web app for internal staff (org hierarchy, users,
   lens catalogues, custom orders, reporting), which also exposes the API consumed by the field
   app.
2. **Field Distribution App** — a Blazor WebAssembly PWA, installable on low/mid-range Android
   phones, used fully offline by field agents to record vision tests, leads, and sales.

This is currently an **architectural skeleton**: real domain entities (org hierarchy, lens
catalogues, Test/Lead/Sale) are not yet designed. Every cross-cutting concern — audit/soft
delete, data scoping, RBAC, offline sync, observability — is proven end-to-end using one
deliberately generic placeholder entity, `WidgetExample`, so the patterns are established
before real entities are dropped in. See [CLAUDE.md](CLAUDE.md) for the behavioural rules this
repo is built against.

Both front ends now also have a full **UI skeleton** matching the design system handoff
(colors/type/spacing tokens in each project's `wwwroot/css/dot-glasses.css`, component patterns
described in `CLAUDE.md`): every screen from the design mockups is a real, navigable route, but
populated with static placeholder data rather than a database — not wired to real domain
entities for the same reason as above.

## Solution structure

```
DotGlasses.sln
  /src
    DotGlasses.Domain          — entities, value objects, domain interfaces. No EF Core references.
    DotGlasses.Application     — application services (interfaces + implementations), no MediatR.
    DotGlasses.Contracts       — DTOs, shared FluentValidation validators, enums. The only project
                                  both Web and App are allowed to depend on for cross-boundary types.
    DotGlasses.Infrastructure  — EF Core DbContext, migrations, repositories, interceptors,
                                  ASP.NET Identity configuration.
    DotGlasses.Web             — ASP.NET Core MVC (Admin Portal) + Web API in one project.
                                  Cookie auth for MVC, JWT for API consumers.
    DotGlasses.App             — Blazor WebAssembly PWA (Field App). References Contracts only.
    DotGlasses.ServiceDefaults — .NET Aspire shared project: OpenTelemetry, health checks, resilience.
    DotGlasses.AppHost         — .NET Aspire orchestration for local dev (Postgres container,
                                  dashboard) and the source model azd/Aspire use to generate
                                  Bicep for Azure Container Apps deployment.
  /tests
    DotGlasses.Application.Tests    — xUnit
    DotGlasses.Infrastructure.Tests — xUnit, EF Core InMemory provider
    DotGlasses.Web.Tests             — xUnit, WebApplicationFactory
  /infra                        — Bicep output from azd/Aspire (generated, not hand-authored —
                                    regenerate with `azd infra gen --force`, don't hand-edit)
```

Dependency direction: `Web`/`App` → `Application`/`Contracts`; `Application` → `Domain`;
`Infrastructure` → `Domain`/`Application`. Nothing references `Infrastructure` except `Web`'s
`Program.cs` (composition root) and `AppHost` (orchestration). `DotGlasses.App` may only ever
reference `DotGlasses.Contracts`.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) or later
- Docker Desktop (or Podman) — AppHost runs Postgres in a container
- [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) — only needed for deployment, not local dev

No .NET Aspire *workload* install is required — the Aspire templates and packages used here are
plain NuGet (`dotnet new install Aspire.ProjectTemplates` was run once to scaffold this repo;
you don't need to repeat that to build or run it).

## Running locally

```bash
dotnet run --project src/DotGlasses.AppHost
```

This starts Postgres in a container, runs `DotGlasses.Web` (Admin Portal + API), and opens the
Aspire dashboard (logs/traces/metrics for every service in one place). The EF Core migration
applies automatically — nothing manual to run first.

A dev admin user and the `Admin`/`Manager`/`User` roles are seeded automatically on startup —
credentials are in `src/DotGlasses.Web/appsettings.Development.json` (`DevSeed` section). This
is a placeholder pending real account provisioning — see the `[OPEN]` items in
[CLAUDE.md](CLAUDE.md).

Swagger is available at `/swagger` in development, with a "Bearer" auth box for pasting a JWT
obtained from `POST /api/v1/auth/login`.

To run the Field App (`DotGlasses.App`) against the running API, in a second terminal:

```bash
dotnet run --project src/DotGlasses.App
```

It expects the API at the URL configured in `src/DotGlasses.App/wwwroot/appsettings.json`
(`ApiBaseUrl`) — this defaults to `DotGlasses.Web`'s fixed dev HTTPS port
(`src/DotGlasses.Web/Properties/launchSettings.json`). If AppHost assigns a different port,
check the Aspire dashboard and update `ApiBaseUrl` accordingly.

## Running tests

```bash
dotnet test DotGlasses.sln
```

`DotGlasses.Infrastructure.Tests` includes the security-critical hierarchy-scoping query filter
tests (root/leaf/sibling-prefix edge cases) and the audit interceptor tests, both against EF
Core InMemory. `DotGlasses.Web.Tests` uses `WebApplicationFactory` with the same InMemory
provider swapped in for Postgres, and exercises the RBAC policy example end-to-end (401 with no
token, 403 for an authenticated-but-under-privileged role, 201 for Admin).

## Exercising offline sync locally

1. Start `AppHost`, then `DotGlasses.App` as above; log in via the App's `/login` page using
   the dev admin credentials.
2. Go to `/widget-examples`, open browser devtools → Network → set throttling to **Offline**.
3. Create a Widget Example — it's written to IndexedDB with a `PendingSync` status and shown
   immediately in the list.
4. Set the network back to **Online**. Within ~30 seconds (or immediately, via the browser
   `online` event) the outbox drains and the row flips to `Synced`.
5. Refresh the page — the record is now also returned by the server, and re-triggering a sync
   (e.g. by reloading while still holding the same pending item) is a no-op: the create endpoint
   upserts by the client-generated id, so retried syncs never duplicate.

Batched client-side log entries (warnings, errors, lifecycle events) follow the same outbox path
to `POST /api/v1/client-logs` — you can watch them arrive in the Aspire dashboard's log view for
`DotGlasses.Web`, tagged with the client's session correlation id.

## Deployment

Two independent `azd` projects, deployed separately:

- **Root (`azure.yaml`)** — `DotGlasses.Web` (Admin Portal + API) to Azure Container Apps and
  Postgres to Azure Database for PostgreSQL Flexible Server, both generated from the
  `DotGlasses.AppHost` model. `/infra` is generated output (`azd infra gen`), not
  hand-maintained — regenerate it after AppHost changes rather than editing it directly. Run
  `azd up` from the repo root.
- **`src/DotGlasses.App/azure.yaml`** — the Field App PWA to Azure Static Web Apps, kept
  separate because `azd` doesn't currently allow mixing an Aspire-generated service with a
  hand-declared one in the same project, and no Aspire hosting integration for Static Web Apps
  exists to fold it into the root project instead. Run `azd up` from `src/DotGlasses.App`.

Neither has been provisioned against a real subscription yet — see [CLAUDE.md](CLAUDE.md) for
what's still `[OPEN]` before that happens (production JWT signing key, Application Insights
connection string, and the PWA's `ApiBaseUrl` needing to point at the deployed API origin once
that's known, among others).
