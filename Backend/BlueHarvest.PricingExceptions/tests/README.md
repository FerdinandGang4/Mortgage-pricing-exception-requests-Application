# Integration test guide

Run commands from `Backend/BlueHarvest.PricingExceptions` with the .NET 10 SDK installed.

## Test projects and prerequisites

| Project | Purpose | Storage | Docker required |
| --- | --- | --- | --- |
| PricingExceptions.UnitTests | Isolated service rules and development seeding | EF Core InMemory | No |
| PricingExceptions.IntegrationTests | HTTP endpoints, authentication simulation, workflow, and HTTP responses | EF Core InMemory | No |
| PricingExceptions.PostgreSqlTests | Migrations, relational constraints, atomic saves, persistence, and concurrency | PostgreSQL 17 in a disposable container | Yes |

Start Docker Desktop with Linux containers (or a local Docker Engine) before running the complete solution. The Docker CLI must be on PATH. The first PostgreSQL test run may pull `postgres:17-alpine` from Docker Hub. No paid service or private infrastructure is required.

The PostgreSQL suite starts a uniquely named container on a randomly assigned loopback port, creates a fresh database for every test, applies the application's checked-in migrations, and seeds development users. Cleanup drops those test databases and removes only its own container and anonymous volume. It never uses the application Compose database, credentials, named volume, or fixed port. Application containers do not need to be running. If Docker is unavailable, database tests fail with diagnostics rather than silently skipping.

## Commands

Restore, build, and run everything:

```powershell
dotnet restore
dotnet build
dotnet test --logger "console;verbosity=normal"
```

Only the HTTP integration tests (Docker is not required):

```powershell
dotnet test tests/PricingExceptions.IntegrationTests/PricingExceptions.IntegrationTests.csproj --logger "console;verbosity=normal"
```

Only the PostgreSQL integration tests:

```powershell
dotnet test tests/PricingExceptions.PostgreSqlTests/PricingExceptions.PostgreSqlTests.csproj --logger "console;verbosity=normal"
```

Only unit tests:

```powershell
dotnet test tests/PricingExceptions.UnitTests/PricingExceptions.UnitTests.csproj
```

To save machine-readable results, append `--logger trx --results-directory TestResults` to the desired test command.

## Workflow decisions

Creating a request also submits revision 1 as Pending. Submitting a revision also saves and submits that revision as Pending. Neither has a separate draft or submission endpoint. The submission sections verify visibility and state from those existing operations.

Any known internal user can currently list and read all requests. Assigned-reviewer tests establish that the assignee can see the request and only that assignee may decide it; they do not claim that other reviewers cannot read it.

Section order supports a business walkthrough, not xUnit execution order. Every HTTP case owns a fresh host and InMemory database. Small helpers build valid requests and assert successful setup operations; individual tests retain the meaningful business and HTTP assertions.

## HTTP tests by business workflow

### 1. Request Creation

- `ManagerCanCreatePricingExceptionRequest`

### 2. Request Submission

- `CreatingRequestSubmitsItForAssignedReviewer`

### 3. Request Approval

- `AssignedReviewerCanApprovePendingRequest`

### 4. Request Decline

- `AssignedReviewerCanDeclineRequestWithReason`

### 5. Approved Discount Retrieval

- `MortgageProcessCanRetrieveApprovedDiscount`
- `UnapprovedRequestDoesNotExposeDiscount`

### 6. Request Revision

- `ManagerCanReviseRequestAndPreserveOriginalRevision`

### 7. Revision Submission and Review

- `SubmittingRevisionRequiresNewApproval`
- `AssignedReviewerCanReviewRevisedRequest`
- `ReviewerListShowsLatestRevisionOfEveryRequest`

### 8. Request History

- `RequestHistoryPreservesRevisionsDecisionsAndBusinessActions`

### 9. Manager and Reviewer Authorization

- `ManagersCanAssignRequestsToDifferentReviewers` ? all 30 manager/reviewer/decision combinations
- `OnlyAssignedReviewerCanDecideRequest` ? all 30 combinations, checking every other reviewer
- `OnlyManagersCanCreatePricingExceptionRequests`
- `ManagerCannotApproveOrDeclineOwnRequest`
- `OtherManagerCannotReviseRequest`
- `ManagerCanSelectFromFiveReviewers`
- `ReviewerCannotAccessManagerReviewerDirectory`

### 10. Request Validation

- `InvalidDiscountIsRejected`
- `MissingBusinessReasonIsRejected`
- `InvalidReviewerAssignmentIsRejected`
- `InvalidMortgageApplicationIdIsRejected`
- `MissingIdempotencyKeyIsRejected`
- `MissingOrInvalidDecisionIsRejected`
- `ReviewOfOutdatedRevisionIsRejected`

### 11. Duplicate Prevention and Idempotency

- `DuplicatePricingExceptionRequestIsRejected`
- `RetryingRequestCreationReturnsOriginalResponse`
- `RetryingReviewDoesNotCreateDuplicateDecision`
- `RetryingRevisionDoesNotCreateDuplicateRevision`
- `ReusingIdempotencyKeyWithDifferentDataIsRejected`
- `DecidedRevisionCannotReceiveAnotherDecision`

### 12. API Security and HTTP Responses

- `MissingInvalidOrUnknownIdentityReturns401`
- `UnknownRequestReturns404`
- `CurrentUserReflectsAuthenticatedManager`
- `RequestBodyCannotOverrideAuthenticatedManagerOrReviewerName`
- `DecisionBodyCannotOverrideAuthenticatedReviewer`
- `DevelopmentSwaggerDescribesEndpointsAndIdentityHeaders`
- `ProductionRejectsDemoIdentity`
- `ProductionDoesNotExposeDevelopmentDocumentation`

## PostgreSQL tests

### 1. Persistence and history across independent database contexts

- `RequestHistoryAndRetryResultsSurviveNewDatabaseContexts`

### 2. Relational constraints and duplicate prevention

- `DatabaseRejectsDuplicateRevisionNumber`
- `DatabaseAllowsOnlyOneDecisionPerRevision`
- `DatabaseRejectsRevisionWithUnknownManager`
- `DuplicateApplicationIsRejectedAcrossIndependentContexts`

### 3. Transactions and consistency

- `FailedSaveRollsBackRequestDecisionHistoryAndRetryRecord`
- `DatabaseRejectsUpdateFromAnOutdatedContext`

### 4. Concurrent workflow operations within the supported single API instance

- `ConcurrentCreationRetriesPersistOneRequest`
- `ConcurrentApprovalAndDeclinePersistOneDecision`
- `ConcurrentReviewAndRevisionPersistOneChange`

## Validation results and scope

| Suite | Before this refactor | After this refactor |
| --- | ---: | ---: |
| Unit | 56 | 56 |
| HTTP integration | 45 | 118 |
| PostgreSQL integration | 0 | 10 |
| Total | 101 | 184 |

Validation on 6 November 2026: `dotnet restore` succeeded; `dotnet build` succeeded with zero warnings and errors; `dotnet test --logger "console;verbosity=normal"` passed all 184 cases (56 unit, 118 HTTP, 10 PostgreSQL), with zero failed and zero skipped. The initial PostgreSQL timestamp comparison failed on sub-microsecond precision and was corrected to match the provider's storage precision before the successful full rerun.

Files changed for this refactor:

- `PricingExceptions.IntegrationTests/HttpWorkflowTests.cs`: twelve workflow sections, focused scenarios, independent input cases, two separate 30-case assignment/authorization matrices, and small HTTP setup helpers.
- `PricingExceptions.PostgreSqlTests/PostgreSqlFixture.cs`: disposable Docker PostgreSQL lifecycle and a separate database for each case.
- `PricingExceptions.PostgreSqlTests/PostgreSqlWorkflowTests.cs`: ten real database tests covering persistence, constraints, rollback, and concurrency.
- `PricingExceptions.PostgreSqlTests/PricingExceptions.PostgreSqlTests.csproj` and the solution: register the separate database test project using the existing .NET/xUnit versions.
- This guide, the backend README, and the unit-test README: commands, test catalog, Docker prerequisite, and coverage boundaries. No production business logic or application Docker configuration changed.

The original 30 positive assignment cases remain. Authorization against the four other reviewers now has its own 30-case matrix. Combined workflow tests were split into focused scenarios; validation and retries have independent parameterized cases.

The database suite confirms real PostgreSQL primary-key/unique and foreign-key enforcement, atomic SaveChanges rollback on a foreign-key failure, concurrency-token rejection across contexts, persistence of history and retry responses, and service conflict handling within one supported API instance. Timestamp comparisons account for PostgreSQL microsecond precision; saved JSON retry responses must still match the exact original result.

One request per application and scoped idempotency-key uniqueness remain service checks without database unique indexes. The tests explicitly distinguish those service rules from database-enforced revision and decision uniqueness. The concurrent service tests share the existing singleton lock; they do not prove distributed safety across API instances. The separate stale-context test exercises the actual database concurrency token.

Remaining gaps: distributed writers, database outage/recovery, backup/restore, exhaustive migration upgrades from historical schemas, and full HTTP behavior on PostgreSQL are not covered here. The PostgreSQL tests use the production services and DbContext directly. The assignment's external mortgage-application existence check remains an explicit implementation assumption, not a tested integration. Production identity is outside the demo's scope.

The existing [Docker persistence smoke test](Test-DockerPersistence.ps1) additionally checks persistence across Compose recreation against the running application. It changes that application's demo data and restarts its containers; it is separate from these isolated tests and is not called by dotnet test.
