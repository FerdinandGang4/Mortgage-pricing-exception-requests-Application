# Validation results and limits

Previously recorded local checks for the current multi-user PostgreSQL implementation:

| Check | Recorded result |
| --- | --- |
| .NET solution suite | 69 passed: 24 unit cases and 45 HTTP integration cases |
| Isolated Playwright suite | 13 passed, including user switching and the three-request reviewer flow |
| Browser flow against Docker/PostgreSQL | James creates three requests; David, Emily and Robert record their decisions successfully |
| Live HTTP smoke script | Creation, replay/conflict handling, approval, discount lookup, revision, history, input validation and existing access rules passed |
| Persistence test | Request, revision, decision, history and original retry response survived Compose down/up without deleting volumes |
| Repeated seed after backend restart | Nine users remained, with request, decision and history counts unchanged |

These are prior results, not a claim that tests were rerun during documentation cleanup. Reproduce the checks using the [Word run instructions](../BlueHarvest_Docker_Clear_Instructions.docx) or repository-root DOCKER.md.

The isolated .NET and browser suites explicitly use InMemory and do not prove PostgreSQL guarantees. Deployment and persistence checks exercise PostgreSQL. Passing tests does not establish production authentication, full failure coverage, distributed concurrency safety, regulatory compliance, or remote CI execution.

Documentation was validated separately by rendering diagram sources and checking generated Word/PDF/PowerPoint artifacts. Current artifacts are indexed in [backend docs](../README.md); older copied reports and exports have been removed.
