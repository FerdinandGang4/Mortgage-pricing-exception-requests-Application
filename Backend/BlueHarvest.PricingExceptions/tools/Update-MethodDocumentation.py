"""Refresh method-name documentation from the checked-in application source."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[3]
DOCS = ROOT / 'Backend/BlueHarvest.PricingExceptions/docs'
API = DOCS.parent / 'src/PricingExceptions.API'
RENAMES = {
    'ListAsync': 'ListPricingExceptionRequestsAsync',
    'CreateAsync': 'CreatePricingExceptionRequestAsync',
    'SubmitRevisionAsync': 'SubmitRequestRevisionAsync',
    'ReviewAsync': 'ReviewPricingExceptionRequestAsync',
    'GetHistoryAsync': 'GetRequestHistoryAsync',
    'GetRevisionsAsync': 'GetRequestRevisionsAsync',
    'GetDecisionsAsync': 'GetReviewDecisionsAsync',
    'GetHistorySnapshotAsync': 'GetRequestHistorySnapshotAsync',
    'ReadRequestAsync': 'ExecuteAuthorizedRequestReadAsync',
    'FindAsync': 'FindRequestOrThrowAsync',
    'ExecuteWriteAsync': 'ExecuteIdempotentWriteAsync',
    'ValidateTerms': 'ValidateDiscountTerms',
    'CheckVersion': 'ValidateRequestVersionAndRevision',
    'NewRevision': 'CreateRequestRevision',
    'AppendHistory': 'AppendRequestHistoryEntry',
    'GetRequiredAsync': 'GetUserOrThrowAsync',
    'RequireRoleAsync': 'GetUserWithRequiredRoleAsync',
}

def rename(text):
    for old, new in RENAMES.items():
        text = re.sub(r'\b' + old + r'\b', new, text)
    text = text.replace('PricingExceptionService.GetAsync', 'PricingExceptionService.GetPricingExceptionRequestAsync')
    text = re.sub(r'\bResult\(', 'MapToPricingExceptionResult(', text)
    return text

def prepare():
    generator = DOCS / 'CurrentImplementation/generate_documents.py'
    text = rename(generator.read_text(encoding='utf-8-sig'))
    text = text.replace("from datetime import date", "from datetime import date\nSNAPSHOT_DATE = '2026-11-07'") if 'SNAPSHOT_DATE =' not in text else text
    text = text.replace('date.today().isoformat()', 'SNAPSHOT_DATE')
    text = text.replace('Earlier design documents remain unchanged and may describe previous proposals.',
                        'Method names reflect the current controllers, service interfaces and private helpers. Entity relationships are unchanged by the naming refactor.')
    # Name controller entry points as well as the service messages.
    notes = {
        'C->>S: CreatePricingExceptionRequestAsync': 'CreatePricingExceptionRequestAsync',
        'C->>S: ReviewPricingExceptionRequestAsync': 'RecordPricingExceptionDecisionAsync',
        'C->>S: SubmitRequestRevisionAsync': 'SubmitPricingExceptionRevisionAsync',
        'C->>S: GetAsync(applicationId': 'GetApprovedDiscountForMortgageApplicationAsync',
        'C->>S: GetRequestHistorySnapshotAsync': 'GetPricingExceptionHistoryAsync',
        'C->>S: GetUserOrThrowAsync': 'GetCurrentUserProfileAsync',
        'C->>S: GetReviewersAsync': 'GetReviewersAsync',
    }
    for marker, action in notes.items():
        lines = text.splitlines()
        updated = []
        for line in lines:
            if marker in line and (not updated or action not in updated[-1]):
                updated.append('    Note over C: ' + action)
            updated.append(line)
        text = '\n'.join(updated) + '\n'
    text = text.replace('HistoryTimeline.tsx', 'RequestHistory.tsx')
    generator.write_text(text, encoding='utf-8')

    groups = {}
    for relative in ['Controllers/PricingExceptionsController.cs', 'Controllers/UsersController.cs',
                     'Controllers/ApprovedDiscountsController.cs', 'Services/PricingExceptionService.cs',
                     'Services/UserService.cs', 'Services/ApprovedDiscountService.cs']:
        source = (API / relative).read_text(encoding='utf-8-sig')
        groups[Path(relative).stem] = re.findall(
            r'^\s*(public|private)\s+(?:static\s+)?(?:async\s+)?[\w<>?,]+\s+(\w+)(?:<\w+>)?\(', source, re.M)
    content = ['# Current controller and service methods', '', 'Code reference snapshot: 7 November 2026.', '',
               'This is a naming and organization refactor. HTTP routes, DTOs, schema, authorization, retry scopes and workflow behavior are unchanged.', '',
               '`ApprovedDiscountService.GetAsync` is intentionally unchanged. It is distinct from the renamed pricing-exception request read method.', '']
    for name, methods in groups.items():
        content += ['## ' + name, '']
        content += ['- `' + method + '` (' + visibility + ')' for visibility, method in methods]
        content += ['']
    content += ['## Entity model', '',
                'The six persisted entities remain User, PricingExceptionRequest, RequestRevision, ReviewDecision, HistoryEntry and IdempotencyRecord. Method renaming does not add or rename any entity, field, table, key or relationship.', '',
                'The ER diagrams show persisted data. Controller and service methods belong in this method reference and the sequence diagrams, rather than inside entity boxes.', '']
    (DOCS / 'CurrentImplementation/MethodReference.md').write_text('\n'.join(content), encoding='utf-8')
    print('Updated generator and source-derived method reference.')

if __name__ == '__main__':
    prepare()
