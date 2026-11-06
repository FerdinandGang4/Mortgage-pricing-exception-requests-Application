# Submission notes and requirement coverage

The current solution is an ASP.NET Core API, React frontend and PostgreSQL database, runnable locally with Docker Compose. The [documentation index](../README.md) is the single entry point for the current diagrams, assumptions, presentation and Docker Word guide.

| Assignment need | Current implementation / evidence |
| --- | --- |
| Create a pricing exception | Application GUID, integer discount and supporting reason; independent reviewer selection |
| Authorised review with actor/time | Assigned-reviewer and role checks, recorded decision, reason and UTC time |
| Current approved discount | Lookup joins only the current revision and its approval |
| Status and previous decisions | Immutable submitted revisions and decisions, plus ordered history |
| Safe retries | Actor/operation/key lookup, input hash and saved response; PostgreSQL retains records across normal restarts |
| Changed terms versus exact approval | New revision returns to Pending and withdraws the previous effective approval |
| Easy local execution | Compose builds and starts frontend, API and PostgreSQL; startup migrations and nine seeded users |
| Engineering handoff | Current schema/sequence documents, explicit assumptions, local commands and production rationale |
| AI-assisted development | Developer-directed Codex work and validation described in AiDevelopmentLog.md and the presentation |

Recorded validation: 69 .NET cases, 13 isolated browser tests, deployment browser and HTTP smoke checks, plus persistence/reseeding verification. See [validation results](ValidationResults.md); rerun against the intended submission commit.

Scope limits: no production identity provider; no real mortgage-application existence integration; no validated distributed-writer guarantees; no approval expiry/consumption contract. Service duplicate checks are not all backed by database unique constraints. See [assumptions](Assumptions.md).

Before handoff, confirm repository sharing and remote CI execution, review the actual changes, and include actual development time. Local results do not establish that remote CI has run.

Actual developer time: **not recorded yet**. The developer must enter the actual time; it cannot be inferred from chat or command durations.
