"""Generate the concise, step-by-step local Docker Word guide."""
from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

root = Path(__file__).resolve().parents[1]
output = root / 'docs' / 'BlueHarvest_Docker_Clear_Instructions.docx'
d = Document()
sec = d.sections[0]
sec.page_width, sec.page_height = Inches(8.27), Inches(11.69)
sec.top_margin = sec.bottom_margin = Inches(.6)
sec.left_margin = sec.right_margin = Inches(.65)
style = d.styles['Normal']
style.font.name, style.font.size = 'Aptos', Pt(11)
style.paragraph_format.space_after = Pt(7)
for name, size in [('Title', 27), ('Heading 1', 20), ('Heading 2', 14)]:
    d.styles[name].font.name = 'Aptos Display'
    d.styles[name].font.size = Pt(size)
    d.styles[name].font.color.rgb = RGBColor.from_string('007E87')
sec.header.paragraphs[0].text = 'BLUE HARVEST | LOCAL DOCKER INSTRUCTIONS'
footer = sec.footer.paragraphs[0]
footer.text = 'Developer handoff - 6 November 2026 | Page '
field = OxmlElement('w:fldSimple')
field.set(qn('w:instr'), 'PAGE')
footer._p.append(field)
d.core_properties.title = 'Blue Harvest - Clear Docker Instructions'

def p(value):
    d.add_paragraph(value)

def heading(value):
    d.add_heading(value, level=2)

def command(value):
    paragraph = d.add_paragraph()
    shade = OxmlElement('w:shd')
    shade.set(qn('w:fill'), 'EDF2F7')
    paragraph._p.get_or_add_pPr().append(shade)
    paragraph.paragraph_format.keep_together = True
    run = paragraph.add_run(value)
    run.font.name, run.font.size = 'Consolas', Pt(9)

def expected(value):
    paragraph = d.add_paragraph()
    paragraph.add_run('Expected result: ').bold = True
    paragraph.add_run(value)

def page(title):
    d.add_page_break()
    d.add_heading(title, level=1)

d.add_heading('Run Blue Harvest locally\nwith Docker', 0)
p('A five-page guide for reviewing developers. Follow Steps 1–6 to start the application; use the remaining steps to demonstrate, test, and stop it.')
p('Terminal: Windows PowerShell. Application: React frontend, ASP.NET Core API, PostgreSQL database. No production sign-in is configured.')
heading('Step 1 — Install and start Docker Desktop')
p('Install Docker Desktop using your organisation’s approved installation process. Start it, select Linux containers, and wait until the Docker engine is running.')
command('docker version\ndocker compose version\ndocker info')
expected('Docker shows client and server information. Compose reports a version. There is no daemon connection error.')
p('Docker is sufficient to run the application. Install Git to clone it. Install the .NET 10 SDK only if you will run the backend tests in Step 8. Host Node.js and PostgreSQL installations are not needed for Compose.')
heading('Step 2 — Obtain the complete repository')
p('Open PowerShell in the directory where you want to keep the project. Run:')
command('git clone https://github.com/FerdinandGang4/Mortgage-pricing-exception-requests-Application.git\ncd Mortgage-pricing-exception-requests-Application')
p('Alternative: download the repository ZIP, extract all files, and open PowerShell in its extracted root. If access is denied, ask Ferdinand Dinga for repository access.')
heading('Step 3 — Confirm the working folder')
command('Test-Path ./compose.yaml\nTest-Path ./Dockerfile\nTest-Path ./FrontEnd\nTest-Path ./Backend')
expected('All four commands return True. This folder is called the repository root throughout the guide.')

page('Start and check the application')
heading('Step 4 — Build and start the containers')
p('Working directory: repository root, containing compose.yaml.')
command('docker compose up --build -d\ndocker compose ps')
expected('frontend and backend show Up. postgres shows Up (healthy). The first build may take several minutes while images and packages download.')
p('Compose applies the database migrations and seeds three managers, five reviewers, and the Mortgage Process user. It uses a persistent PostgreSQL volume.')
heading('Step 5 — Confirm API readiness')
command('docker compose logs --tail 50 backend')
expected('The backend reports that it is listening and the application has started. PostgreSQL being healthy does not by itself confirm that the API has finished startup.')
p('Then check the API through the frontend proxy:')
command("$headers = @{'X-User-Id'='11111111-1111-1111-1111-111111111111'}\nInvoke-RestMethod http://localhost:5173/api/v1/users/me -Headers $headers")
expected('The response identifies James Wilson (Manager), with role RelationshipManager.')
heading('Step 6 — Open the application')
p('Frontend: http://localhost:5173\nBackend Swagger: http://localhost:5267/swagger')
expected('The frontend shows the pricing exceptions workspace and Select Demo User. Swagger shows the API operations.')
p('If you previously ran host development servers, stop those using ports 5173 or 5267 before starting Compose. The database also publishes port 5432. Do not start a second Vite or ASP.NET Core server for this Docker walkthrough.')

page('Demonstrate the assignment workflow')
heading('Step 7 — Create, decide, and inspect history')
p('1. Select James Wilson (Manager) in Select Demo User. Click New request.')
p('2. Generate a fresh application ID in PowerShell:')
command('[guid]::NewGuid().ToString()')
p('3. Paste that ID, request 50 basis points, enter a supporting reason, and assign David Miller (Reviewer). Submit the request.')
expected('The request is Pending and its details identify James as requester and David as assigned reviewer.')
p('4. Create two more requests with different application IDs. Assign Emily Davis and Robert Taylor respectively.')
p('5. Switch to David, select his request, choose Review request, enter a note, and approve it. Repeat as Emily and Robert; decline one request to demonstrate both outcomes.')
expected('Only the assigned reviewer can approve or decline each request. Managers cannot approve. History retains the requester, reviewer, decision, reason, and timestamp.')
p('6. Select Mortgage process, open Approved discounts, and look up an approved application ID.')
expected('The currently approved discount is returned.')
p('7. Switch back to James, open the approved request, and submit revised terms. Look up its discount again as Mortgage process.')
expected('The request returns to Pending. The old approval remains in history but no longer provides an effective discount until the new revision is approved.')
heading('Important demo rules')
p('Use a new GUID for each new mortgage application. One request per application is allowed; subsequent proposals use revisions. Application IDs are external references—the demo does not verify that a real mortgage application exists.')
p('Use synthetic data. The user picker is development-only identity simulation. A fresh browser may show an empty workspace; use Open request to browse records already in PostgreSQL.')

page('Run the validation checks')
heading('Step 8 — Run backend tests')
p('Prerequisite: .NET 10 SDK installed on the host. Open a new terminal at the repository root, then run:')
command('cd Backend/BlueHarvest.PricingExceptions\ndotnet test')
expected('All tests pass with zero failures. Previously recorded results: 69 passing tests (24 unit and 45 HTTP integration). Record your own run results.')
p('These tests use isolated InMemory stores; they do not test the running PostgreSQL container. Return to the repository root:')
command('cd ../..')
heading('Step 9 — Test the live Docker application')
p('Working directory: repository root. Compose must be running. Run this single command; backticks continue the command onto the next line:')
command('powershell.exe -NoProfile -ExecutionPolicy Bypass `\n  -File Backend/BlueHarvest.PricingExceptions/tests/Invoke-ApiSmokeTests.ps1 `\n  -BaseUrl http://localhost:5173')
expected('The script prints “HTTP smoke tests passed”. It checks creation, retries, conflicts, approval, discount lookup, revisions, history, validation, and access rules.')
p('This creates synthetic records in PostgreSQL. Do not include trailing spaces after a PowerShell continuation backtick.')
heading('Optional — Confirm data survives container recreation')
command('powershell.exe -NoProfile -ExecutionPolicy Bypass `\n  -File Backend/BlueHarvest.PricingExceptions/tests/Test-DockerPersistence.ps1 `\n  -BaseUrl http://localhost:5173')
expected('The script prints “PostgreSQL persistence passed”. It restarts the deployment and verifies retained request, decision, history, and retry records.')
p('Additional browser tests, EF migration commands, and configuration details are in the repository-root DOCKER.md. Tests were not rerun merely to generate this document.')

page('Stop, restart, and resolve common errors')
heading('Step 10 — Stop or start again')
p('Working directory: repository root. Choose the command you need:')
table = d.add_table(rows=1, cols=2)
table.style = 'Light Shading Accent 1'
table.rows[0].cells[0].text = 'Task'
table.rows[0].cells[1].text = 'Command'
for task, cmd in [
    ('Stop all application containers', 'docker compose stop'),
    ('Start again; preserve database data', 'docker compose up -d'),
    ('Restart processes without rebuilding', 'docker compose restart'),
    ('Rebuild after source changes', 'docker compose up --build -d'),
    ('Inspect stopped and running containers', 'docker compose ps -a'),
    ('Remove containers, retain the data volume', 'docker compose down'),
]:
    cells = table.add_row().cells
    cells[0].text, cells[1].text = task, cmd
p('Do not use docker compose down -v for normal shutdown: it deletes the database volume. A restart does not rebuild changed source code.')
heading('Troubleshooting')
for value in [
    'Docker connection error: start Docker Desktop and confirm Linux containers; retry docker info.',
    'No configuration file: change into the directory containing compose.yaml.',
    'Port already allocated: stop the conflicting service or use the .env port settings documented in DOCKER.md.',
    '502 or API offline: inspect backend startup/migration logs; wait for the listening message and refresh.',
    '401: verify the Development environment and seeded user ID. 403: select the assigned reviewer.',
    '409: check for an existing application request, stale version, or conflicting retry key; reload the current request.',
    'Database password error: align .env and the stored database password. Changing .env does not reset an existing database password.',
    'Old frontend visible: rebuild the containers and hard-refresh the browser.',
]:
    d.add_paragraph(value, style='List Bullet')
command('docker compose logs --tail 100 backend postgres frontend')
heading('Assignment handoff')
p('Share the intended Git commit and confirm reviewer access. Include actual development time, your test results, and the documented assumptions. This is a local demo; production needs trusted identity, mortgage-system validation, stronger multi-instance guarantees, and operational controls.')

d.save(output)
print(f'Created {output.name}; {len(d.paragraphs)} paragraphs, {len(d.tables)} table.')
