# Blue Harvest Pricing Exceptions

ASP.NET Core controller-based API targeting .NET 10. Implementation is being built incrementally.

## Current step: models and database context

Models/Entities contains User, PricingExceptionRequest, RequestRevision, ReviewDecision, HistoryEntry, and IdempotencyRecord. Models/Enums contains RequestStatus, DecisionOutcome, and UserRole.

These plain C# models describe the class diagram. Data/PricingExceptionsDbContext exposes all six entities and maps their relationships, including the composite revision key. Startup registers EF Core InMemory and initializes an empty database named PricingExceptions.

Data is lost when the application stops. InMemory requires no database server, connection string, or migrations. It does not enforce relational foreign keys or unique indexes and does not provide database transactions; database-backed workflow guarantees will need a relational provider later.

Workflow rules, validation, DTOs, services, controllers, seed data, and tests will be added in later steps.

## Run

From this directory:

```powershell
dotnet build BlueHarvest.PricingExceptions.slnx
dotnet run --project src/PricingExceptions.API --launch-profile http
```

Open http://localhost:5267/swagger in Development. No business endpoints are implemented yet, so Swagger currently shows no operations. Visual Studio launch profiles open Swagger automatically; terminal launches require opening the URL manually.

## Design references

Design documents are organized under docs/EntityClassDiagram, docs/SequenceDiagrams, and docs/Architecture.
