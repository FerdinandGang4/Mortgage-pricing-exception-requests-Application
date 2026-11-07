# Blue Harvest mortgage pricing exceptions — README documentation

## Business problem

A relationship manager may need to offer a mortgage customer a discount below the standard interest rate. An independent reviewer must approve the requested terms before the mortgage process can use the discount.

This application keeps that process clear and traceable. A manager creates a request, chooses a reviewer and explains the discount. The reviewer approves or declines it. The mortgage process can retrieve the currently approved discount, and users can see the history of terms and decisions.

Changing the terms must not silently change an existing approval. A change creates a new revision that needs a fresh decision. Earlier revisions and decisions stay in the history.

## Implementation assumptions

These are choices made for this assignment, rather than confirmed banking policies:

- A mortgage application belongs to another system. This application stores its ID, but does not check whether the mortgage application exists or is eligible.
- Each mortgage application has one pricing exception request. Later proposals use revisions of that request, including after a decline.
- Discounts are whole numbers between 1 and 500 basis points. One basis point is 0.01 percentage points; 50 basis points is a reduction of 0.50 percentage points.
- Creation and revision require a reason of up to 2,000 characters. A review reason is optional and has the same length limit.
- Only the original manager can revise a request. Only the assigned reviewer can decide it, and the creator cannot review their own request.
- Any known internal user can read requests and history. Only a user with the `MortgageProcess` role can retrieve an effective approved discount.
- Approval applies to the exact revision reviewed. A new revision returns the request to `Pending` and makes the earlier approval unavailable for discount lookup.
- Approvals have no automatic expiry. The application does not track whether another system has already used an approved discount.

See [Assumptions and scope decisions](Architecture/Assumptions.md) for the reasons, limits and decisions that need stakeholder confirmation.

## Deliberate scope choices and simplifications

The application handles pricing exception requests. It does not calculate the standard mortgage rate, create mortgages, assess affordability, perform underwriting or apply a discount in an external mortgage system. Notifications, delegated reviews and approval limits are outside this assignment implementation.

For the local demonstration, a user picker selects a seeded identity. The API accepts that identity through the `X-User-Id` header in Development and reads the role from the stored user. This lets a reviewer try the different roles without setting up an identity provider. It is not production sign-in. The demo header is rejected outside Development, and production authentication still needs to be integrated.

The solution runs as one API instance. A shared lock coordinates its workflow operations within that process. It does not coordinate multiple API instances. Some duplicate checks are enforced by the service rather than unique database indexes, so distributed concurrency needs further work before scaling out.

PostgreSQL stores the application data. Docker keeps that data in a named volume across ordinary restarts. A volume is not a backup. Isolated tests can use a temporary InMemory database; separate PostgreSQL tests check relational behavior.

## Architecture and key design decisions

The main request path is:

```text
React frontend → Nginx API proxy → ASP.NET Core controllers
              → application services → EF Core → PostgreSQL
```

Controllers handle HTTP requests, identity claims, request DTOs and response mapping. `PricingExceptionService` manages requests, revisions, decisions and history. `UserService` finds users and checks their stored roles. `ApprovedDiscountService` retrieves the discount for an approved current revision. Services use `PricingExceptionsDbContext` directly; no separate repository service is introduced.

The main design decisions are:

- **Keep submitted terms:** each revision has its own stored terms. Decisions and history retain the earlier record rather than replacing it.
- **Reject stale changes:** revision and decision requests include the version and revision the user viewed. A mismatch returns `409 Conflict` so the user can reload the latest state.
- **Make retries safe:** every POST requires an `Idempotency-Key`. An identical retry by the same actor for the same operation returns the original result. Reusing that key with different input returns `409`. Use a new key for a new operation; keep the same key and input when retrying a lost response.
- **Save related changes together:** workflow changes, history and the completed retry record are saved together in PostgreSQL.
- **Preserve attribution:** records keep actor IDs, relevant name snapshots and UTC timestamps. Editing a user profile does not rewrite earlier attribution.
- **Keep HTTP contracts separate:** DTOs and mappers define the API input and output. Renaming a C# method does not change its HTTP route.

The persisted entities are `User`, `PricingExceptionRequest`, `RequestRevision`, `ReviewDecision`, `HistoryEntry` and `IdempotencyRecord`. There is no local mortgage application entity. See the entity and sequence documents below for diagrams and database constraint details.

## API endpoints

All endpoints require an authenticated user. The Development examples below use `X-User-Id`. All POST endpoints also require `Idempotency-Key`.

| Method | Route | Purpose | Access | Success |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/users/me` | Read the current user's profile | Any known internal user | 200 |
| GET | `/api/v1/users/reviewers` | List reviewers available for assignment | Relationship manager | 200 |
| POST | `/api/v1/pricing-exceptions` | Create and submit the first revision | Relationship manager | 201 |
| GET | `/api/v1/pricing-exceptions` | List requests | Any known internal user | 200 |
| GET | `/api/v1/pricing-exceptions/{id}` | Read the current request | Any known internal user | 200 |
| POST | `/api/v1/pricing-exceptions/{id}/decisions` | Approve or decline the current revision | Assigned reviewer | 200 |
| POST | `/api/v1/pricing-exceptions/{id}/revisions` | Submit replacement terms | Original manager | 201 |
| GET | `/api/v1/pricing-exceptions/{id}/history` | Read events, revisions and decisions | Any known internal user | 200 |
| GET | `/api/v1/mortgage-applications/{id}/approved-discount` | Read the currently approved discount | Mortgage process | 200 |

In pricing exception routes, `{id}` is the **request ID**. In the approved-discount route, it is the **mortgage application ID**.

Creation and revision return a `Location` header pointing to the request's GET route. Errors use Problem Details: `400` for invalid input, `401` for missing or invalid identity, `403` for a forbidden action, `404` for a missing request or no current approved discount, and `409` for duplicate requests, stale updates or conflicting retries.

## Example workflow

Start the application from the repository root with `docker compose up -d --build`. Open the frontend at `http://localhost:5173` or Swagger at `http://localhost:5267/swagger`. These HTTP examples use the API at `http://localhost:5267` in Development. They create demo data.

### 1. Create and submit a request as the manager

The seeded manager is James Wilson. The selected reviewer is David Miller. Use a new mortgage application GUID for a new example; the application rejects a second request for the same application.

```http
POST /api/v1/pricing-exceptions
X-User-Id: 11111111-1111-1111-1111-111111111111
Idempotency-Key: example-create-001
Content-Type: application/json

{
  "mortgageApplicationId": "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee",
  "discountBps": 50,
  "reason": "Customer received a competing mortgage offer.",
  "reviewerId": "22222222-2222-2222-2222-222222222222"
}
```

The response is `201 Created`. Copy its `id` as `<request-id>`. The request starts with status `Pending`, revision number `1` and version `1`. Creation also submits the request; there is no separate draft or submit endpoint.

### 2. Review the current terms as the assigned reviewer

Read `GET /api/v1/pricing-exceptions/<request-id>` first. Send the version and revision from that response when recording the decision. For the new request above:

```http
POST /api/v1/pricing-exceptions/<request-id>/decisions
X-User-Id: 22222222-2222-2222-2222-222222222222
Idempotency-Key: example-review-001
Content-Type: application/json

{
  "outcome": "Approved",
  "expectedVersion": 1,
  "reviewedRevisionNumber": 1,
  "reason": "The competing offer supports the requested discount."
}
```

The response is `200 OK`. The status becomes `Approved`, the revision remains `1`, and the version becomes `2`. To decline instead, use `"outcome": "Declined"`. Each revision can receive only one decision.

### 3. Retrieve the approved discount as the mortgage process

Use the mortgage application ID from creation, not the request ID:

```http
GET /api/v1/mortgage-applications/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee/approved-discount
X-User-Id: 33333333-3333-3333-3333-333333333333
```

For the approved request, this returns `200 OK` with the 50-basis-point discount and its review details. If the current revision is Pending or Declined, the response is `404`.

### 4. Change the terms as the original manager

For the approved request from step 2, submit a new revision using version `2` and revision `1`:

```http
POST /api/v1/pricing-exceptions/<request-id>/revisions
X-User-Id: 11111111-1111-1111-1111-111111111111
Idempotency-Key: example-revise-001
Content-Type: application/json

{
  "discountBps": 75,
  "reason": "The customer provided an updated competing offer.",
  "expectedVersion": 2,
  "expectedRevisionNumber": 1
}
```

The response is `201 Created`. The request is now Pending at revision `2`, version `3`. The approved-discount lookup returns `404` until the reviewer approves revision `2`. A new decision must use the latest values and a new idempotency key.

### 5. Inspect the history

```http
GET /api/v1/pricing-exceptions/<request-id>/history
X-User-Id: 11111111-1111-1111-1111-111111111111
```

The response contains the creation, decision and revision events, both revisions, and the earlier decision. The first approval remains visible as history, but it does not approve the replacement terms.

## Supporting documents

| Document | Main file | Reading copy / source |
| --- | --- | --- |
| Entity relationship diagram and data dictionary | [Word](BlueHarvest_Current_Entity_Relationships_Final.docx) | [PDF](BlueHarvest_Current_Entity_Relationships.pdf) |
| Workflow sequence diagrams | [Word](BlueHarvest_Current_Sequence_Diagrams_Final.docx) | [PDF](BlueHarvest_Current_Sequence_Diagrams.pdf) |
| Assumptions and reasons | [Word](Assumptions.docx) | [Maintained Markdown](Architecture/Assumptions.md) |
| Assignment presentation | [Latest PowerPoint](BlueHarvest_Assignment_Presentation.pptx) | [PDF](BlueHarvest_Assignment_Presentation.pdf) |
| Run locally with Docker | [Step-by-step Word guide](BlueHarvest_Docker_Clear_Instructions.docx) | [Detailed Docker commands](../../../DOCKER.md) |
| Frontend design | [Word](BlueHarvest_Frontend_Design.docx) | [Markdown and editable diagrams](../../../FrontEnd/Design/Frontend_Design.md) |

The presentation preserves the original assignment deck, dated 8 November 2026 and presented by Ferdinand Dinga. A current-code appendix refreshed on 7 November 2026 documents controller actions, service methods, private helpers, entity relationships and workflow sequences. The validation slide reflects 184 backend tests and 20 browser tests.

The [solution design Word document](Architecture/Blue_Harvest_Mortgage_Pricing_Exception_Design.docx) retains the original proposal and adds a current-implementation appendix. That appendix supersedes the earlier SQLite and repository/unit-of-work proposal for implemented architecture and method names. The [current method reference](CurrentImplementation/MethodReference.md) lists public actions, service methods and private helpers directly from the code.

Editable diagram sources and rendering instructions remain in [CurrentImplementation](CurrentImplementation/README.md). The assumptions reflect PostgreSQL, persistent retry records, the React UI, and the nine development users. Supporting engineering records remain in [Architecture](Architecture).

The [original assignment brief](<Architecture/Blue Harvest Agentic SDLC Engineering Assignment.pdf>) is preserved as the source requirement document.
