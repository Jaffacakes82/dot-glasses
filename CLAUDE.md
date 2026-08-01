# CLAUDE.md

Behavioural contract for Claude Code working in this repo. Keep this concise — durable
decisions only, not documentation (that's `README.md`).

## Architecture rules

- Clean Architecture, dependency direction: `Web`/`App` → `Application`/`Contracts`;
  `Application` → `Domain`; `Infrastructure` → `Domain`/`Application` (implements its
  interfaces). Nothing references `Infrastructure` except `Web`'s `Program.cs` (composition
  root) and `AppHost` (orchestration).
- **`DotGlasses.App` may only ever reference `DotGlasses.Contracts`.** If a change seems to
  need `App` to reference anything else, that's a signal the type belongs in `Contracts`
  instead — flag it, don't add the reference.
- No MediatR. Plain application services with interfaces in `Application`, implementations
  alongside.
- Controller-based Web API (not Minimal APIs), versioned from `v1`, Swagger-visible.

## Data scoping vs RBAC — do not conflate

- **Data scoping** (which rows a user can see) is a global EF Core query filter on
  `IHierarchyScoped` entities, keyed off `ICurrentUserContext.HierarchyPathPrefix`. It is
  role-independent.
- **RBAC** (what a user can do with rows they can see) is separate, policy-based
  `IAuthorizationHandler`/`[Authorize(Policy = ...)]` — role-dependent, never touches the
  query filter.
- The only sanctioned way to look outside a caller's hierarchy scope is the explicit
  `IUnscopedReportQueryService` — no ad hoc `.IgnoreQueryFilters()` elsewhere.

## Offline sync (Field App)

New `App` features that write data go through the outbox pattern (IndexedDB pending-sync
table, client-generated GUID as idempotency key, `ISyncService` draining on reconnect) — never
call the API directly from a Blazor page/component.

## Domain modelling

Real domain entities (org hierarchy, lens catalogues, Test/Lead/Sale) are **not yet designed**.
Don't infer a shape from assumptions — check with the user before modelling them. The
`WidgetExample` entity is a deliberately generic placeholder proving the pipeline; it is not a
template to extend with real fields.

## Repo/public-repo constraints

- This repo is public. `/design` (Claude Design handoff bundle) and local Claude Code settings
  are gitignored and must not be committed or referenced from `README.md`.

## Testing

xUnit. EF Core InMemory provider is acceptable for now (`Infrastructure.Tests`).
`WebApplicationFactory` for `Web.Tests` integration tests.

## Running locally

`dotnet run` on the `DotGlasses.AppHost` project (starts Postgres via container, `Web`, and the
Aspire dashboard).

## `[OPEN]` items — implement simplest placeholder, flag, don't guess

- RBAC permission matrix (roles are Admin/Manager/User; only one example policy is wired).
- Role/claim seeding on first run.
- Offline sync conflict resolution (currently last-write-wins; don't hard-code away a future
  version/ETag column).
- Azure Monitor/Application Insights exporter connection string.

This file should grow as real architectural decisions get made — propose updates here when a
significant decision is agreed, not as a one-time artifact.
