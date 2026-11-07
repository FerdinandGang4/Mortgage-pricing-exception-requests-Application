# Blue Harvest Mortgage Pricing Exceptions

An internal application for requesting, reviewing and tracking mortgage interest-rate discounts, developed for the [Blue Harvest engineering assignment](<../Documentation/Blue Harvest Agentic SDLC Engineering Assignment.pdf>).

## 1. Business Problem

Relationship managers need to request discounts for mortgage applications. An authorized reviewer must approve or decline the terms before the mortgage process can use a discount. The application records the decision, reviewer and time, exposes the current approved discount, preserves history and prevents duplicate records when callers retry an operation.

## 2. Implementation Assumptions and Scope

### 2.1 Key Assumptions

- Mortgage applications belong to an external system. The API accepts a non-empty application GUID but does not verify existence or eligibility.
- One exception request is allowed per mortgage application; later proposals use revisions.
- Discounts are whole numbers from 1 to 500 basis points. A 50-basis-point discount reduces the rate by 0.50 percentage points.
- Creation and revision require a reason of up to 2,000 characters. A review reason is optional with the same limit.
- Only the original manager may revise a request. Only the assigned reviewer may decide it; self-review is forbidden.
- Known internal users may read requests and history. Only the mortgage-process role may retrieve an approved discount.

These policies are implementation choices where the brief leaves details open.

### 2.2 Scope and Limitations

The service manages exceptions; it does not calculate standard rates, originate mortgages, perform underwriting, send notifications or apply discounts in another system. Approval expiry and downstream use are not tracked.

Development uses seeded identities selected through the UI and passed in `X-User-Id`. This simulates identity, rather than providing production authentication. The API derives roles from stored users, rejects demo headers outside Development and has no production identity provider configured.

The implementation assumes one API instance. Its shared lock does not coordinate multiple instances, and some duplicate checks remain service-level checks rather than unique database constraints.

## 3. Architecture and Key Design Decisions

The React frontend is served through Nginx and calls an ASP.NET Core API. Application services enforce workflow rules, and EF Core stores records in PostgreSQL. Docker Compose runs these three components with a persistent database volume.

- **Revisions preserve approval integrity.** Managers can change submitted terms by creating a new revision. It returns the request to Pending and makes the previous approval historical until the new terms are approved. Earlier terms and decisions remain available in history. This resolves the conflicting stakeholder requirements in the brief.
- **Version checks prevent stale updates.** Decisions and revisions must match the version and revision the user viewed; a mismatch returns `409 Conflict`.
- **Idempotency makes retries safe.** Every POST requires an `Idempotency-Key`. The same actor repeating the same operation with the same key and input receives the original result; different input under that key returns `409`.
- **Related changes are saved together.** PostgreSQL saves workflow changes, history and the completed retry record together. Actor IDs, name snapshots and UTC timestamps retain attribution.

## 4. Local Setup

Start Docker Desktop with Linux containers. From the repository root, which contains `compose.yaml`, run:

```powershell
docker compose up -d --build
docker compose ps
```

With the default port settings:

- Frontend: http://localhost:5173
- Swagger: http://localhost:5267/swagger

Select a seeded demo user in the frontend to create, review and inspect requests. The local setup requires no private infrastructure or paid services. Stop the application with `docker compose stop`; the database volume retains its data.

## 5. API Endpoints

All endpoints require a recognized user identity. During development, identity is simulated using seeded users and the `X-User-Id` header. All POST requests also require `Idempotency-Key`.

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

In pricing exception routes, `{id}` is the request ID. In the approved-discount route, it is the mortgage application ID. Creation and revision return a `Location` header pointing to the request.

Errors use Problem Details: `400` for invalid input, `401` for missing or invalid identity, `403` for forbidden actions, `404` for missing records or no current approved discount, and `409` for conflicting changes or retries. Swagger provides request schemas and interactive API examples.

## 6. Example Business Workflow

Use the frontend or Swagger to exercise the following scenario:

1. Select manager James Wilson. Create a request for a new mortgage application GUID, request 50 basis points, enter a reason and assign reviewer David Miller. Creation submits revision 1 as Pending.
2. Switch to David Miller and approve the current revision. The decision records the reviewer and UTC time. Declining instead leaves no effective discount.
3. Switch to the mortgage-process user and look up the application ID. An approved current revision returns the 50-basis-point discount.
4. Switch back to James and submit revised terms of 75 basis points. Revision 2 becomes Pending; discount lookup returns `404` until it is approved.
5. Inspect the history. Both sets of terms and the earlier decision remain visible, but the first approval does not authorize the revised terms.

For direct API calls, read the latest request before a decision or revision and send its version and revision values. Use a new idempotency key for each new operation; reuse the key and input only when retrying that operation.

## 7. Testing and Validation

### 7.1 Backend

Install the .NET 10 SDK and keep Docker running for the isolated PostgreSQL tests. From the repository root:

```powershell
cd Backend/BlueHarvest.PricingExceptions
dotnet build
dotnet test
```

The suite includes unit, HTTP integration and PostgreSQL workflow tests covering decisions, revisions, history, retries, access rules and concurrency.

### 7.2 Frontend

Install Node.js 24. From the repository root, open a separate terminal and run:

```powershell
cd FrontEnd
npm.cmd ci
npm.cmd run build
npm.cmd run lint
npm.cmd run test:e2e
```

These commands use Windows syntax; use `npm` instead of `npm.cmd` on other systems. Browser tests start isolated API and frontend servers on ports 5270 and 5174. They require the .NET SDK and Microsoft Edge on Windows, or Playwright Chromium on other systems (`npx playwright install chromium`). Leave `E2E_BASE_URL` unset for the isolated suite.

See the testing documents below for execution details and recorded evidence. Tests were not rerun for this README-only update.

## 8. Before Production

Integrate trusted sign-in and mortgage application validation, confirm the business policies, and strengthen database constraints and concurrency for multiple API instances. Establish backup recovery, privacy controls, monitoring and operational ownership.

## 9. Supporting Documentation

| Document | Location |
| --- | --- |
| Architecture and detailed design | [Word design document](../Documentation/Architecture/Blue_Harvest_Mortgage_Pricing_Exception_Design.docx) |
| Business assumptions and scope | [Word assumptions document](../Documentation/Assumptions.docx) |
| Sequence diagrams and explanations | [Word sequence document](../Documentation/BlueHarvest_Current_Sequence_Diagrams_Final.docx) |
| Entity relationships and data model | [Word entity document](../Documentation/BlueHarvest_Current_Entity_Relationships_Final.docx) |
| Local execution | [Word Docker guide](../Documentation/BlueHarvest_Docker_Clear_Instructions.docx) |
| Test execution and validation | [Backend test guide](../Documentation/Testing/README.md), [frontend test guide](../Documentation/Frontend/TESTING.md), [validation record](../Documentation/Architecture/ValidationResults.md) |
| AI-assisted development | [Development log](../Documentation/Architecture/AiDevelopmentLog.md) |
| Assignment presentation | [PowerPoint](../Documentation/BlueHarvest_Assignment_Presentation.pptx) |

AI-assisted tools supported development, tests and documentation; they do not make mortgage approval decisions.

## 10. Development Time

Actual development time: **25 hours**, completed over **5 days at 5 hours per day**.
