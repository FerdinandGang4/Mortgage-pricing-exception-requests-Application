# Current controller and service methods

Code reference snapshot: 7 November 2026.

This is a naming and organization refactor. HTTP routes, DTOs, schema, authorization, retry scopes and workflow behavior are unchanged.

`ApprovedDiscountService.GetAsync` is intentionally unchanged. It is distinct from the renamed pricing-exception request read method.

## PricingExceptionsController

- `CreatePricingExceptionRequestAsync` (public)
- `GetPricingExceptionRequestsAsync` (public)
- `GetPricingExceptionRequestByIdAsync` (public)
- `RecordPricingExceptionDecisionAsync` (public)
- `SubmitPricingExceptionRevisionAsync` (public)
- `GetPricingExceptionHistoryAsync` (public)

## UsersController

- `GetCurrentUserProfileAsync` (public)
- `GetReviewersAsync` (public)

## ApprovedDiscountsController

- `GetApprovedDiscountForMortgageApplicationAsync` (public)

## PricingExceptionService

- `CreatePricingExceptionRequestAsync` (public)
- `SubmitRequestRevisionAsync` (public)
- `ReviewPricingExceptionRequestAsync` (public)
- `ListPricingExceptionRequestsAsync` (public)
- `GetPricingExceptionRequestAsync` (public)
- `GetRequestHistoryAsync` (public)
- `GetRequestRevisionsAsync` (public)
- `GetReviewDecisionsAsync` (public)
- `GetRequestHistorySnapshotAsync` (public)
- `ExecuteAuthorizedRequestReadAsync` (private)
- `FindRequestOrThrowAsync` (private)
- `ExecuteIdempotentWriteAsync` (private)
- `ValidateDiscountTerms` (private)
- `ValidateRequestVersionAndRevision` (private)
- `CreateRequestRevision` (private)
- `AppendRequestHistoryEntry` (private)
- `MapToPricingExceptionResult` (private)

## UserService

- `GetReviewersAsync` (public)
- `GetUserOrThrowAsync` (public)
- `GetUserWithRequiredRoleAsync` (public)

## ApprovedDiscountService

- `GetAsync` (public)

## Entity model

The six persisted entities remain User, PricingExceptionRequest, RequestRevision, ReviewDecision, HistoryEntry and IdempotencyRecord. Method renaming does not add or rename any entity, field, table, key or relationship.

The ER diagrams show persisted data. Controller and service methods belong in this method reference and the sequence diagrams, rather than inside entity boxes.
