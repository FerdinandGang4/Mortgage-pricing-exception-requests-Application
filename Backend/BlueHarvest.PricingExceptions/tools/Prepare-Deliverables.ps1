$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$docsRoot = Join-Path $repoRoot 'Backend/BlueHarvest.PricingExceptions/docs'
& (Join-Path $PSScriptRoot 'Export-AssumptionsDocument.ps1')
$required = @(
    'BlueHarvest_Current_Entity_Relationships_Final.docx',
    'BlueHarvest_Current_Entity_Relationships.pdf',
    'BlueHarvest_Current_Sequence_Diagrams_Final.docx',
    'BlueHarvest_Current_Sequence_Diagrams.pdf',
    'Assumptions.docx',
    'BlueHarvest_Assignment_Presentation.pptx',
    'BlueHarvest_Assignment_Presentation.pdf',
    'BlueHarvest_Docker_Clear_Instructions.docx'
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $docsRoot $name) -PathType Leaf)) {
        throw "Missing current deliverable: $name"
    }
}
Write-Output "Verified $($required.Count) current document files in $docsRoot"
Write-Output 'Deliverables/README.md links to this single source of truth; no duplicate copies are created.'
