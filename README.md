# Mortgage pricing exception requests

An internal application for creating mortgage pricing exception requests, independently reviewing them, and retrieving approved discounts with preserved history.

## Current documentation

The [backend documentation index](Backend/BlueHarvest.PricingExceptions/docs/README.md) contains the latest entity diagram, sequence diagrams, assumptions, presentation, and local Docker instructions. Only one current copy of each document is maintained.

- [Run locally: Word guide](Backend/BlueHarvest.PricingExceptions/docs/BlueHarvest_Docker_Clear_Instructions.docx)
- [Docker commands and configuration](DOCKER.md)
- [Latest presentation](Backend/BlueHarvest.PricingExceptions/docs/BlueHarvest_Assignment_Presentation.pptx)
- [Assumptions and reasons](Backend/BlueHarvest.PricingExceptions/docs/Assumptions.docx)
- [Original assignment](<Backend/BlueHarvest.PricingExceptions/docs/Architecture/Blue Harvest Agentic SDLC Engineering Assignment.pdf>)

## Start with Docker

From the repository root containing compose.yaml:

```powershell
docker compose up --build -d
docker compose ps
```

Frontend: http://localhost:5173. Swagger: http://localhost:5267/swagger. PostgreSQL data is retained in the named volume during ordinary restarts and Compose down/up.

For source details, see the [backend README](Backend/BlueHarvest.PricingExceptions/README.md), [frontend README](FrontEnd/README.md), and [deliverables inventory](Deliverables/README.md).
