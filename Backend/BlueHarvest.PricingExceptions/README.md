# Blue Harvest Pricing Exceptions

ASP.NET Core controller-based API targeting .NET 10. Implementation is being built incrementally.

## Current step: controllers and HTTP workflow

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

Controllers obtain actor IDs from authenticated claims. Development authentication accepts an X-User-Id header for one of the seeded demo identities below. This header is deliberately disabled outside Development; configure a trusted identity provider before running authenticated workflows in another environment. No production identity provider is configured yet. Mortgage application existence validation is pending; non-empty application IDs are accepted as external references for now.

## Test in Swagger

Open http://localhost:5267/swagger, expand an operation and select Try it out. Supply X-User-Id using the appropriate seeded identity:

| Role | X-User-Id |
| --- | --- |
| RelationshipManager | 11111111-1111-1111-1111-111111111111 |
| Reviewer | 22222222-2222-2222-2222-222222222222 |
| MortgageProcess | 33333333-3333-3333-3333-333333333333 |

| Method | Route | Role |
| --- | --- | --- |
| POST | /api/v1/pricing-exceptions | RelationshipManager |
| GET | /api/v1/pricing-exceptions/{id} | Known internal user |
| POST | /api/v1/pricing-exceptions/{id}/revisions | Owning RelationshipManager |
| POST | /api/v1/pricing-exceptions/{id}/decisions | Reviewer |
| GET | /api/v1/pricing-exceptions/{id}/history | Known internal user |
| GET | /api/v1/mortgage-applications/{id}/approved-discount | MortgageProcess |

1. Create as the manager with an Idempotency-Key such as create-001:

```json
{
  "mortgageApplicationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "discountBps": 50,
  "reason": "Competitive rate offer"
}
```

2. Copy the returned request id. Submit a decision as the reviewer with a new key such as review-001:

```json
{
  "outcome": "Approved",
  "expectedVersion": 1,
  "reviewedRevisionNumber": 1,
  "reason": "Accepted"
}
```

3. Retrieve the approved discount using the mortgage application ID and the MortgageProcess identity.
4. Submit revised terms as the original manager using expectedVersion 2, expectedRevisionNumber 1, and a new idempotency key. The request becomes Pending; approved-discount lookup returns 404 until the new revision is approved. History retains the original terms and decision.

Every POST requires Idempotency-Key. Reuse a key only for an identical retry of the same operation. Create and revision return 201 with a Location header; reads and decisions return 200. Errors use Problem Details: 400 invalid input, 401 missing/invalid identity, 403 forbidden operation, 404 unknown request/no current approval, and 409 conflicting changes or retry payload. Enum values are strings such as Approved and Declined.

HTTP DTOs are separate from service results and persistence entities. The history response includes events, revision terms, and past decisions.

## Service validation

```powershell
dotnet test BlueHarvest.PricingExceptions.slnx
```

The tests exercise InMemory workflows: approval followed by revision, decline, preserved history and terms, retries, key conflicts, unauthorized actions, self-review, stale versions, invalid input, duplicate applications, missing requests, and competing decisions across service scopes. They do not validate relational guarantees.

With the Development API running, run the HTTP smoke checks from this directory:

```powershell
./tests/Invoke-ApiSmokeTests.ps1
```

This creates a fresh demo request and exercises the controller workflow, validation, role checks, error codes, and Swagger. It uses a new application ID and idempotency keys on every run.

## Run

From this directory:

```powershell
dotnet build BlueHarvest.PricingExceptions.slnx
dotnet run --project src/PricingExceptions.API --launch-profile http
```

Open http://localhost:5267/swagger in Development. Visual Studio launch profiles open Swagger automatically; terminal launches require opening the URL manually.

## Design references

See [Assumptions and scope decisions](docs/Architecture/Assumptions.md) for implemented business policies, InMemory and demo-authentication trade-offs, and decisions requiring stakeholder confirmation.

Design documents are organized under docs/EntityClassDiagram, docs/SequenceDiagrams, and docs/Architecture.
