# Blue Harvest Pricing Exceptions

ASP.NET Core controller-based API targeting .NET 10. Implementation is being built incrementally.

## Current step: service layer

Models/Entities contains User, PricingExceptionRequest, RequestRevision, ReviewDecision, HistoryEntry, and IdempotencyRecord. Models/Enums contains RequestStatus, DecisionOutcome, and UserRole.

These plain C# models describe the class diagram. Data/PricingExceptionsDbContext exposes all six entities and maps their relationships, including the composite revision key. Startup registers EF Core InMemory and initializes an empty database named PricingExceptions.

Data is lost when the application stops. InMemory requires no database server, connection string, or migrations. It does not enforce relational foreign keys or unique indexes and does not provide database transactions; database-backed workflow guarantees will need a relational provider later.

Services and their interfaces are registered through dependency injection:

- PricingExceptionService creates requests, retrieves current terms, submits immutable revisions, approves or declines, and reads history, revisions, and decisions.
- ApprovedDiscountService returns a discount only when the current revision has an approval. It returns null when no current approval exists.
- UserService looks up internal users and enforces roles from stored user records.

Services validate input, ownership, self-review restrictions, expected versions, and revision numbers. Every mutation appends history and stores an idempotent result. Identical retries replay the original result; reuse of a key with different input raises ConflictException. Keys are scoped by caller, operation, and target request.

Current demo policies: one request per application, discounts of 1–500 basis points, reasons up to 2000 characters, and revisions allowed after a decision. A revision makes the request Pending and withdraws the previous effective approval. All known internal users can read requests and history. These policies remain assumptions to confirm.

InMemoryWriteLock serializes service writes within one application process. It is not a substitute for relational transactions or constraints and does not support multiple application instances. Writes use one SaveChanges call for state, history, and the retry record, but InMemory does not guarantee rollback after storage failures. The service owns changes within its DbContext scope; callers must not stage unrelated changes in that scope.

The service actor ID must come from authenticated identity when controllers are added. Authentication, HTTP DTOs, controllers, exception-to-HTTP mapping, seed users, and mortgage application existence validation are still pending. Non-empty application IDs are accepted as external references for now.

## Service validation

```powershell
dotnet test BlueHarvest.PricingExceptions.slnx
```

The tests exercise InMemory workflows: approval followed by revision, decline, preserved history and terms, retries, key conflicts, unauthorized actions, self-review, stale versions, invalid input, duplicate applications, missing requests, and competing decisions across service scopes. They do not validate relational guarantees or HTTP behavior.

## Run

From this directory:

```powershell
dotnet build BlueHarvest.PricingExceptions.slnx
dotnet run --project src/PricingExceptions.API --launch-profile http
```

Open http://localhost:5267/swagger in Development. No business endpoints are implemented yet, so Swagger currently shows no operations. Visual Studio launch profiles open Swagger automatically; terminal launches require opening the URL manually.

## Design references

Design documents are organized under docs/EntityClassDiagram, docs/SequenceDiagrams, and docs/Architecture.
