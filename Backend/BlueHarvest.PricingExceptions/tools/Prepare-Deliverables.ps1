$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$destinationRoot = Join-Path $repoRoot 'Deliverables'
& (Join-Path $PSScriptRoot 'Export-AssumptionsDocument.ps1')
$copies = @{
    'Documentation/Assumptions.docx' = 'Documents/Assumptions.docx'
    'Documentation/Blue Harvest Agentic SDLC Engineering Assignment.pdf' = 'Documents/Blue Harvest Agentic SDLC Engineering Assignment.pdf'
    'Backend/BlueHarvest.PricingExceptions/docs/Architecture/Assumptions.md' = 'Documents/Assumptions.md'
    'Backend/BlueHarvest.PricingExceptions/docs/Architecture/Blue_Harvest_Mortgage_Pricing_Exception_Design.docx' = 'Documents/Blue_Harvest_Mortgage_Pricing_Exception_Design.docx'
    'Backend/BlueHarvest.PricingExceptions/docs/EntityClassDiagram/BlueHarvest_Entity_Class_Diagram_and_Relationships.docx' = 'Documents/BlueHarvest_Entity_Class_Diagram_and_Relationships.docx'
    'Backend/BlueHarvest.PricingExceptions/docs/SequenceDiagrams/BlueHarvest_PlantUML_Detailed_Sequence_Diagrams.docx' = 'Documents/BlueHarvest_PlantUML_Detailed_Sequence_Diagrams.docx'
    'Backend/BlueHarvest.PricingExceptions/docs/Architecture/SubmissionNotes.md' = 'Reports/SubmissionNotes.md'
    'Backend/BlueHarvest.PricingExceptions/docs/Architecture/ValidationResults.md' = 'Reports/ValidationResults.md'
    'Backend/BlueHarvest.PricingExceptions/docs/Architecture/AiDevelopmentLog.md' = 'Reports/AiDevelopmentLog.md'
}
foreach ($source in $copies.Keys) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $source))) {
        throw "Missing deliverable source: $source"
    }
}
foreach ($source in $copies.Keys) {
    $target = Join-Path $destinationRoot $copies[$source]
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    Copy-Item -LiteralPath (Join-Path $repoRoot $source) -Destination $target -Force
}
Write-Output "Refreshed $($copies.Count) document/report copies in $destinationRoot"
Write-Output 'Source code and automated tests remain in Backend/BlueHarvest.PricingExceptions.'
