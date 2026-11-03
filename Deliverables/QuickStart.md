# Run and test the application

## Prerequisites

- .NET 10 SDK.
- A Git checkout of the complete repository.
- NuGet access for the first restore. No paid service or private infrastructure is required.

All commands below run from the repository root.

## Restore, build and test

```powershell
dotnet restore Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx
dotnet build Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx --configuration Release --no-restore
dotnet test Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx --configuration Release --no-build --no-restore
```

The suite currently contains 27 cases: 18 service tests and 9 HTTP integration tests. Tests create isolated stores and do not require a separately running API.

## Start the API

```powershell
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --configuration Release --no-build --launch-profile http
```

Open http://localhost:5267/swagger in a browser. The OpenAPI document is available at http://localhost:5267/openapi/v1.json. Terminal startup does not automatically open a browser.

For Visual Studio, open the solution, select PricingExceptions.API as the startup project, choose the http profile and start debugging.

## Demo identities

Supply X-User-Id on Swagger operations:

| Role | ID |
| --- | --- |
| RelationshipManager | 11111111-1111-1111-1111-111111111111 |
| Reviewer | 22222222-2222-2222-2222-222222222222 |
| MortgageProcess | 33333333-3333-3333-3333-333333333333 |

Each POST also needs a non-blank Idempotency-Key. Use a new key for a new operation and the same key/payload for an exact retry. See [DemoGuide.md](DemoGuide.md).

These identities simulate authentication for local demonstrations only. The header is disabled outside Development.

## Live smoke check

With the API running, open another terminal:

```powershell
./Backend/BlueHarvest.PricingExceptions/tests/Invoke-ApiSmokeTests.ps1
```

The script writes fresh demo requests and tests the HTTP workflow, access checks and Swagger. If your machine blocks PowerShell scripts, use the automated dotnet tests and perform the Swagger walkthrough; follow your organization's script execution policy.

## Stop, reset and troubleshooting

Press Ctrl+C in the API terminal to stop it. Restarting clears all InMemory workflow data and recreates the demo users. Stop the API before rebuilding if Windows reports a locked executable.

If the port is already in use, stop the earlier instance or choose another URL and pass that URL to the smoke script with its BaseUrl parameter. If Windows Application Control blocks the compiled API, follow the machine's development application policy or contact its administrator.

No migrations, database server, connection string or database reset command are required.
