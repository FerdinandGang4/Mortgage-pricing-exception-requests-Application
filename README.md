# Mortgage-pricing-exception-requests-Application
A service that lets internal users create a pricing exception request, review it, and retrieve its outcome.

The backend solution, run instructions, and design documents are in [Backend/BlueHarvest.PricingExceptions](Backend/BlueHarvest.PricingExceptions/README.md).

From the repository root:

```powershell
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --launch-profile http
```

Open http://localhost:5267/swagger in Development. The current implementation contains entity models and enums only; business endpoints and tests will be added incrementally.
