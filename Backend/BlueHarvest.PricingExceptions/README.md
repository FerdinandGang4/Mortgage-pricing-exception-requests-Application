# Blue Harvest Pricing Exceptions

ASP.NET Core controller-based API targeting .NET 10. Implementation is being built incrementally.

## Current step: models and entities

Models/Entities contains User, PricingExceptionRequest, RequestRevision, ReviewDecision, HistoryEntry, and IdempotencyRecord. Models/Enums contains RequestStatus, DecisionOutcome, and UserRole.

These plain C# models describe the class diagram. Database mapping, workflow rules, validation, DTOs, services, controllers, and tests will be added in later steps. Their folders will be created when implementation begins.

## Run

From this directory:

```powershell
dotnet build BlueHarvest.PricingExceptions.slnx
dotnet run --project src/PricingExceptions.API --launch-profile http
```

Open http://localhost:5267/swagger in Development. No business endpoints are implemented yet, so Swagger currently shows no operations. Visual Studio launch profiles open Swagger automatically; terminal launches require opening the URL manually.

## Design references

Design documents are organized under docs/EntityClassDiagram, docs/SequenceDiagrams, and docs/Architecture.
