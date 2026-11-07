# Blue Harvest lending workspace

Frontend design: [Word document](../Backend/BlueHarvest.PricingExceptions/docs/BlueHarvest_Frontend_Design.docx) · [Markdown and diagrams](Design/Frontend_Design.md). Covers component structure, responsibilities, API data flow, state management, and responsive behaviour.

React + TypeScript interface for the mortgage pricing exception API, built with Vite. It uses live backend responses and a responsive desktop/mobile layout.

## Run locally

Start PostgreSQL and the backend in Development from the repository root. Stop the Compose backend/frontend first if they already use the local development ports:

```powershell
docker compose stop frontend backend
docker compose up -d postgres
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --launch-profile http
```

Then open another terminal:

```powershell
cd FrontEnd
npm.cmd ci
npm.cmd run dev
```

Open http://localhost:5173. The frontend is a separate process. Vite proxies /api and /openapi to http://localhost:5267, so local development does not require backend CORS changes. If the backend uses another port, set API_PROXY_TARGET before starting Vite:

These defaults assume the Compose database's default port and demo password. If you override `POSTGRES_PORT` or `POSTGRES_PASSWORD`, set the backend's `ConnectionStrings__PricingExceptions` environment variable to the matching local connection string before `dotnet run`. See [DOCKER.md](../DOCKER.md).

```powershell
$env:API_PROXY_TARGET = 'http://localhost:5267'
npm.cmd run dev
```

On other platforms use npm instead of npm.cmd.

## Components and workflow

New requests automatically show the requester profile from the authenticated backend identity. Select an assigned reviewer from the API-backed dropdown. Request details display requester and assigned-reviewer IDs and names, while completed decisions also show the acting reviewer name/ID and decision time. Assignment is preserved when revising; only the selected reviewer can decide.

- Sidebar and Header: responsive navigation, backend connectivity and demo persona selection.
- RequestSummary: counts for requests opened in this browser workspace.
- PricingExceptionTable: search by request/application ID or reason, filter by status, select request details.
- RequestDetails: current terms, reviewer/time, copyable request ID and role-aware actions.
- RequestFormDialog: create a request or submit a new immutable revision, with validation and approval-withdrawal notice.
- ReviewRequestDialog: independent approval/decline with optional review notes and an exact revision/version.
- RequestHistory: chronological events linked to preserved terms and review decisions.
- RequestDetailsDialog: load all existing backend requests, select one from a dropdown, preview it and open its latest details.
- ApprovedDiscountLookup: mortgage-process lookup of the current approved discount.
- Modal, StatusBadge, Icon and Feedback: shared accessible interface elements.

Select a demo role in the header: Relationship manager, Independent reviewer, or Mortgage process. Requests use the seeded backend identity for that role. This is Development-only identity simulation, not a production login.

Create a request as the manager. Select it, switch to the reviewer, review it, then use the mortgage-process role to look up its approved discount by application ID. Submit revisions as the original manager. The revised request returns to Pending; earlier terms and decisions remain visible in History. Decline and backend conflict messages are also supported.

## Workspace behavior

The header sun/moon button switches the entire workspace between light and dark mode. The selected theme is stored under `blueharvest.theme` and restored on reload; switching still works when browser storage is unavailable. Shared semantic colors live in `src/theme.css`, including surfaces, controls, statuses, purple accents, and application chrome. The initial theme is light.

The Find Request dropdown uses GET /api/v1/pricing-exceptions to list all existing backend requests, including requests not previously opened in this browser. The main workspace remembers up to 100 request IDs created or opened in the current browser and reloads their current details from the API. Dashboard totals describe that workspace, not all backend requests. Use Find Request in another browser to browse and select any existing request.

Only request IDs are stored in localStorage; supporting reasons and decision details are read from the backend. Unknown IDs are removed during refresh, for example after the InMemory backend restarts. The workspace still functions when browser storage is unavailable.

POST commands generate idempotency keys and reuse the key when retrying unchanged form input while the dialog remains open. Changing the input generates a new key. Stale versions and other backend failures stay visible without silently replacing reviewed terms. For a conflict, close the dialog, refresh the workspace, inspect the new terms and try again.

The connection indicator reports whether the Development OpenAPI endpoint responds. Refresh reloads workspace requests. No fake requests or precomputed portfolio metrics are displayed.

## Checks

See [TESTING.md](TESTING.md) for the combined frontend/backend validation results and environment variables. The browser suite contains twenty scenarios, including theme persistence and contrast, lost-response retry, stale decisions, validation, workspace reload, and responsive navigation.

```powershell
npm.cmd run build
npm.cmd run lint
```

Browser tests use Microsoft Edge on Windows and Chromium on other platforms. With the .NET 10 SDK installed, run from FrontEnd; Playwright builds and starts the backend from its current source:

```powershell
npm.cmd run test:e2e
```

On non-Windows systems install the browser with npx playwright install chromium first. The tests start an isolated backend on port 5270 and frontend on port 5174. They cover approval, revision, approved-discount withdrawal, decline, history, role restrictions, lookup failures, modal dismissal, filtering and responsive navigation. Screenshots and failure traces are ignored by Git under test-results.

The production build is emitted to dist. Serve production assets with same-origin /api routing or an appropriate reverse proxy; the Vite development proxy is not part of the production build. The application backend uses PostgreSQL and Development-only demo authentication. Only isolated service, HTTP, and browser tests select InMemory; the separate PostgreSQL test project exercises the real database.

Typography is self-hosted using DM Sans and Manrope. Their SIL Open Font License notices are distributed under public/licenses. No external font-service connection is needed.

## Code layout

```text
src/
  api/          Typed backend client and Problem Details handling
  components/   Reusable workspace and workflow components
  hooks/        Workspace state and operation retry keys
  lib/          Persona definitions, validation and formatting
  types/        Backend request/response types
  App.tsx       Workspace composition and active view
```
