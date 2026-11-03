# Blue Harvest submission deliverables

Start with [QuickStart.md](QuickStart.md), then follow [DemoGuide.md](DemoGuide.md).

This directory contains review documents and delivery instructions. The runnable source and tests remain in their normal repository locations; submit the complete repository, rather than this directory alone.

## Deliverable inventory

| Deliverable | Location | Purpose |
| --- | --- | --- |
| API source and solution | [Backend solution](../Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx), [API source](../Backend/BlueHarvest.PricingExceptions/src/PricingExceptions.API) | Runnable controller API, services, mappers, models and InMemory persistence |
| Run and test instructions | [QuickStart.md](QuickStart.md) | Prerequisites, setup, startup, tests and reset behavior |
| Demo walkthrough | [DemoGuide.md](DemoGuide.md) | Create, approve, retrieve, revise, reapprove, decline and inspect history |
| Submission checklist | [SubmissionChecklist.md](SubmissionChecklist.md) | Implemented items and remaining developer-supplied submission details |
| Assumptions, Word | [Documents/Assumptions.docx](Documents/Assumptions.docx) | Business policies and exercise limitations in Word format |
| Assumptions, Markdown | [Documents/Assumptions.md](Documents/Assumptions.md) | Reviewable text version of the same assumptions |
| Solution design | [Documents/Blue_Harvest_Mortgage_Pricing_Exception_Design.docx](Documents/Blue_Harvest_Mortgage_Pricing_Exception_Design.docx) | Original proposed design; its SQLite proposal differs from this InMemory implementation |
| Class diagram | [Documents/BlueHarvest_Entity_Class_Diagram_and_Relationships.docx](Documents/BlueHarvest_Entity_Class_Diagram_and_Relationships.docx) | Entity responsibilities and relationships |
| Sequence diagrams | [Documents/BlueHarvest_PlantUML_Detailed_Sequence_Diagrams.docx](Documents/BlueHarvest_PlantUML_Detailed_Sequence_Diagrams.docx) | Create, review, revision and approved-discount interactions |
| Original assignment | [Documents/Blue Harvest Agentic SDLC Engineering Assignment.pdf](<Documents/Blue Harvest Agentic SDLC Engineering Assignment.pdf>) | Requirements and submission expectations |
| Requirements coverage and trade-offs | [Reports/SubmissionNotes.md](Reports/SubmissionNotes.md) | Requirement-to-implementation evidence and production changes |
| Validation results | [Reports/ValidationResults.md](Reports/ValidationResults.md) | Test outcomes, verification scope and limits |
| AI contribution record | [Reports/AiDevelopmentLog.md](Reports/AiDevelopmentLog.md) | AI-assisted tasks and how generated output was checked |
| Automated tests | [Test projects](../Backend/BlueHarvest.PricingExceptions/tests) | Service tests, HTTP integration tests and a live HTTP smoke script |
| CI configuration | [Backend workflow](../.github/workflows/backend.yml) | Restore, build and test on GitHub Actions; remote execution still to be verified |

## Delivery status

The API is an exercise implementation using EF Core InMemory and Development-only demo identities. It is not production-ready: data and retry records reset on restart, relational transactions are absent, and real mortgage application existence is not checked. The optional frontend is not included.

Actual developer time, repository sharing, final commits and remote CI status must be confirmed by the developer. See the checklist; these are not marked complete automatically.

## Refreshing this directory

Copies under Documents and Reports are submission snapshots. Their maintained source files stay under Documentation and Backend/BlueHarvest.PricingExceptions/docs. After editing those sources, run from the repository root:

```powershell
./Backend/BlueHarvest.PricingExceptions/tools/Prepare-Deliverables.ps1
```

The script refreshes copies without duplicating source code, build outputs or local test results.
