# Mortgage-pricing-exception-requests-Application
A service that lets internal users create a pricing exception request, review it, and retrieve its outcome.

Start with the [delivery package and inventory](Deliverables/README.md) for setup instructions, the demonstration guide, submission documents, and the final checklist.

The backend solution, run instructions, and design documents are in [Backend/BlueHarvest.PricingExceptions](Backend/BlueHarvest.PricingExceptions/README.md).

From the repository root:

```powershell
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --launch-profile http
```

Open http://localhost:5267/swagger in Development. The API exposes the pricing exception workflow using EF Core InMemory. See the backend README for seeded demo users, authentication headers, example requests, and tests.

The [Word assumptions document](Documentation/Assumptions.docx), [requirements coverage and submission notes](Backend/BlueHarvest.PricingExceptions/docs/Architecture/SubmissionNotes.md), and [AI development record](Backend/BlueHarvest.PricingExceptions/docs/Architecture/AiDevelopmentLog.md) describe the exercise policies, verification, and production limitations.
