# Reviewer demo walkthrough

Start the Development API using [QuickStart.md](QuickStart.md), then open Swagger. Use a fresh application ID or restart the API before reusing the application IDs below.

## 1. Create an exception

POST /api/v1/pricing-exceptions

- X-User-Id: 11111111-1111-1111-1111-111111111111
- Idempotency-Key: demo-create-001

```json
{
  "mortgageApplicationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "discountBps": 50,
  "reason": "Competitive mortgage rate offer"
}
```

Expected: 201, Pending, revisionNumber 1, version 1, and a Location header. Copy the returned id as REQUEST_ID.

Repeat the same create with the same key and body. The original response is replayed without another request or history event. Change discountBps to 60 with the same key: expect 409. Return to the original payload for any further retries.

## 2. Inspect the submitted terms

GET /api/v1/pricing-exceptions/REQUEST_ID using the reviewer ID:

22222222-2222-2222-2222-222222222222

Expected: 200 with the exact current terms and version.

## 3. Approve the exact revision

POST /api/v1/pricing-exceptions/REQUEST_ID/decisions

- X-User-Id: 22222222-2222-2222-2222-222222222222
- Idempotency-Key: demo-approve-001

```json
{
  "outcome": "Approved",
  "expectedVersion": 1,
  "reviewedRevisionNumber": 1,
  "reason": "Offer accepted"
}
```

Expected: 200, Approved, version 2, reviewer identity and UTC decision time.

## 4. Retrieve the approved discount

GET /api/v1/mortgage-applications/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/approved-discount

X-User-Id: 33333333-3333-3333-3333-333333333333

Expected: 200, discountBps 50 and revisionNumber 1.

## 5. Revise the proposal

POST /api/v1/pricing-exceptions/REQUEST_ID/revisions

- X-User-Id: 11111111-1111-1111-1111-111111111111
- Idempotency-Key: demo-revise-001

```json
{
  "discountBps": 60,
  "reason": "Updated competitive offer",
  "expectedVersion": 2,
  "expectedRevisionNumber": 1
}
```

Expected: 201, Pending, revisionNumber 2 and version 3.

Repeat the approved-discount lookup: expect 404. Explain that the old approval remains historical while the revised terms require another review.

## 6. Approve the new revision

POST /api/v1/pricing-exceptions/REQUEST_ID/decisions

Use the reviewer ID and Idempotency-Key demo-approve-002:

```json
{
  "outcome": "Approved",
  "expectedVersion": 3,
  "reviewedRevisionNumber": 2,
  "reason": "Revised offer accepted"
}
```

Expected: version 4 and Approved. The mortgage-process lookup now returns 60 basis points for revision 2.

## 7. Inspect history

GET /api/v1/pricing-exceptions/REQUEST_ID/history using a known internal user.

Expected: four ordered events, two immutable revision snapshots and two decisions. Show that revision 1 still contains 50 basis points.

## 8. Demonstrate decline

Create another request using application ID bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb, the manager ID and Idempotency-Key demo-create-decline. Copy its returned request ID.

Review it as the reviewer with Idempotency-Key demo-decline-001:

```json
{
  "outcome": "Declined",
  "expectedVersion": 1,
  "reviewedRevisionNumber": 1,
  "reason": "Discount not justified"
}
```

Expected: Declined. The approved-discount lookup for application bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb returns 404.

## 9. Explain failure handling and scope

- Missing or invalid demo identity: 401.
- Wrong role: 403.
- Invalid discount/reason or missing operation key: 400.
- Stale version/revision or conflicting retry payload: 409.
- Unknown request or no current approval: 404.

The original manager cannot review their own request. Request reasons are preserved as submitted terms; the service does not calculate rates or validate the external mortgage application's existence.

Explain the InMemory reset behavior and demo authentication limitations. The intended production changes are in Reports/SubmissionNotes.md.
