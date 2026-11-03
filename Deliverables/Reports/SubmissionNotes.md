# Submission notes and requirements coverage

## Deliverable

A .NET 10 controller-based HTTP API with EF Core InMemory, Swagger, seeded Development identities, automated tests, and a live HTTP smoke-test script. It runs without private infrastructure or paid services.

The implementation deliberately retains the user's selected InMemory provider. The supplied design proposes SQLite as a durability improvement; that proposal is not implemented in this version. Its transactional guarantees must not be inferred from the process-wide workflow lock.

## Requirements and evidence

| Assignment need | Implementation | Evidence |
| --- | --- | --- |
| Create a discount exception for an existing application reference | Create endpoint; non-empty external GUID, basis points and reason | Service input tests and HTTP create/validation tests; existence remains an explicit assumption |
| Authorized approval or decline, with actor and time | Role checks, independent reviewer, revision/version checks, UTC decision time | Approval, decline, wrong-role and self-review tests |
| Supply the approved discount to the mortgage process | Current approved revision lookup | Approve/revise/reapprove HTTP workflow; no discount after decline or pending revision |
| Inspect current status and past decisions | Request endpoint and combined history containing events, revisions and decisions | Service history assertions and HTTP snapshot checks |
| Prevent duplicate requests after timeout | Actor/operation/target-scoped idempotency records and payload hashes | Exact HTTP replay including status and Location; simultaneous service retries; conflicting payload tests |
| Resolve conflicting requirements about changed terms | Immutable revisions, re-review and withdrawal of old effective approval | Revision workflow tests and historical record mutation/deletion guards |
| Straightforward local execution | .NET SDK commands, seeded identities and Swagger | README; automated TestServer startup and Swagger checks |
| Explain scope, production trade-offs and assumptions | Assumptions.md and Word export | Documentation/Assumptions.docx and architecture notes |
| Demonstrate validation | Service tests, automated HTTP tests and smoke script | Run dotnet test; CI workflow added, remote CI execution not claimed |
| Explain AI assistance and time spent | AiDevelopmentLog.md | Contribution and verification record; actual time must be supplied by the developer |

## Architecture and implementation decisions

Controllers accept HTTP DTOs and obtain actor identity from authenticated claims. Services enforce the workflow and use the scoped DbContext. Domain/persistence models and enums remain separate from HTTP contracts. This is one deployable project with logical folder boundaries.

For the exercise, services access EF Core directly instead of introducing repository abstractions. That is a maintainability trade-off against the stricter dependency separation proposed by the design. Split repositories or assemblies only when needed.

The request version is a concurrency token. The process-wide lock coordinates workflow reads and writes, including a combined history snapshot. Historical records reject modification or deletion through SaveChanges. These controls do not create relational transactions or durable audit storage.

No additional mortgage entity is owned here. Example mortgage application IDs can be any non-empty GUID, including aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa. Real application existence and eligibility checks belong at the external integration boundary and remain a documented assumption.

## Fresh-checkout verification

From the repository root:

```powershell
dotnet restore Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx
dotnet build Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx --no-restore
dotnet test Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx --no-build --no-restore
dotnet run --project Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API --launch-profile http
```

Open http://localhost:5267/swagger and follow the backend README. In another terminal run the smoke script. Tests create isolated stores and need no running API; the smoke script needs the API and writes fresh demo data.

Stop and restart to reset all demo workflow data. Seeded users are recreated. Do not use real customer information: the demo identity header allows impersonation of known demo users.

## Before production

Use a relational database with migrations, transactions, unique constraints, backups and recovery. Add trusted authentication, external application validation, privacy and retention controls, monitoring, operational ownership, and an agreed approval expiry/consumption contract.

The UI and AI-agent bonus features are optional and not implemented as product features. AI-assisted development and validation are documented separately.

## Time spent

Actual developer time: **not recorded yet**. This cannot be inferred reliably from chat or tool durations; the developer must enter their actual total before submission.
