# Mortgage-pricing-exception-requests-Application
A service that lets internal users create a pricing exception request, review it, and retrieve its outcome.

The backend solution, run instructions, and design documents are in [Backend/BlueHarvest.PricingExceptions](Backend/BlueHarvest.PricingExceptions/README.md).

From the repository root:

```powershell
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --launch-profile http
```

Open http://localhost:5267/swagger in Development. The API exposes the pricing exception workflow using EF Core InMemory. See the backend README for seeded demo users, authentication headers, example requests, and tests.
