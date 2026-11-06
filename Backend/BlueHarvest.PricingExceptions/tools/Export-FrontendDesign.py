"""Export FrontEnd/Design/Frontend_Design.md and its rendered diagrams to Word."""
from pathlib import Path
import re
import struct
import zipfile
from xml.etree import ElementTree
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

repo = Path(__file__).resolve().parents[3]
source = repo / 'FrontEnd' / 'Design' / 'Frontend_Design.md'
output = repo / 'Backend' / 'BlueHarvest.PricingExceptions' / 'docs' / 'BlueHarvest_Frontend_Design.docx'
d = Document()
sec = d.sections[0]
sec.page_width, sec.page_height = Inches(8.27), Inches(11.69)
sec.top_margin = sec.bottom_margin = Inches(.5)
sec.left_margin = sec.right_margin = Inches(.6)
normal = d.styles['Normal']
normal.font.name, normal.font.size = 'Aptos', Pt(10)
normal.paragraph_format.space_after = Pt(6)
normal.paragraph_format.line_spacing = 1.05
for name, size in [('Title', 26), ('Heading 1', 17), ('Heading 2', 12)]:
    d.styles[name].font.name = 'Aptos Display'
    d.styles[name].font.size = Pt(size)
    d.styles[name].font.color.rgb = RGBColor.from_string('007E87')
sec.header.paragraphs[0].text = 'BLUE HARVEST | FRONTEND DESIGN'
footer = sec.footer.paragraphs[0]
footer.text = 'Current implementation | 6 November 2026 | Page '
field = OxmlElement('w:fldSimple')
field.set(qn('w:instr'), 'PAGE')
footer._p.append(field)
d.core_properties.title = 'Blue Harvest - Frontend Design'
d.core_properties.subject = 'Component structure, responsibilities, data flow, state management and responsive behaviour'

lines = source.read_text(encoding='utf-8').splitlines()
i = 0
while i < len(lines):
    line = lines[i]
    if not line.strip():
        i += 1
        continue
    if line == '<!-- pagebreak -->':
        d.add_page_break()
    elif line.startswith('# '):
        d.add_heading(line[2:], 0)
    elif line.startswith('## '):
        d.add_heading(line[3:], 1)
    elif line.startswith('!['):
        name = re.search(r'\]\(([^)]+)\)', line).group(1)
        image = source.parent / name
        w, h = struct.unpack('>II', image.read_bytes()[16:24])
        scale = min(7.0 / w, 3.6 / h)
        d.add_picture(str(image), width=Inches(w*scale), height=Inches(h*scale))
    elif line.startswith('|'):
        rows = []
        while i < len(lines) and lines[i].startswith('|'):
            row = [v.strip() for v in lines[i].strip('|').split('|')]
            if not all(re.fullmatch(r':?-+:?', value) for value in row):
                rows.append(row)
            i += 1
        table = d.add_table(rows=0, cols=len(rows[0]))
        table.style = 'Light Shading Accent 1'
        for n, row in enumerate(rows):
            target = table.add_row()
            properties = target._tr.get_or_add_trPr()
            properties.append(OxmlElement('w:cantSplit'))
            if n == 0:
                properties.append(OxmlElement('w:tblHeader'))
            for cell, value in zip(target.cells, row):
                cell.text = value
                for paragraph in cell.paragraphs:
                    paragraph.paragraph_format.space_after = Pt(4)
                    for run in paragraph.runs:
                        run.font.size = Pt(9)
                        run.bold = n == 0
        d.add_paragraph()
        continue
    elif line.startswith('- '):
        d.add_paragraph(line[2:], style='List Bullet')
    else:
        d.add_paragraph(line)
    i += 1

d.save(output)
with zipfile.ZipFile(output) as archive:
    ElementTree.fromstring(archive.read('word/document.xml'))
    images = [name for name in archive.namelist() if name.startswith('word/media/')]
    assert len(images) == 2
for heading in ('1. Component diagram', '2. Component responsibilities', '3. Data flow', '4. State management', '5. Responsive behaviour'):
    assert any(paragraph.text == heading for paragraph in d.paragraphs)
print(f'Created {output.name}: all five requested headings, two diagrams, {len(d.tables)} tables.')
