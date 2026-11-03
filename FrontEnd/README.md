# Blue Harvest lending workspace

React + TypeScript interface for the mortgage pricing exception API, built with Vite. It uses live backend responses and a responsive desktop/mobile layout.

## Run locally

Start the backend in Development from the repository root:

```powershell
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --launch-profile http
```

Then open another terminal:

```powershell
cd FrontEnd
npm.cmd ci
npm.cmd run dev
```

Open http://localhost:5173. The frontend is a separate process. Vite proxies /api and /openapi to http://localhost:5267, so local development does not require backend CORS changes. If the backend uses another port, set API_PROXY_TARGET before starting Vite:

```powershell
$env:API_PROXY_TARGET = 'http://localhost:5267'
npm.cmd run dev
```

On other platforms use npm instead of npm.cmd.

## Components and workflow

New requests automatically show the requester profile from the authenticated backend identity. Select an assigned reviewer from the API-backed dropdown. Request details display requester and assigned-reviewer IDs and names, while completed decisions also show the acting reviewer name/ID and decision time. Assignment is preserved when revising; only the selected reviewer can decide.

- Sidebar and Header: responsive navigation, backend connectivity and demo persona selection.
- StatCards: counts for requests opened in this browser workspace.
- RequestTable: search by request/application ID or reason, filter by status, select request details.
- RequestDetails: current terms, reviewer/time, copyable request ID and role-aware actions.
- RequestFormDialog: create a request or submit a new immutable revision, with validation and approval-withdrawal notice.
- ReviewDialog: independent approval/decline with optional review notes and an exact revision/version.
- HistoryTimeline: chronological events linked to preserved terms and review decisions.
- OpenRequestDialog: load all existing backend requests, select one from a dropdown, preview it and open its latest details.
- ApprovedDiscountLookup: mortgage-process lookup of the current approved discount.
- Modal, StatusBadge, Icon and Feedback: shared accessible interface elements.

Select a demo role in the header: Relationship manager, Independent reviewer, or Mortgage process. Requests use the seeded backend identity for that role. This is Development-only identity simulation, not a production login.

Create a request as the manager. Select it, switch to the reviewer, review it, then use the mortgage-process role to look up its approved discount by application ID. Submit revisions as the original manager. The revised request returns to Pending; earlier terms and decisions remain visible in History. Decline and backend conflict messages are also supported.

## Workspace behavior

The Open request dropdown uses GET /api/v1/pricing-exceptions to list all existing backend requests, including requests not previously opened in this browser. The main workspace remembers up to 100 request IDs created or opened in the current browser and reloads their current details from the API. Dashboard totals describe that workspace, not all backend requests. Use Open request in another browser to browse and select any existing request.

Only request IDs are stored in localStorage; supporting reasons and decision details are read from the backend. Unknown IDs are removed during refresh, for example after the InMemory backend restarts. The workspace still functions when browser storage is unavailable.

POST commands generate idempotency keys and reuse the key when retrying unchanged form input while the dialog remains open. Changing the input generates a new key. Stale versions and other backend failures stay visible without silently replacing reviewed terms. For a conflict, close the dialog, refresh the workspace, inspect the new terms and try again.

The connection indicator reports whether the Development OpenAPI endpoint responds. Refresh reloads workspace requests. No fake requests or precomputed portfolio metrics are displayed.

## Checks

See [TESTING.md](TESTING.md) for the combined frontend/backend validation results. The browser suite contains eleven scenarios, including lost-response retry, stale decisions, validation, workspace reload, and responsive navigation.

```powershell
npm.cmd run build
```

Browser tests use Microsoft Edge on Windows and Chromium on other platforms. Build the backend first, then run from FrontEnd:

```powershell
dotnet build ../Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx
npm.cmd run test:e2e
```

On non-Windows systems install the browser with npx playwright install chromium first. The tests start an isolated backend on port 5270 and frontend on port 5174. They cover approval, revision, approved-discount withdrawal, decline, history, role restrictions, lookup failures, modal dismissal, filtering and responsive navigation. Screenshots and failure traces are ignored by Git under test-results.

The production build is emitted to dist. Serve production assets with same-origin /api routing or an appropriate reverse proxy; the Vite development proxy is not part of the production build. The backend currently uses InMemory and Development-only demo authentication.

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
