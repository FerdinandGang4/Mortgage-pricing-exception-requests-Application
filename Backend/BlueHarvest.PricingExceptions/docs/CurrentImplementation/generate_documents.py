"""Generate Mermaid sources from the current code, then Word documents from rendered PNGs.

python generate_documents.py prepare
Render diagrams/*.mmd to PNG and SVG with Mermaid CLI, then:
python generate_documents.py documents
"""
from pathlib import Path
import re
import struct
import textwrap
import sys
from datetime import date

HERE = Path(__file__).resolve().parent
OUTPUT = HERE.parent
API = HERE.parents[1] / 'src' / 'PricingExceptions.API'
DIAGRAMS = HERE / 'diagrams'

RELATIONS = '''
    User ||--o{ PricingExceptionRequest : "CreatedByUserId"
    User ||--o{ PricingExceptionRequest : "AssignedReviewerId"
    User ||--o{ RequestRevision : "SubmittedByUserId"
    User ||--o{ ReviewDecision : "ReviewedByUserId"
    User ||--o{ HistoryEntry : "ActorUserId"
    User ||--o{ IdempotencyRecord : "ActorUserId"
    PricingExceptionRequest ||--o{ RequestRevision : "RequestId"
    PricingExceptionRequest ||--o{ HistoryEntry : "RequestId"
    PricingExceptionRequest |o--o{ IdempotencyRecord : "nullable RequestId"
    RequestRevision ||--o| ReviewDecision : "RequestId + RevisionNumber"
    RequestRevision ||--o{ HistoryEntry : "RequestId + RevisionNumber"
'''
PK = {'User': {'Id'}, 'PricingExceptionRequest': {'Id'}, 'RequestRevision': {'RequestId', 'RevisionNumber'}, 'ReviewDecision': {'Id'}, 'HistoryEntry': {'Id'}, 'IdempotencyRecord': {'Id'}}
FK = {'User': set(), 'PricingExceptionRequest': {'CreatedByUserId', 'AssignedReviewerId'}, 'RequestRevision': {'RequestId', 'SubmittedByUserId'}, 'ReviewDecision': {'RequestId', 'RevisionNumber', 'ReviewedByUserId'}, 'HistoryEntry': {'RequestId', 'RevisionNumber', 'ActorUserId'}, 'IdempotencyRecord': {'ActorUserId', 'RequestId'}}
TABLES = {'User': 'Users', 'PricingExceptionRequest': 'PricingExceptionRequests', 'RequestRevision': 'RequestRevisions', 'ReviewDecision': 'ReviewDecisions', 'HistoryEntry': 'HistoryEntries', 'IdempotencyRecord': 'IdempotencyRecords'}

def entities():
    result = {}
    for name in TABLES:
        source = (API / 'Models' / 'Entities' / (name + '.cs')).read_text(encoding='utf-8-sig')
        result[name] = [(kind, prop) for kind, prop in re.findall(r'public\s+(?:required\s+)?([\w?]+)\s+(\w+)\s*\{\s*get;', source)]
    return result

SEQUENCES = [
('01-development-identity', 'Development identity and reviewer selection', '''sequenceDiagram
    autonumber
    actor U as Demo user
    participant UI as React
    participant A as Auth handler
    participant C as UsersController
    participant S as UserService
    participant D as EF / PostgreSQL
    U->>UI: Select Demo User (stable seeded ID)
    UI->>A: GET /api/v1/users/me + X-User-Id
    alt Development and parseable user GUID
        A->>D: Read user by ID
        D-->>A: DisplayName and stored role
        alt User exists
            A->>C: ClaimsPrincipal (ID, name, role)
            C->>S: GetUserOrThrowAsync(actorId)
            S->>D: Read and validate internal user
            S-->>C: User
            C-->>UI: 200 UserResponseDto
        else Unknown user
            A-->>UI: Authentication failure (401)
        end
    else Not Development or invalid / missing GUID
        A-->>UI: No identity / authentication failure (401)
    end
    opt Manager opens create dialog
        UI->>A: GET /api/v1/users/reviewers
        Note over A,C: Authenticate; require RelationshipManager role
        C->>S: GetReviewersAsync(actorId)
        S->>D: Read role; select Reviewer users excluding actor
        D-->>S: Reviewer users ordered by DisplayName
        S-->>C: Eligible reviewers
        C-->>UI: 200 reviewer dropdown data
    end''',
 'DevelopmentAuthenticationHandler.cs; UsersController.cs; UserService.cs; Header.tsx; RequestFormDialog.tsx.',
 'The ID header supplies no trusted role: the handler reads the stored role. Invalid GUIDs and unknown users fail authentication. Outside Development the demo scheme returns no identity. The API rechecks stored roles in services. There are three managers, five reviewers and one Mortgage Process user; switching closes dialogs and refreshes actor-dependent reads.'),
('02-create-request', 'Create a pricing exception request', '''sequenceDiagram
    autonumber
    actor M as Manager
    participant UI as React form
    participant C as Controller
    participant S as PricingExceptionService
    participant W as Write wrapper
    participant D as EF / PostgreSQL
    M->>UI: Enter application GUID, bps, reason, reviewer
    UI->>C: POST /api/v1/pricing-exceptions + identity + key
    Note over C,S: Authentication, role and DTO validation occur before action logic
    C->>S: CreateAsync(... actorId, idempotencyKey)
    S->>D: GetUserWithRequiredRoleAsync(RelationshipManager)
    S->>W: ExecuteWriteAsync(actor, create, key, payload)
    Note over W,D: Shared write wrapper handles lock and retries (diagram 03)
    W->>S: Run operation callback for a new key
    S->>S: Validate bps, reason and non-empty application GUID
    S->>D: Find independent Reviewer and check application duplicate
    alt Invalid input / reviewer or duplicate application
        S-->>C: BusinessRuleException / ConflictException
        C-->>UI: Global handler returns 400 / 409 Problem Details
    else Valid new request
        S->>D: Stage Pending request (version 1, revision 1)
        S->>D: Stage terms snapshot and Created history
        S-->>W: Result with requester / assigned-reviewer name snapshots
        W->>D: Add retry result; SaveChangesAsync
        D-->>W: Transaction committed
        W-->>S: Saved result
        S-->>C: PricingExceptionResult
        C-->>UI: 201 DTO + Location pointing to request GET
    end''',
 'PricingExceptionsController.CreatePricingExceptionRequestAsync; PricingExceptionService.CreateAsync, NewRevision, AppendHistory; ExecuteWriteAsync.',
 'Same-key replays bypass the operation callback. A different key for an existing mortgage application hits the service duplicate check. MortgageApplicationId is an external reference, not a foreign key to a local mortgage table. One request per application is a service policy, not a unique database index in the current migration.'),
('03-write-and-retry', 'Shared write execution and idempotent retries', '''sequenceDiagram
    autonumber
    participant S as Service entry point
    participant W as ExecuteWriteAsync
    participant L as Singleton semaphore
    participant D as EF / PostgreSQL
    S->>W: actor, scope, key, payload, callback
    W->>W: Validate key length; SHA256(serialized payload)
    W->>L: WaitAsync(cancellationToken)
    L-->>W: Acquired in this API process
    W->>D: Clear tracker; find actor + scope + key
    alt Saved record with same input hash
        D-->>W: Saved ResponseJson
        W-->>S: Deserialize and replay original result (no callback)
    else Saved record with different hash
        W-->>S: ConflictException (HTTP 409)
    else No saved record
        W->>S: Execute operation callback
        S-->>W: Result with staged workflow / history changes
        W->>D: Stage IdempotencyRecord with serialized result
        W->>D: SaveChangesAsync (historical-record guard)
        Note over W,D: PostgreSQL transaction saves staged changes together
        alt Successful save
            D-->>W: Committed
            W-->>S: Original operation result
        else EF concurrency exception
            W->>D: Clear tracker
            W-->>S: ConflictException (HTTP 409)
        end
    end
    Note over W,L: finally releases semaphore; other caught errors clear tracker and rethrow''',
 'PricingExceptionService.ExecuteWriteAsync; PricingExceptionsDbContext.SaveChangesAsync / ProtectHistoricalRecords; InMemoryWriteLock.cs.',
 'Scopes are create, revise:{requestId}, and review:{requestId}. The actor is part of the lookup. The saved ResponseStatusCode is set to 200 for review and 201 otherwise, but controllers actually choose HTTP status and Location on each response. Location is a nullable entity property and is not populated by this wrapper. The semaphore serializes only one API process; actor/scope/key has no database unique index yet.'),
('04-review-decision', 'Approve or decline the assigned revision', '''sequenceDiagram
    autonumber
    actor R as Reviewer
    participant C as Controller
    participant S as PricingExceptionService
    participant W as Write wrapper
    participant D as EF / PostgreSQL
    R->>C: POST /api/v1/pricing-exceptions/{id}/decisions
    Note over R,C: Outcome, optional reason, expectedVersion, reviewedRevisionNumber, key
    C->>S: ReviewAsync(... actorId)
    S->>D: GetUserWithRequiredRoleAsync(Reviewer)
    S->>W: ExecuteWriteAsync(review:id, payload)
    W->>S: New-key callback after lock / replay lookup
    S->>D: Load request and validate outcome / reason
    S->>S: Reject creator and unassigned reviewer (403)
    S->>S: Check expected version and current revision (409)
    S->>D: Require Pending and no existing decision (409 otherwise)
    S->>D: Load reviewed revision
    S->>D: Stage decision with reviewer ID, name, UTC time, note
    S->>D: Set Approved or Declined; increment Version; append history
    S-->>W: Result
    W->>D: Stage retry record and SaveChangesAsync
    W-->>C: Result via service
    C-->>R: 200 response DTO
    Note over C,D: Authentication errors are 401; wrong role / assignment is 403''',
 'PricingExceptionsController.RecordPricingExceptionDecisionAsync; PricingExceptionService.ReviewAsync; ReviewDecision entity; UserService.GetUserWithRequiredRoleAsync.',
 'The current request must be Pending and match both expected values. Assignment is independent of who last edited or viewed a record. A unique (RequestId, RevisionNumber) index on ReviewDecisions additionally enforces at most one decision per revision in PostgreSQL. The decision record has no direct foreign key to PricingExceptionRequest; it references RequestRevision.'),
('05-submit-revision', 'Revise terms and withdraw effective approval', '''sequenceDiagram
    autonumber
    actor M as Original manager
    participant C as Controller
    participant S as PricingExceptionService
    participant W as Write wrapper
    participant D as EF / PostgreSQL
    M->>C: POST /api/v1/pricing-exceptions/{id}/revisions
    Note over M,C: bps, reason, expectedVersion, expectedRevisionNumber, key
    C->>S: SubmitRevisionAsync(... actorId)
    S->>D: GetUserWithRequiredRoleAsync(RelationshipManager)
    S->>W: ExecuteWriteAsync(revise:id, payload)
    W->>S: New-key callback after lock / replay lookup
    S->>S: Validate terms
    S->>D: Find tracked request (404 if missing)
    S->>S: Require creator (403); match version and revision (409)
    S->>D: Increment revision and version; set Pending and UTC update time
    S->>D: Stage new immutable terms and Revised history
    Note over S,D: Assigned reviewer unchanged; old decisions and revisions retained
    S-->>W: Result for new revision (no decision)
    W->>D: Stage retry result and SaveChangesAsync
    W-->>C: Result via service
    C-->>M: 201 response DTO + request Location
    Note over M,D: Discount lookup now returns 404 until the new revision is approved''',
 'PricingExceptionsController.SubmitPricingExceptionRevisionAsync; PricingExceptionService.SubmitRevisionAsync, CheckVersion, NewRevision; ApprovedDiscountService.GetAsync.',
 'Revision is allowed while Pending and after either decision. Even unchanged terms create a new revision. No old approval is deleted; it becomes historical because current revision and status change. Version increases on both revision and decision; revision number increases only when terms are submitted.'),
('06-approved-discount', 'Retrieve the effective approved discount', '''sequenceDiagram
    autonumber
    actor P as Mortgage process
    participant C as ApprovedDiscountsController
    participant S as ApprovedDiscountService
    participant L as Singleton semaphore
    participant D as EF / PostgreSQL
    P->>C: GET /api/v1/mortgage-applications/{id}/approved-discount
    Note over P,C: Authenticated MortgageProcess role required
    C->>S: GetAsync(applicationId, actorId)
    S->>D: Recheck stored MortgageProcess role
    S->>S: Require non-empty application GUID (400)
    S->>L: WaitAsync
    S->>D: Join request to CURRENT revision and its decision
    Note over S,D: Require request.Status = Approved and decision.Outcome = Approved
    alt Current approval exists
        D-->>S: Discount, revision, reviewer and UTC decision time
        S-->>C: ApprovedDiscountResult
        C-->>P: 200 mapped DTO
    else Missing, Pending or Declined
        D-->>S: No match
        S-->>C: null
        C-->>P: NotFoundException mapped to 404 Problem Details
    end
    Note over S,L: Service finally releases the lock before controller responds''',
 'ApprovedDiscountsController.GetApprovedDiscountForMortgageApplicationAsync; ApprovedDiscountService.GetAsync; ApprovedDiscountMapper.cs.',
 'Only the current revision participates. Historical approvals never substitute for Pending revised terms. This endpoint reads exception state; it neither fetches a mortgage application nor consumes an approval. An approval has no implemented expiry or external revocation contract.'),
('07-read-history', 'Read a consistent request history snapshot', '''sequenceDiagram
    autonumber
    actor U as Known internal user
    participant C as Controller
    participant S as PricingExceptionService
    participant L as Singleton semaphore
    participant D as EF / PostgreSQL
    U->>C: GET /api/v1/pricing-exceptions/{id}/history
    C->>S: GetHistorySnapshotAsync(id, actorId)
    S->>D: GetUserOrThrowAsync(actorId)
    S->>L: ReadRequestAsync acquires shared semaphore
    S->>D: Confirm request exists
    alt Missing request
        S-->>C: NotFoundException (404)
    else Existing request
        S->>D: Read HistoryEntries ordered by Sequence
        S->>D: Read RequestRevisions ordered by RevisionNumber
        S->>D: Read ReviewDecisions ordered by RevisionNumber
        D-->>S: Three collections assembled under the same lock
        S-->>C: RequestHistoryResult
        C-->>U: 200 HistoryMapper response
    end
    Note over S,L: finally releases semaphore
    Note over U,D: React displays events newest first using Sequence''',
 'PricingExceptionsController.GetPricingExceptionHistoryAsync; PricingExceptionService.GetHistorySnapshotAsync / ReadRequestAsync; HistoryMapper.cs; HistoryTimeline.tsx.',
 'Any known internal user may read history under current policy. The lock coordinates only writes in this process; the reads are not wrapped in a database snapshot transaction, so do not claim a cross-instance atomic snapshot. History uses request Version as Sequence. AsNoTracking reads avoid modifying submitted records.'),
('08-startup', 'Startup, PostgreSQL migrations and idempotent seeding', '''sequenceDiagram
    autonumber
    participant O as Docker Compose
    participant P as PostgreSQL
    participant A as Program.cs
    participant D as DbContext
    participant S as DevelopmentDataSeeder
    O->>P: Start database with persistent named volume
    O->>P: pg_isready health check
    P-->>O: Healthy
    O->>A: Start backend with connection string
    A->>A: Register controllers, DI services, demo auth and singleton lock
    A->>D: Create startup scope
    alt PostgreSQL relational provider
        D->>P: MigrateAsync (pending EF migrations)
    else Explicit test-only InMemory provider
        D->>D: EnsureCreatedAsync
    end
    opt Development environment
        A->>S: SeedAsync
        loop Each of nine predefined stable user IDs
            S->>D: Find user
            alt User absent
                S->>D: Add User
            else User exists
                S->>D: Update current display name and role
            end
        end
        S->>D: SaveChangesAsync
    end
    A->>A: Expose controllers; Swagger only in Development
    Note over A,P: Requests and historical name snapshots are not rewritten by seeding''',
 'compose.yaml; Program.cs; DevelopmentDataSeeder.cs; Data/Migrations.',
 'The application defaults to PostgreSQL. Only an explicit Database:Provider=InMemory selects temporary test storage. Compose starts the frontend after the backend container starts, but does not currently wait for backend HTTP readiness. PostgreSQL migrations occur before the API serves requests. No additional production authentication is implied by this startup flow.'),
]

ER_NOTES = [
    'The six entities shown are the current persisted model, not the original proposal. PostgreSQL table names are plural DbSet names. MortgageApplicationId is an external reference: no local MortgageApplication table or foreign key exists.',
    'RequestRevision uses composite primary key (RequestId, RevisionNumber). ReviewDecision has its own Id primary key and a unique foreign-key pair (RequestId, RevisionNumber), giving a revision zero or one decision. HistoryEntry references both its request and its composite revision.',
    'IdempotencyRecord.RequestId is nullable in the model and migration, although the workflow currently fills it. CurrentRevisionNumber is a service-managed pointer: there is no composite foreign key from PricingExceptionRequest to its current revision.',
    'Version is an EF concurrency token; it is not a database-generated row version. One request per application and actor/scope/key retry uniqueness are checked by services: the current migration does not create unique indexes for either rule.',
    'Submitted revisions, decisions, history and completed retry records are protected from edits/deletion through DbContext SaveChanges guards. The PostgreSQL migration defines cascade deletes for required FKs, and an optional retry-record FK without cascade; these are not database append-only triggers. Direct SQL can bypass application guards.',
    'RequesterName, AssignedReviewerName and ReviewerName are stored name snapshots. Changing a current user profile does not rewrite historical attribution. Enums are stored as PostgreSQL integers; API JSON uses enum names.',
    'The service sets ResponseStatusCode and ResponseJson on retry records, but does not populate nullable Location. The controller derives HTTP status and Location on each response. HistoryEntry.Details is nullable and the workflow does not currently set it.',
]

def prepare():
    DIAGRAMS.mkdir(parents=True, exist_ok=True)
    model = entities()
    for overview in (True, False):
        lines = ['erDiagram', '    direction LR']
        for name, props in model.items():
            lines.append(f'    {name} {{')
            for kind, prop in props:
                if overview and prop not in PK[name] | FK[name] and prop not in {'MortgageApplicationId', 'CurrentRevisionNumber', 'Version', 'Status', 'Role'}:
                    continue
                marks = ','.join(k for k, fields in [('PK', PK[name]), ('FK', FK[name])] if prop in fields)
                dtype = kind.replace('?', '')
                optional = ' "nullable"' if kind.endswith('?') else ''
                lines.append(f'        {dtype} {prop}' + (f' {marks}' if marks else '') + optional)
            lines.append('    }')
        lines.append(RELATIONS)
        (DIAGRAMS / ('entity-relationships' + ('-overview' if overview else '-full') + '.mmd')).write_text('\n'.join(lines), encoding='utf-8')
    ermd = '# Current entity relationship model\n\nDerived from entities, DbContext mappings and the checked-in PostgreSQL migration.\n\n'
    ermd += '![Entity relationship overview](diagrams/entity-relationships-overview.svg)\n\n'
    ermd += '\n\n'.join(ER_NOTES) + '\n\n'
    for name, props in model.items():
        ermd += f'## {name} / {TABLES[name]}\n\n| Property | C# type | Key |\n| --- | --- | --- |\n'
        for kind, prop in props:
            marks = ', '.join(k for k, fields in [('PK', PK[name]), ('FK', FK[name])] if prop in fields)
            ermd += f'| {prop} | {kind} | {marks or "—"} |\n'
        ermd += '\n'
    (HERE / 'EntityRelationships.md').write_text(ermd, encoding='utf-8')
    seqmd = '# Current sequence diagrams\n\nPostgreSQL application implementation; explicit test-only InMemory path is noted in startup.\n\n'
    for slug, title, source, references, note in SEQUENCES:
        rendered = []
        for line in source.replace(';', ',').splitlines():
            if line.strip().startswith('Note over'):
                prefix, message = line.split(':', 1)
                line = prefix + ': ' + '<br/>'.join(textwrap.wrap(message.strip(), width=30))
            rendered.append(line)
        (DIAGRAMS / (slug + '.mmd')).write_text('\n'.join(rendered) + '\n', encoding='utf-8')
        seqmd += f'## {title}\n\n![{title}](diagrams/{slug}.svg)\n\n{note}\n\nSources: {references}\n\n'
    (HERE / 'SequenceDiagrams.md').write_text(seqmd, encoding='utf-8')
    (HERE / 'schema.json').write_text(__import__('json').dumps(model, indent=2), encoding='utf-8')
    print(f'Prepared 2 ER diagrams and {len(SEQUENCES)} sequence diagrams.')

def documents():
    from docx import Document
    from docx.shared import Inches, Pt, RGBColor
    from docx.enum.section import WD_ORIENT
    def doc(title, landscape=True):
        d = Document()
        sec = d.sections[0]
        sec.orientation = WD_ORIENT.LANDSCAPE if landscape else WD_ORIENT.PORTRAIT
        sec.page_width, sec.page_height = (Inches(11.69), Inches(8.27)) if landscape else (Inches(8.27), Inches(11.69))
        sec.top_margin = sec.bottom_margin = Inches(.45)
        sec.left_margin = sec.right_margin = Inches(.55)
        normal = d.styles['Normal']
        normal.font.name, normal.font.size = 'Aptos', Pt(10)
        normal.paragraph_format.space_after = Pt(5)
        for name in ('Title', 'Heading 1', 'Heading 2'):
            d.styles[name].font.color.rgb = RGBColor.from_string('007E87')
        sec.header.paragraphs[0].text = 'BLUE HARVEST | CURRENT CODE DOCUMENTATION'
        sec.footer.paragraphs[0].text = 'Source-derived snapshot • PostgreSQL implementation • Generated ' + date.today().isoformat()
        d.add_heading(title, 0)
        d.add_paragraph('Generated from the repository code. Earlier design documents remain unchanged and may describe previous proposals.')
        return d
    def image(d, name, maxh=5.15, maxw=10.4):
        p = DIAGRAMS / (name + '.png')
        data = p.read_bytes()
        w, h = struct.unpack('>II', data[16:24])
        scale = min(maxw / w, maxh / h)
        d.add_picture(str(p), width=Inches(w * scale), height=Inches(h * scale))
    d = doc('Entity relationship diagram and data dictionary')
    image(d, 'entity-relationships-overview', 5.8)
    d.add_paragraph('PK = primary key; FK = foreign key. Crow-foot relationships describe physical constraints, not additional service-only rules. For a zoomable full-field diagram, open diagrams/entity-relationships-full.svg.')
    d.add_page_break()
    d.add_heading('Relationships, constraints and important boundaries', 1)
    for note in ER_NOTES:
        d.add_paragraph(note, style='List Bullet')
    for name, props in entities().items():
        d.add_page_break()
        d.add_heading(f'{name} — {TABLES[name]}', 1)
        table = d.add_table(rows=1, cols=4)
        table.style = 'Light Shading Accent 1'
        for cell, value in zip(table.rows[0].cells, ['Property', 'C# type', 'PostgreSQL storage', 'Key / nullability']):
            cell.text = value
        types = {'Guid': 'uuid', 'int': 'integer', 'long': 'bigint', 'DateTimeOffset': 'timestamp with time zone', 'string': 'text', 'UserRole': 'integer enum', 'RequestStatus': 'integer enum', 'DecisionOutcome': 'integer enum'}
        for kind, prop in props:
            flags = [k for k, fields in [('PK', PK[name]), ('FK', FK[name])] if prop in fields]
            flags.append('nullable' if kind.endswith('?') else 'required')
            storage = types.get(kind.replace('?', ''), kind)
            if name == 'PricingExceptionRequest' and prop in {'RequesterName', 'AssignedReviewerName'}:
                storage = 'varchar(200)'
            for cell, value in zip(table.add_row().cells, [prop, kind, storage, ', '.join(flags)]):
                cell.text = value
    d.add_paragraph('Sources: src/PricingExceptions.API/Models/Entities/*.cs; Models/Enums/*.cs; Data/PricingExceptionsDbContext.cs; Data/Migrations/*InitialPostgreSql.cs and model snapshot.')
    d.save(OUTPUT / 'BlueHarvest_Current_Entity_Relationships_Final.docx')
    d = doc('Sequence diagrams — current application workflows', landscape=False)
    d.add_paragraph('Eight diagrams cover identity/reviewer selection, creation, shared retries, decisions, revisions, discount lookup, history, and startup. Common HTTP authentication and DTO validation are condensed in workflow diagrams; diagram 01 expands identity handling.')
    d.add_paragraph('All writes reuse ExecuteWriteAsync. It coordinates a single API process and persists state/history/retry records in one SaveChanges call. The documentation does not imply multi-instance transaction or uniqueness guarantees that the code does not implement.')
    for slug, title, source, references, note in SEQUENCES:
        d.add_page_break()
        d.add_heading(title, 1)
        image(d, slug, 8.0, 7.1)
        d.add_paragraph(note)
        d.add_paragraph('Code references: ' + references)
    d.save(OUTPUT / 'BlueHarvest_Current_Sequence_Diagrams_Final.docx')
    print('Created both Word documents with embedded diagrams.')

def pdfs():
    import fitz
    teal, ink, muted = (0, .40, .44), (.08, .15, .24), (.31, .39, .47)
    def page(pdf, title, landscape=False):
        p = pdf.new_page(width=842 if landscape else 595, height=595 if landscape else 842)
        p.draw_rect(fitz.Rect(0, 0, p.rect.width, 7), fill=teal, color=teal)
        p.insert_text((30, 28), 'BLUE HARVEST | CURRENT CODE DOCUMENTATION', fontsize=9, color=teal)
        p.insert_textbox(fitz.Rect(30, 45, p.rect.width-30, 85), title, fontsize=18, fontname='hebo', color=ink)
        p.insert_text((30, p.rect.height-18), f'Code-derived PostgreSQL snapshot | Page {len(pdf)}', fontsize=8, color=muted)
        return p
    def para(p, value, y, size=10):
        # Measure first so no paragraph is silently truncated.
        rect = fitz.Rect(30, y, p.rect.width-30, p.rect.height-35)
        outcome = p.insert_textbox(rect, value, fontsize=size, fontname='helv', color=ink, lineheight=1.25)
        if outcome < 0:
            raise ValueError('PDF text does not fit: ' + value[:70])
        return y + rect.height - outcome + 8
    pdf = fitz.open()
    p = page(pdf, 'Entity relationships - current implementation', True)
    p.insert_image(fitz.Rect(25, 100, 817, 425), filename=str(DIAGRAMS/'entity-relationships-overview.png'), keep_proportion=True)
    para(p, 'Six persisted entities. PK = primary key; FK = foreign key. Nullable request links and composite revision keys reflect EF mappings. Open the standalone SVGs for zoomable diagrams and the Word/Markdown dictionary for property details.', 445)
    p = page(pdf, 'Schema constraints and service boundaries', True)
    y = 100
    for note in ER_NOTES:
        y = para(p, '- ' + note, y, 10)
    for name, props in entities().items():
        p = page(pdf, name + ' / ' + TABLES[name], True)
        y = 102
        for kind, prop in props:
            flags = ', '.join(k for k, fields in [('PK', PK[name]), ('FK', FK[name])] if prop in fields)
            if kind.endswith('?'):
                flags = (flags + '; ' if flags else '') + 'nullable'
            y = para(p, f'{prop} : {kind}' + (f' [{flags}]' if flags else ''), y, 12)
    pdf.save(OUTPUT/'BlueHarvest_Current_Entity_Relationships.pdf', garbage=4, deflate=True, deflate_images=True)
    pdf.close()
    pdf = fitz.open()
    p = page(pdf, 'Sequence diagrams - current implementation')
    y = para(p, 'Eight workflows derived from the current controllers, services, authentication handler, startup and EF persistence code. Diagram messages condense implementation details; sources and scope limits are stated for each workflow.', 100, 12)
    for _, title, _, _, _ in SEQUENCES:
        y = para(p, '- ' + title, y, 11)
    para(p, 'The write wrapper and read semaphore coordinate a single API process. No distributed locking or cross-instance history snapshot guarantee is implied. PostgreSQL is the normal application provider; InMemory is selected explicitly for isolated tests.', y + 15, 11)
    for slug, title, source, references, note in SEQUENCES:
        p = page(pdf, title)
        # Reserve a dedicated diagram page to keep long sequences readable when zoomed.
        p.insert_image(fitz.Rect(25, 95, 570, 785), filename=str(DIAGRAMS/(slug+'.png')), keep_proportion=True)
        p = page(pdf, 'Implementation notes: ' + title)
        y = para(p, note, 105, 12)
        para(p, 'Code references: ' + references, y + 15, 11)
    pdf.save(OUTPUT/'BlueHarvest_Current_Sequence_Diagrams.pdf', garbage=4, deflate=True, deflate_images=True)
    pdf.close()
    print('Created both PDF companions with diagram pages and implementation notes.')

if __name__ == '__main__':
    {'prepare': prepare, 'documents': documents, 'pdfs': pdfs}[sys.argv[1]]()
