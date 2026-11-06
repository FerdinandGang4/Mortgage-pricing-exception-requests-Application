# Pricing exception service tests

`PricingExceptionServiceTests.cs` follows the business workflow in the Blue Harvest assignment. Section numbers describe the interview walkthrough, not test execution order. Each xUnit case gets its own service provider, uniquely named EF Core InMemory database, users, lock, and controlled clock.

## Workflow walkthrough

1. **Request Creation:** a relationship manager creates a request containing the external application ID, discount in basis points, reason, and assigned reviewer.
2. **Request Submission:** `CreateAsync()` also submits the first revision immediately as Pending. The reviewer can retrieve it through `ListAsync()`. There is no draft state or separate submission command.
3. **Request Approval:** the assigned reviewer approves; the result and stored decision retain the reviewer, reason, and exact UTC timestamp.
4. **Request Decline:** the assigned reviewer declines; the status and saved decision retain the reason, reviewer, and timestamp.
5. **Approved Discount:** MortgageProcess receives the current approved discount; Pending and Declined requests expose no discount.
6. **Request Revision:** the original manager submits new discount and reason values, increasing the revision and version. Revising an approved request invalidates its effective approval.
7. **Revision Submission and Review:** `SubmitRevisionAsync()` saves and submits the revision in one operation. The latest revision is Pending and visible to the reviewer, who can approve or decline it. Only its approved discount is effective; an older approval is never a fallback.
8. **Request History:** previous revisions, approvals, declines, names, actors, and timestamps remain available. Submitted revisions cannot be changed, and protected history and retry records cannot be deleted through the DbContext.
9. **Validation and Authorization:** invalid inputs and assignments, unauthorized actors, unknown requests, self-review, and stale review/revision commands are rejected.
10. **Duplicate Prevention and Idempotency:** duplicate applications are rejected. Identical create, review, and revision retries return their saved results without duplicate records. Changed input under the same key is rejected.
11. **Concurrency and Reliability:** concurrent creation retries, opposing decisions, review versus revision, and cancelled reads preserve consistency. Bounded waits prevent a lock regression from hanging the suite indefinitely.

Arrange, Act, and Assert comments identify each test's purpose. Exception assertions combine Act and Assert because the expected result is a rejected operation. History tests exercise a sequence of operations to verify the retained record.

## Run the tests

From `Backend/BlueHarvest.PricingExceptions`:

```powershell
dotnet restore
dotnet build
dotnet test --logger "console;verbosity=normal"
```

## Coverage boundaries

All eleven workflow sections have dedicated service tests. These tests deliberately use InMemory and do not prove PostgreSQL transactions, migrations, constraints, restart persistence, distributed concurrency, or rollback on storage failure. The separate HTTP integration test project also selects InMemory; it validates API behavior rather than PostgreSQL storage semantics.

The assignment refers to an existing mortgage application. This implementation accepts an external, non-empty application GUID; verifying the application's existence with the mortgage system is not implemented or tested. Production identity, frontend user switching, and Docker operation are outside this service test class. No new submission operation or production behavior is introduced by these tests.
