# Assumptions and scope decisions

This document records the policies and exercise trade-offs used by the current implementation. These choices are not stakeholder-approved banking rules. The assignment requires creation, authorized review, approved-discount retrieval, history, retry safety, and a locally runnable solution; it leaves many implementation and business details open.

## Business assumptions

| Topic | Current decision | Reason and consequence |
| --- | --- | --- |
| Mortgage application ownership | The application belongs to an external system. This service stores only MortgageApplicationId, a non-empty GUID. | A local mortgage entity is unnecessary for exception management. Application existence and eligibility are not checked yet. |
| Request cardinality | One exception request per mortgage application during the lifetime of the database, including after decline. | Further proposals use revisions of the existing request. Another create with a different operation key returns 409. Confirm whether multiple independent requests should be allowed. |
| Discount | An integer from 1 to 500 basis points, inclusive. | Avoids fractional precision ambiguity. This range is a proposed policy, not an assignment requirement. One basis point equals 0.01 percentage points. |
| Supporting reason | Creation and revisions require a non-blank reason of at most 2,000 characters; stored reasons are trimmed. | Keeps submitted terms understandable. Confirm the required content and limit. |
| Review reason | Optional for both approval and decline, with a 2,000-character maximum. | The assignment requires reviewer and time, but does not require an explanation. Confirm whether declines must include a reason. |
| Revision policy | Only the original manager may revise. Revisions are allowed while Pending and after either decision, including when terms are unchanged. | Each submission creates a new numbered snapshot; the service never edits previous submitted terms through its workflow. |
| Approval integrity | A decision applies to the exact current revision and expected request version. Only one final decision is accepted per revision. | Resolves the conflict between allowing changed terms and preserving exactly what was reviewed. Corrections require another revision. |
| Effect of revision | Every new revision returns the request to Pending and makes any previous approval historical. | The previous discount is immediately unavailable through the approved-discount API until the new revision is approved. Confirm the business impact of post-approval revisions. |
| Self-review | The creator may not review their own request, even if their role later changes. | Provides independent review; this restriction is a proposed policy. |
| Read visibility | Any known internal user can retrieve requests and history, including their reasons. Only MortgageProcess can use the approved-discount endpoint. | Simplifies the demo. Confirm ownership, team, and privacy restrictions before wider use. |
| Roles | Each stored user has one role: RelationshipManager, Reviewer, or MortgageProcess. | Multiple roles, delegation, and approval limits are outside the current model. |
| Approval lifetime | No expiry, consumption, or external revocation is implemented. | An approval remains current until another revision is submitted. The service cannot undo a discount already consumed by another system. |

## API and workflow decisions

- Requests begin at revision 1, version 1, and Pending status. Each successful revision or decision increments the version and appends a history event. Times are UTC.
- Revision and review commands must identify the expected version and revision. Stale commands return 409 instead of replacing newer state.
- The approved-discount endpoint returns 200 only for an approved current revision. Pending, Declined, and missing requests return 404 from that endpoint; historical approvals are not returned as effective discounts.
- Create and revision operations return 201 and a Location header pointing to the request. Decisions and reads return 200. Errors use Problem Details.
- History records successful state transitions, actors, and timestamps. It includes the retained revisions and decisions in the HTTP response. Rejected attempts and reads do not create history events.
- All POST operations require a non-blank Idempotency-Key of at most 200 characters. Keys are scoped to the actor, operation, and target request where applicable.
- A successful operation stores its service result. A matching retry returns that original result, even if the request has subsequently changed. A different payload under the same scoped key returns 409. Current identity and role checks still apply before replay.
- Retry payload comparison uses serialized typed input before reason trimming. Changing reason whitespace can therefore conflict under the same key. Failed operations are not stored as completed retries.
- No explicit idempotency expiry is implemented. PostgreSQL persists retry records across ordinary container restarts and recreation while the database volume is retained. Deleting the volume or its records removes that protection.

## Local execution trade-offs

### Storage and concurrency

The application uses EF Core with PostgreSQL. Docker Compose supplies the database and a persistent named volume, and the API applies checked-in EF migrations during startup. Requests, users, decisions, history, and retry records survive ordinary restarts and Compose down/up while that volume is retained. Isolated automated tests explicitly select temporary InMemory stores.

A singleton lock coordinates workflow reads and writes within one API process. The current-request lookup, combined history snapshot, and approved-discount query use the same lock as mutations. State changes, history, and retry records are submitted in one SaveChanges call using a PostgreSQL transaction. The revision primary key and decision-per-revision unique index are database constraints; one request per application and actor/scope/key retry uniqueness currently remain service checks without unique database indexes. Reads are not wrapped in a cross-instance snapshot transaction.

This setup assumes a single API instance and that workflow access goes through the registered services. The lock does not coordinate distributed writers. The DbContext rejects changes or deletion of submitted revisions, decisions, history entries, and completed retry records. This is application-level protection, not database-level tamper protection; direct SQL can bypass it. A persistent volume is not a backup or recovery plan.

### Identity and demo data

Development-only authentication accepts X-User-Id for a known seeded user and derives identity and role claims from the stored record. Anyone with a demo ID can act as that user; this is identity simulation, not secure authentication. Actor IDs are not accepted in business request bodies.

The application seeds three managers, five reviewers, and one mortgage-process identity in Development. Stable IDs and idempotent seeding preserve existing request references and historical name snapshots. It does not seed mortgage application records; any non-empty application GUID is accepted as an external reference. The demo header is rejected outside Development, and no alternative production identity provider is configured. Swagger is exposed only in Development. The React picker is enabled for local development and explicitly opted into by the local Docker build; normal production builds disable it.

### Deliberate exclusions

The service does not calculate mortgage rates, originate applications, assess affordability, perform underwriting, send notifications, or manage a complete mortgage lifecycle. A React frontend implements the assignment's UI bonus. AI supports development and validation, not automated mortgage approval decisions.

## Decisions to confirm before production

1. Confirm discount limits, required reasons, one-request-per-application policy, read visibility, and who may revise.
2. Confirm whether approved terms can be revised, whether approvals expire, and how downstream consumption or revocation works.
3. Integrate application existence and eligibility checks with the mortgage system.
4. Replace demo authentication with a trusted identity provider and define multi-role and delegated permissions.
5. Strengthen the existing PostgreSQL design for concurrent writers across instances, coordinate production migration rollouts, and test backup/restore and failure recovery.
6. Define audit retention, privacy controls, retry-record expiry, and protection of stored response data.
7. Establish production deployment, monitoring, operational support, and any reliable downstream handoff needed.

## Evidence and limits

Previously recorded validation includes 69 .NET cases, 13 browser tests, a live HTTP smoke test, a three-request multi-reviewer browser flow against PostgreSQL, and persistence/reseeding checks. Isolated tests use InMemory; live checks exercise actual PostgreSQL. Passing these checks demonstrates the tested local behaviour, not production security or exhaustive multi-instance race coverage. Rerun tests against the intended submission commit.

References: the original assignment PDF under Documentation, the current diagram sources under ../CurrentImplementation, and the latest Word/PDF documents indexed in ../README.md. These assumptions describe the current code rather than earlier storage proposals.
