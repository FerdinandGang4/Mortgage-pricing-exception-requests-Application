# Submission checklist

## Implemented and documented

- [x] Controller API for create, retrieve, revise, review, history and approved-discount lookup.
- [x] Separate entities, enums, DTOs, services, mappers and EF Core DbContext.
- [x] Immutable revision workflow, reviewer and UTC decision timestamps, ordered history.
- [x] Demo identities, role checks and self-review prevention.
- [x] Version checks and safe retries within one process/store lifetime.
- [x] Service tests, automated HTTP integration tests and live smoke script.
- [x] Swagger and realistic approval/decline/revision walkthrough.
- [x] Business assumptions in Markdown and Word.
- [x] Class diagram, sequence diagrams and proposed solution design.
- [x] Explicit InMemory, authentication and external-integration trade-offs.
- [x] AI contribution and verification notes.
- [x] CI workflow definition.
- [x] Fresh local checkout restored, built in Release with 0 warnings/errors, and passed all 27 tests.
- [x] Fresh-checkout API started and passed the live HTTP smoke checks on port 5268.

## Developer-supplied details and final handoff

- [ ] Enter actual development time in Reports/SubmissionNotes.md and its maintained source under Backend/BlueHarvest.PricingExceptions/docs/Architecture.
- [ ] Review and commit the intended changes. No commit is created by preparing this directory.
- [ ] Confirm the shared repository URL and that the reviewer can access it.
- [ ] Check the remote CI run after pushing; a workflow definition alone is not execution evidence.
- [ ] Perform the demo using the intended reviewer environment.
- [ ] Confirm that the exercise's InMemory and demo-identity trade-offs are acceptable.

## Scope statement

No frontend is included; it is an assignment bonus. SQLite from the proposed design is not implemented because this version retains the earlier InMemory choice. Do not present this implementation as durable across restarts or production-ready.

The validation report records the actual local checks performed. Actual developer time and repository access cannot be inferred from automated tests.
