# Current implementation diagrams

These documents describe the current code and PostgreSQL migration, rather than the earlier proposed design. They do not change application behaviour.

## Review documents

| Document | Editable copy | PDF |
| --- | --- | --- |
| Entity relationship diagram and full property dictionary | [Word](../BlueHarvest_Current_Entity_Relationships_Final.docx) | [PDF](../BlueHarvest_Current_Entity_Relationships.pdf) |
| Eight workflow sequence diagrams, with code references and caveats | [Word](../BlueHarvest_Current_Sequence_Diagrams_Final.docx) | [PDF](../BlueHarvest_Current_Sequence_Diagrams.pdf) |

## Diagram sources and images

- [ER overview](diagrams/entity-relationships-overview.svg) · [full-field ER](diagrams/entity-relationships-full.svg) · [data dictionary in Markdown](EntityRelationships.md).
- [Sequence diagram index in Markdown](SequenceDiagrams.md).
- Each diagram has editable `.mmd` source and standalone `.svg` and `.png` images in [diagrams](diagrams). SVGs remain readable when zoomed.

Sequence diagrams cover development identities and reviewer selection; request creation; shared write execution and retries; approval/decline; revisions; approved-discount lookup; history; and startup/migrations/seeding.

## Important implementation distinctions

- Six persisted entities; mortgage applications remain external ID references.
- `RequestRevision` has a composite primary key. A unique composite foreign-key index permits at most one `ReviewDecision` per revision.
- `HistoryEntry` references both the request and its revision. `IdempotencyRecord.RequestId` is nullable in the model.
- The current-revision pointer, one-request-per-application policy, and actor/scope/key lookup are not all database uniqueness or foreign-key constraints. The documents distinguish the actual schema from service rules.
- The singleton semaphore coordinates one API process. It does not provide distributed locking or cross-instance snapshot consistency.
- Historical-record immutability is guarded by application `SaveChanges`, not PostgreSQL append-only triggers. Direct database access can bypass that guard.
- PostgreSQL is the application provider; InMemory is explicitly selected by isolated tests. The current development identity mechanism is not production sign-in.

## Source locations

- [Entities](../../src/PricingExceptions.API/Models/Entities) and [enums](../../src/PricingExceptions.API/Models/Enums).
- [DbContext mappings and save guards](../../src/PricingExceptions.API/Data/PricingExceptionsDbContext.cs) and [migrations](../../src/PricingExceptions.API/Data/Migrations).
- [Workflow services](../../src/PricingExceptions.API/Services), [controllers](../../src/PricingExceptions.API/Controllers), and [development authentication](../../src/PricingExceptions.API/Authentication/DevelopmentAuthenticationHandler.cs).
- [Startup](../../src/PricingExceptions.API/Program.cs), [seed data](../../src/PricingExceptions.API/Data/Seed/DevelopmentDataSeeder.cs), and [Compose](../../../../compose.yaml).

## Regeneration

`generate_documents.py prepare` extracts scalar properties from the entity classes and writes Mermaid/Markdown sources. Relationship mappings and sequence descriptions in that script must be reviewed when workflow or schema changes. They are code-derived documentation, not automatic static-analysis proofs.

Render the `.mmd` files with Mermaid CLI into matching `.svg` and `.png` files, then run `generate_documents.py documents` with Python and `python-docx` installed. Run `generate_documents.py pdfs` with PyMuPDF installed to generate PDF companions directly from the same diagram images and implementation notes. The PDFs use dedicated diagram pages for readability. Diagrams were syntax-checked by rendering with Mermaid CLI using Microsoft Edge.

Generated Word/PDF documents are written directly to the parent backend docs directory. Superseded diagrams and duplicate exports have been removed; this directory retains the current editable sources and images.
