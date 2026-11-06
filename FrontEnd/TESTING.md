# Application checks and browser tests

Run frontend commands from `FrontEnd`. Windows examples use `npm.cmd`; use `npm` on macOS/Linux. Install the .NET 10 SDK and Node.js 24, then run `npm.cmd ci` if frontend dependencies are not installed.

## Build and lint

```powershell
npm.cmd run build
npm.cmd run lint
```

The build uses strict TypeScript checks for application code, Vite configuration, Playwright configuration, and browser tests before building production assets. Unused local variables and parameters are checked by TypeScript.

## Isolated browser tests

```powershell
$env:E2E_BASE_URL = $null
npm.cmd run test:e2e
```

Playwright builds and runs the current backend project in Development with an isolated InMemory database on port 5270, and starts Vite on port 5174. A previously built Debug DLL is not required. Both ports must be free; existing servers are not reused. Playwright shuts down its temporary servers after the run. This mode does not change the application Compose database.

The tests use Microsoft Edge on Windows. On Linux/macOS install Chromium with `npx playwright install chromium` (on Linux CI, `npx playwright install --with-deps chromium`).

## Test the running Docker application

From the repository root, first start the application:

```powershell
docker compose up --build -d
docker compose ps
cd FrontEnd
$env:E2E_BASE_URL = 'http://localhost:5173'
npm.cmd run test:e2e
```

An explicit URL prevents Playwright from starting local servers. These tests create labelled demo requests in the running PostgreSQL database. Existing data remains intact. To return to isolated tests, clear `E2E_BASE_URL` as shown above.

## Environment variables

| Variable | Used by | Default and effect |
| --- | --- | --- |
| `E2E_BASE_URL` | Playwright | Unset or blank starts temporary servers. A non-blank URL tests an existing application. Whitespace is trimmed. |
| `API_PROXY_TARGET` | Vite | Defaults to `http://localhost:5267`. Both `/api` and `/openapi` use the same target. Unset or blank values use the default. Isolated browser tests set it to port 5270. |
| `VITE_ENABLE_DEMO_USERS` | Frontend build | Normal production builds disable demo switching. The local Docker build explicitly sets `true`; Vite development also enables it. |
| `ASPNETCORE_ENVIRONMENT` | Backend | Browser tests set Development, where seeded identities and OpenAPI are available. The demo identity header is rejected outside Development. |
| `Database__Provider` | Backend tests | Temporary browser servers set InMemory. Normal application deployments use PostgreSQL. |
| `ConnectionStrings__PricingExceptions` | Backend | Override the local database connection when using a custom host, port, database, or password. Never use the application database for isolated database tests. |
| `POSTGRES_PORT`, `POSTGRES_PASSWORD`, `BACKEND_PORT`, `FRONTEND_PORT` | Docker Compose | Local overrides documented in [DOCKER.md](../DOCKER.md). If the frontend port changes, match it in `E2E_BASE_URL`. |

## Coverage and counts

The browser suite has 20 cases covering request creation, reviewer attribution, validation, approval, decline, approved-discount retrieval, revisions, history, retry safety, stale decisions, stored references, demo-user switching (including delayed responses), reviewer assignment, and desktop/mobile navigation. Six theme cases additionally cover keyboard operation, persistence, light/dark surfaces, measured text and form-control contrast, the dark-mode review workflow, blocked browser storage, and mobile layout. Theme contrast assertions target a minimum 4.5:1 for sampled text and 3:1 for sampled control boundaries; they are focused checks rather than a complete accessibility audit.

The backend has 56 unit cases, 118 HTTP integration cases, and 10 isolated PostgreSQL cases: 184 backend cases. See the [backend test guide](../Backend/BlueHarvest.PricingExceptions/tests/README.md) for the full catalog and commands. Running that full solution requires Docker for the separate PostgreSQL test container.

```powershell
# From Backend/BlueHarvest.PricingExceptions:
dotnet restore
dotnet build --configuration Release --no-restore -warnaserror
dotnet test --configuration Release --no-build --no-restore --logger "console;verbosity=normal"
```

Browser failure screenshots and traces are written to the ignored `FrontEnd/test-results` directory. Passing these checks validates the exercised workflows; distributed concurrency, production identity, exhaustive performance/security checks, and external mortgage-system integration remain outside this demo's guarantees.
