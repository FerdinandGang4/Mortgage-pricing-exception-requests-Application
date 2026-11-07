"""Refresh the existing Word design and add a current-code appendix to the deck.

Run after Update-MethodDocumentation.py and diagram rendering. Requires python-docx,
python-pptx and Pillow. PDF exports are produced separately through Office.
"""
from pathlib import Path
import re
import zipfile
from docx import Document
from docx.shared import Inches as WordInches
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from PIL import Image
from importlib.util import spec_from_file_location, module_from_spec

ROOT = Path(__file__).resolve().parents[3]
DOCS = ROOT / 'Backend/BlueHarvest.PricingExceptions/docs'
DIAGRAMS = DOCS / 'CurrentImplementation/diagrams'
spec = spec_from_file_location('method_docs', Path(__file__).with_name('Update-MethodDocumentation.py'))
method_docs = module_from_spec(spec)
spec.loader.exec_module(method_docs)

def refresh_design():
    path = DOCS / 'Architecture/Blue_Harvest_Mortgage_Pricing_Exception_Design.docx'
    document = Document(path)
    for paragraph in document.paragraphs:
        if paragraph.text.startswith('Version 1.0'):
            paragraph.text = 'Original proposal: 2 November 2026. Current-code method appendix updated 7 November 2026.'
        elif paragraph.text.startswith('Document status: proposed design only.'):
            paragraph.text = 'The earlier sections record the original proposal. The current-code appendix below supersedes those sections for implemented architecture and method names; SQLite and repository/unit-of-work abstractions in the proposal are not the current implementation.'
    # Keep the original requirements and rationale. Refresh actual identifiers in text and tables.
    def paragraphs(container):
        yield from container.paragraphs
        for table in container.tables:
            for row in table.rows:
                for cell in row.cells:
                    yield from paragraphs(cell)
    for paragraph in paragraphs(document):
        updated = method_docs.rename(paragraph.text)
        if updated != paragraph.text:
            paragraph.text = updated
    marker = 'Current implementation and method reference (7 November 2026)'
    if any(p.text == marker for p in document.paragraphs):
        raise RuntimeError('Design appendix already exists; review before regenerating.')
    document.add_page_break()
    document.add_heading(marker, 0)
    document.add_paragraph('This appendix reflects the implemented ASP.NET Core API, React UI and PostgreSQL deployment. The changes documented here rename methods and variables; they do not change HTTP routes, DTOs, authorization, database schema, idempotency scopes or business rules.')
    document.add_heading('Implemented architecture and workflow', 1)
    document.add_paragraph('Controllers call service interfaces directly. PricingExceptionService owns creation, revisions, decisions and history; UserService resolves stored identities and enforces roles; ApprovedDiscountService reads the currently approved discount. Services use PricingExceptionsDbContext directly. There are no separate repository or unit-of-work services. PostgreSQL is the normal provider; isolated tests explicitly select InMemory.')
    document.add_paragraph('Create submits revision 1 as Pending. The assigned independent reviewer approves or declines the current revision. The creator may submit new immutable terms, returning the request to Pending. Old terms and decisions remain in history. Only an approval on the current revision is effective.')
    document.add_heading('Current public methods and private helpers', 1)
    reference = (DOCS / 'CurrentImplementation/MethodReference.md').read_text(encoding='utf-8')
    for line in reference.splitlines():
        if line.startswith('## '):
            document.add_heading(line[3:], 2)
        elif line.startswith('- '):
            document.add_paragraph(line[2:].replace('`', ''), style='List Bullet')
    document.add_paragraph('ApprovedDiscountService.GetAsync is intentionally unchanged. PricingExceptionsController uses ActionName on GetPricingExceptionRequestByIdAsync so CreatedAtAction continues to produce the request Location header.')
    document.add_heading('Validation evidence', 1)
    document.add_paragraph('Latest local validation: backend build succeeded with zero warnings and errors; 184 backend tests passed (56 unit, 118 HTTP integration, 10 PostgreSQL); frontend build/lint and 20 browser tests passed. The Docker HTTP workflow, persistence across recreation and one deployed multi-user browser workflow also passed. These checks do not prove production readiness.')
    document.add_page_break()
    document.add_heading('Current entity relationships', 1)
    document.add_paragraph('Six persisted entities; unchanged by the naming refactor. MortgageApplicationId remains an external reference. Methods are documented in services and sequences rather than added as entity fields.')
    document.add_picture(str(DIAGRAMS / 'entity-relationships-overview.png'), width=WordInches(6.4))
    document.add_paragraph('The standalone entity document contains the full property dictionary and constraint caveats. See CurrentImplementation/EntityRelationships.md and the full-field SVG for editable detail.')
    for slug, title in [('02-create-request', 'Create and submit'), ('04-review-decision', 'Record a decision'),
                        ('05-submit-revision', 'Submit a revision'), ('07-read-history', 'Read request history')]:
        document.add_page_break()
        document.add_heading(title + ' — current method names', 1)
        with Image.open(DIAGRAMS / (slug + '.png')) as image:
            width, height = image.size
        scale = min(6.3 / width, 7.2 / height)
        document.add_picture(str(DIAGRAMS / (slug + '.png')), width=WordInches(width * scale))
        document.add_paragraph('All eight workflow diagrams, source references and implementation limits are maintained in BlueHarvest_Current_Sequence_Diagrams_Final.docx and its PDF companion.')
    document.save(path)
    print('Updated existing design Word document with current-code appendix.')

def refresh_presentation():
    path = DOCS / 'BlueHarvest_Assignment_Presentation.pptx'
    deck = Presentation(path)
    if any('CURRENT METHOD REFERENCE' in shape.text for slide in deck.slides for shape in slide.shapes if shape.has_text_frame):
        raise RuntimeError('Presentation appendix already exists; review before regenerating.')
    replacements = {'69 .NET tests passed': '184 .NET tests passed',
                    '24 unit cases + 45 HTTP cases.': '56 unit + 118 HTTP + 10 PostgreSQL cases.',
                    '13 browser tests passed': '20 browser tests passed'}
    for slide in deck.slides:
        for shape in slide.shapes:
            if not shape.has_text_frame:
                continue
            for paragraph in shape.text_frame.paragraphs:
                for run in paragraph.runs:
                    for old, new in replacements.items():
                        run.text = run.text.replace(old, new)
    teal, ink = RGBColor.from_string('007E87'), RGBColor.from_string('142B40')
    def text(slide, value, x, y, w, h, size=18, bold=False, color=ink):
        box = slide.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
        frame = box.text_frame
        frame.word_wrap = True
        for index, line in enumerate(value.split('\n')):
            paragraph = frame.paragraphs[0] if index == 0 else frame.add_paragraph()
            paragraph.text = line
            paragraph.font.name = 'Aptos'
            paragraph.font.size = Pt(size)
            paragraph.font.bold = bold
            paragraph.font.color.rgb = color
        return box
    new_slides = []
    def slide(title, subtitle):
        current = deck.slides.add_slide(deck.slide_layouts[0])
        current.background.fill.solid()
        current.background.fill.fore_color.rgb = RGBColor(255, 255, 255)
        text(current, 'BLUE HARVEST / CURRENT METHOD REFERENCE', .5, .2, 12.3, .3, 12, True, teal)
        text(current, title, .5, .65, 12.3, .65, 27, True)
        text(current, subtitle, .5, 1.35, 12.3, .55, 15)
        text(current, 'Code snapshot: 7 November 2026 | Naming refactor; HTTP contracts and database schema unchanged', .5, 7.1, 12.3, .25, 10, color=teal)
        new_slides.append(current)
        return current
    current = slide('Request actions and their service calls', 'Current names for creation, review and revision; the routes remain unchanged.')
    rows = [
        ('Create a request — POST /pricing-exceptions', 'CreatePricingExceptionRequestAsync', 'CreatePricingExceptionRequestAsync'),
        ('Record a decision — POST /{id}/decisions', 'RecordPricingExceptionDecisionAsync', 'ReviewPricingExceptionRequestAsync'),
        ('Submit a revision — POST /{id}/revisions', 'SubmitPricingExceptionRevisionAsync', 'SubmitRequestRevisionAsync'),
    ]
    for index, (label, action, service) in enumerate(rows):
        y = 2.05 + index * 1.5
        text(current, label, .65, y, 12, .4, 19, True, teal)
        text(current, 'Controller: ' + action + '\nService:     ' + service, .8, y + .45, 11.9, .85, 18)
    current = slide('Reads, identities and approved discounts', 'Action → service method. Every call retains its existing cancellation token and access checks.')
    rows = [
        ('GetPricingExceptionRequestsAsync', 'ListPricingExceptionRequestsAsync'),
        ('GetPricingExceptionRequestByIdAsync', 'GetPricingExceptionRequestAsync'),
        ('GetPricingExceptionHistoryAsync', 'GetRequestHistorySnapshotAsync'),
        ('GetCurrentUserProfileAsync', 'GetUserOrThrowAsync'),
        ('GetReviewersAsync', 'GetReviewersAsync'),
        ('GetApprovedDiscountForMortgageApplicationAsync', 'ApprovedDiscountService.GetAsync'),
    ]
    text(current, 'CONTROLLER ACTION', .65, 2, 6.2, .35, 13, True, teal)
    text(current, 'SERVICE METHOD', 7, 2, 5.8, .35, 13, True, teal)
    for index, (action, service) in enumerate(rows):
        y = 2.5 + index * .63
        text(current, action, .65, y, 6.2, .55, 14)
        text(current, service, 7, y, 5.8, .55, 14)
    current = slide('Helpers make the workflow responsibilities explicit', 'Private helpers remain below public methods. UserService preserves stored identity and role validation.')
    text(current, 'PricingExceptionService', .65, 2, 8, .4, 20, True, teal)
    helpers = ['ExecuteAuthorizedRequestReadAsync', 'FindRequestOrThrowAsync', 'ExecuteIdempotentWriteAsync',
               'ValidateDiscountTerms', 'ValidateRequestVersionAndRevision', 'CreateRequestRevision',
               'AppendRequestHistoryEntry', 'MapToPricingExceptionResult']
    text(current, '\n'.join(helpers), .8, 2.55, 7.2, 4, 20)
    text(current, 'UserService', 8.2, 2, 4.5, .4, 20, True, teal)
    text(current, 'GetUserOrThrowAsync\nGetUserWithRequiredRoleAsync\nGetReviewersAsync', 8.2, 2.55, 4.5, 2.2, 17)
    current = slide('Entity relationships remain unchanged', 'Six persisted entities. The naming refactor changes methods, not tables, properties or constraints.')
    def picture(current, filename):
        with Image.open(filename) as image:
            w, h = image.size
        scale = min(12.1 / w, 4.8 / h)
        width, height = w * scale, h * scale
        current.shapes.add_picture(str(filename), Inches((13.333-width)/2), Inches(1.95+(4.8-height)/2), width=Inches(width), height=Inches(height))
    picture(current, DIAGRAMS / 'entity-relationships-overview.png')
    for filename, title, subtitle in [
        ('09-current-workflow-methods.png', 'Sequence: submit, decide and revise', 'Current controller actions and service messages; each new revision requires a fresh decision.'),
        ('10-current-read-methods.png', 'Sequence: history and approved-discount lookup', 'Current read methods retain identity checks, history snapshots and effective-approval rules.')]:
        current = slide(title, subtitle)
        picture(current, DIAGRAMS / filename)
    # Keep the original closing slide last.
    ids = deck.slides._sldIdLst
    closing = ids[17]
    ids.remove(closing)
    ids.append(closing)
    total = len(deck.slides)
    for index, current in enumerate(deck.slides, 1):
        for shape in current.shapes:
            if shape.has_text_frame:
                for paragraph in shape.text_frame.paragraphs:
                    for run in paragraph.runs:
                        if re.fullmatch(r'\d+ / \d+', run.text.strip()):
                            run.text = f'{index} / {total}'
    deck.save(path)
    # Notes can contain source identifiers even when no visible slide uses them.
    with zipfile.ZipFile(path) as archive:
        entries = [(entry, archive.read(entry.filename)) for entry in archive.infolist()]
    with zipfile.ZipFile(path, 'w') as archive:
        for entry, data in entries:
            if entry.filename.endswith('.xml'):
                data = method_docs.rename(data.decode('utf-8')).encode('utf-8')
            archive.writestr(entry, data)
    print(f'Updated existing PowerPoint: {total} slides including current-code appendix.')

if __name__ == '__main__':
    refresh_design()
    refresh_presentation()
