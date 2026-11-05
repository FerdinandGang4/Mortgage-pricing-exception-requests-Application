# Mortgage-pricing-exception-requests-Application
A service that lets internal users create a pricing exception request, review it, and retrieve its outcome.

For container build, run, test, and restart commands, see [Docker instructions](DOCKER.md).

Start with the [delivery package and inventory](Deliverables/README.md) for setup instructions, the demonstration guide, submission documents, and the final checklist.

The backend solution, run instructions, and design documents are in [Backend/BlueHarvest.PricingExceptions](Backend/BlueHarvest.PricingExceptions/README.md).

From the repository root:

```powershell
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --launch-profile http
```

Start PostgreSQL first with `docker compose up -d postgres`. Open http://localhost:5267/swagger in Development. The API uses PostgreSQL and applies EF Core migrations on startup. See [Docker instructions](DOCKER.md) for persistent storage and connection settings, and the backend README for demo users and example requests.

The [Word assumptions document](Documentation/Assumptions.docx), [requirements coverage and submission notes](Backend/BlueHarvest.PricingExceptions/docs/Architecture/SubmissionNotes.md), and [AI development record](Backend/BlueHarvest.PricingExceptions/docs/Architecture/AiDevelopmentLog.md) describe the exercise policies, verification, and production limitations.
