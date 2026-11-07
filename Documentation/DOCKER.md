# Docker and PostgreSQL

Prerequisites: Docker Desktop using Linux containers. Install the .NET 10 SDK for host tests and EF migration commands. Node.js is not required to run the containers.

## Build and run

From the repository root (the folder containing compose.yaml):

```powershell
docker compose up -d --build
docker compose ps
docker compose logs --tail 50 backend postgres frontend
```

Frontend: http://localhost:5173

Swagger: http://localhost:5267/swagger

Compose starts PostgreSQL 17, waits for its readiness check, and starts the API. The API applies checked-in EF Core migrations before serving requests. The frontend is compiled and served by Nginx, which proxies API calls to backend:8080. Stop any host development servers using ports 5173, 5267, or 5432 first.

## Configuration

Defaults work for a local demonstration. Optional root .env settings:

```dotenv
BACKEND_PORT=5267
FRONTEND_PORT=5173
POSTGRES_PORT=5432
POSTGRES_PASSWORD=local-demo-password
```

Compose passes ConnectionStrings__PricingExceptions to the API using Host=postgres, database pricing_exceptions, and username pricing. All published ports bind to localhost. The sample password is only a local development default. Changing POSTGRES_PASSWORD after the database volume is initialized does not change the stored database password; rotate it in PostgreSQL and update the application connection string together.

ASPNETCORE_ENVIRONMENT=Development retains the existing demo users, demo header authentication, and Swagger. No authentication implementation was added.

The local Compose frontend explicitly builds with `VITE_ENABLE_DEMO_USERS=true` so the Select Demo User picker can switch between the seeded managers, reviewers, and Mortgage Process user. The Vite development server enables it automatically. Normal frontend production builds and direct Dockerfile builds default to disabling the picker and omitting `X-User-Id`. Do not enable this build flag for production deployments; the API independently rejects demo headers outside Development.

## Multi-user demo

Development startup seeds these identities idempotently. The original manager and reviewer IDs are retained, and their display names are updated. Existing request and decision name snapshots are preserved.

| User | X-User-Id |
| --- | --- |
| James Wilson (Manager) | 11111111-1111-1111-1111-111111111111 |
| Sarah Johnson (Manager) | 11111111-1111-1111-1111-111111111112 |
| Michael Brown (Manager) | 11111111-1111-1111-1111-111111111113 |
| David Miller (Reviewer) | 22222222-2222-2222-2222-222222222222 |
| Emily Davis (Reviewer) | 22222222-2222-2222-2222-222222222223 |
| Robert Taylor (Reviewer) | 22222222-2222-2222-2222-222222222224 |
| Jennifer Anderson (Reviewer) | 22222222-2222-2222-2222-222222222225 |
| Daniel Thomas (Reviewer) | 22222222-2222-2222-2222-222222222226 |
| Demo Mortgage Process | 33333333-3333-3333-3333-333333333333 |

Select James in the top-bar picker and create three requests with distinct mortgage application IDs. Assign David, Emily, and Robert respectively. Switch to each reviewer, select their request, and approve or decline with a review note. Only the assigned reviewer sees the review action; the API independently enforces this restriction. All known internal users retain the existing ability to read requests and history. The workspace tracks opened request references in browser storage; use Open request to find database requests in a fresh browser.

From FrontEnd, run all isolated browser tests with `npm.cmd run test:e2e`. To test the three-request flow against running Compose instead:

```powershell
$env:E2E_BASE_URL='http://localhost:5173'
npm.cmd run test:e2e -- --grep 'James assigns three requests'
Remove-Item Env:E2E_BASE_URL
```

This deployment test creates three persistent demo requests. The normal suite uses isolated test servers and an InMemory database. Clear E2E_BASE_URL before running the entire isolated suite.

Validated multi-user configuration: all 69 .NET tests and all 13 isolated browser tests passed. The three-request browser test also passed against Compose/PostgreSQL, along with the HTTP workflow smoke test. All nine seeded users resolved through `/users/me`, all five reviewers were returned for assignment, and restarting the backend left user, request, decision, and history counts unchanged.

## Test

After Compose startup, run from the solution directory:

```powershell
cd Backend/BlueHarvest.PricingExceptions
dotnet test
```

The 69 .NET tests use isolated InMemory stores. To exercise the real PostgreSQL-backed application, run the existing smoke test from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Backend/BlueHarvest.PricingExceptions/tests/Invoke-ApiSmokeTests.ps1 -BaseUrl http://localhost:5173
```

This checks creation, retry replay, conflicts, approval, effective discount lookup, revisions, history, validation, existing access rules, and Swagger. It creates demo records in PostgreSQL.

Verify persistence across full container removal and recreation:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Backend/BlueHarvest.PricingExceptions/tests/Test-DockerPersistence.ps1
```

This creates and approves a demo request, runs Compose down/up without deleting volumes, and verifies the request, revision, decision, history, and original retry response. All 69 .NET tests, PostgreSQL workflow smoke tests, and this persistence check passed for this configuration.

## Restart and persistence

From the repository root:

```powershell
docker compose restart
docker compose down
docker compose up -d
# Rebuild after source changes:
docker compose up -d --build
```

The postgres_data named volume retains requests, revisions, decisions, users, history, and retry records across restarts, image rebuilds, and ordinary docker compose down/up. The previous InMemory store cannot be migrated because it had no durable data. Do not use docker compose down -v unless you intend to delete the database volume and its contents.

## EF Core migrations

From the repository root, restore the pinned local tool:

```powershell
dotnet tool restore
```

The API automatically applies migrations on startup. To apply them explicitly from the host, start PostgreSQL first:

```powershell
docker compose up -d postgres
dotnet ef database update --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API
```

The default host connection in appsettings.json uses localhost:5432 and the sample development credentials. If your settings differ, set the connection string before using EF or running the API:

```powershell
$env:ConnectionStrings__PricingExceptions='Host=localhost;Port=5432;Database=pricing_exceptions;Username=pricing;Password=your-password'
```

After changing the EF model, generate and inspect a new migration, then rebuild:

```powershell
dotnet ef migrations add DescribeSchemaChange --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --output-dir Data/Migrations
docker compose up -d --build
```

## Run the API outside Docker

Start only the database, then run the existing launch profile:

```powershell
docker compose up -d postgres
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --launch-profile http
```

Do not also run the Compose backend on port 5267. Database__Provider=InMemory remains available explicitly for isolated tests; normal application startup uses PostgreSQL.
