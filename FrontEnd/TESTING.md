# Full application validation

Validated on 3 November 2026 with Windows, .NET 10, Node.js and headless Microsoft Edge.

| Check | Result |
| --- | --- |
| Backend service tests, Release configuration | 21 passed |
| Backend HTTP integration tests | 14 passed |
| Frontend TypeScript and Vite production build | Passed |
| Frontend Oxlint | Passed, no lint warnings |
| Browser tests against the live backend | 9 passed |
| Live API smoke-test script | Passed |
| Frontend, backend proxy and Swagger HTTP checks | 200 OK |

Total automated test cases: **44**, plus live smoke checks.

## Browser coverage

1. Invalid application IDs and blank supporting reasons are rejected before a create is sent.
2. A create response lost after the server saves can be retried with the same idempotency key, producing one request and one history event.
3. A stale review displays a conflict without closing the dialog; refreshing shows the competing decision.
4. Workspace reload retrieves current details by stored IDs. Search and status filters work, and reasons are not persisted in localStorage.
5. Decline records the review note in history.
6. Creation, independent approval, approved-discount lookup, revision and withdrawal of the old effective approval work together.
7. Unknown IDs, role restrictions and mobile navigation are handled.
8. Desktop/mobile layouts, modal dismissal and horizontal-overflow checks pass.

Tests start a separate backend on port 5270 and frontend on 5174. Development servers remain on 5267 and 5173.

## Fixes during validation

- Moved initial workspace state updates after asynchronous reads and rejected obsolete results on role changes.
- Cancelled stale requests during effect cleanup.
- Associated history data with the current request and persona, hiding outdated history during loading.
- Initialized the footer year once instead of constructing a Date during render.
- Expanded the browser suite from four to eight scenarios.

The initial backend rebuild failed because its running executable was locked. The API was stopped, the Release build and tests passed, and the API was restarted. Restarting resets InMemory workflow data.

## Reproduce

From the repository root, stop any running backend that locks the build output, then:

```powershell
dotnet test Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx --configuration Release --no-restore
dotnet build Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx --configuration Debug --no-restore
cd FrontEnd
npm.cmd run build
npm.cmd run lint
npm.cmd run test:e2e
```

For the live smoke script, start the Development backend and run from the repository root:

```powershell
./Backend/BlueHarvest.PricingExceptions/tests/Invoke-ApiSmokeTests.ps1
```

These results validate the implemented local workflows, not production readiness or exhaustive security/performance coverage. InMemory storage, Development-only demo authentication and external mortgage application validation limitations remain as documented.

## Requester and reviewer tracking update

Additional tests verify requester identity is captured from the authenticated user, caller-supplied identity names are ignored, a valid reviewer is required, only the assigned reviewer can decide, and names are retained as historical snapshots after user renaming. The new browser scenario verifies the read-only requester, reviewer dropdown, request details and decision history. Current totals are 35 backend cases and 9 browser cases; frontend build and lint pass.
