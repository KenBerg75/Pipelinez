param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactsRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$RepoFullName,

    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [Parameter(Mandatory = $true)]
    [string]$RunAttempt
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-SuiteTitle {
    param([string]$Suite)

    switch ($Suite) {
        "in-memory" { return "In-Memory" }
        "kafka" { return "Kafka" }
        "rabbitmq" { return "RabbitMQ" }
        "amazon-s3" { return "Amazon S3" }
        "postgresql" { return "PostgreSQL" }
        "sqlserver" { return "SQL Server" }
        "azure-service-bus" { return "Azure Service Bus" }
        default { return ($Suite -replace "-", " ") }
    }
}

function Read-Metadata {
    param([string]$MetadataPath)

    if (-not (Test-Path -LiteralPath $MetadataPath)) {
        return $null
    }

    return Get-Content -LiteralPath $MetadataPath -Raw | ConvertFrom-Json
}

function Add-Line {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Text = ""
    )

    $Lines.Add($Text) | Out-Null
}

$resolvedArtifactsRoot = (Resolve-Path -LiteralPath $ArtifactsRoot).Path
$suiteDirectories = Get-ChildItem -LiteralPath $resolvedArtifactsRoot -Directory | Sort-Object Name

$suiteData = @(
foreach ($suiteDirectory in $suiteDirectories) {
    $metadata = Read-Metadata -MetadataPath (Join-Path $suiteDirectory.FullName "metadata.json")
    $reportFiles = @(
        Get-ChildItem -LiteralPath $suiteDirectory.FullName -Recurse -Filter "*-report-github.md" -File |
            Sort-Object Name
    )

    [pscustomobject]@{
        SuiteName   = $suiteDirectory.Name -replace "^benchmarks-", ""
        Metadata    = $metadata
        ReportFiles = $reportFiles
    }
}
)

if ($suiteData.Count -eq 0) {
    throw "No benchmark artifacts were found under '$resolvedArtifactsRoot'."
}

$primaryMetadata = $suiteData |
    Where-Object { $_.Metadata } |
    Select-Object -First 1 -ExpandProperty Metadata

$runUrl = "https://github.com/$RepoFullName/actions/runs/$RunId"
$generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$commit = if ($primaryMetadata) { [string]$primaryMetadata.commit } else { "unknown" }
$eventName = if ($primaryMetadata) { [string]$primaryMetadata.event } else { "unknown" }
$runnerOs = if ($primaryMetadata) { [string]$primaryMetadata.runner_os } else { "unknown" }
$executedAtUtc = if ($primaryMetadata) { [string]$primaryMetadata.executed_at_utc } else { $generatedAtUtc }
$enabledSuites = $suiteData | ForEach-Object { Get-SuiteTitle $_.SuiteName }

$lines = New-Object 'System.Collections.Generic.List[string]'

Add-Line -Lines $lines -Text "# Benchmark Results"
Add-Line -Lines $lines
Add-Line -Lines $lines -Text "This page is generated from the latest benchmark workflow artifacts."
Add-Line -Lines $lines
Add-Line -Lines $lines -Text "## Latest Run"
Add-Line -Lines $lines
Add-Line -Lines $lines -Text "- Run date: $executedAtUtc"
Add-Line -Lines $lines -Text "- Workflow run: [Run #$RunId (attempt $RunAttempt)]($runUrl)"
Add-Line -Lines $lines -Text "- Commit: ``$commit``"
Add-Line -Lines $lines -Text "- Trigger: ``$eventName``"
Add-Line -Lines $lines -Text "- Runner OS: ``$runnerOs``"
Add-Line -Lines $lines -Text "- Enabled suites: $($enabledSuites -join ', ')"
Add-Line -Lines $lines
Add-Line -Lines $lines -Text "## Caveats"
Add-Line -Lines $lines
Add-Line -Lines $lines -Text "- Benchmark numbers are for comparative regression tracking, not universal throughput guarantees."
Add-Line -Lines $lines -Text "- Cross-machine and cross-run variance is expected, especially for transport-backed I/O."
Add-Line -Lines $lines -Text "- Raw BenchmarkDotNet artifacts remain attached to the workflow run for deeper inspection."
Add-Line -Lines $lines
Add-Line -Lines $lines -Text "## Suites"
Add-Line -Lines $lines

foreach ($suite in $suiteData) {
    $suiteTitle = Get-SuiteTitle $suite.SuiteName
    Add-Line -Lines $lines -Text "### $suiteTitle"
    Add-Line -Lines $lines

    if ($suite.Metadata) {
        Add-Line -Lines $lines -Text "- Executed at: $($suite.Metadata.executed_at_utc)"
        if ($suite.Metadata.PSObject.Properties.Name -contains "filter" -and $suite.Metadata.filter) {
            Add-Line -Lines $lines -Text "- Filter: ``$($suite.Metadata.filter)``"
        }
        Add-Line -Lines $lines -Text "- Runner OS: ``$($suite.Metadata.runner_os)``"
        Add-Line -Lines $lines
    }

    if ($suite.ReportFiles.Count -eq 0) {
        Add-Line -Lines $lines -Text "_No BenchmarkDotNet markdown report was found for this suite._"
        Add-Line -Lines $lines
        continue
    }

    foreach ($reportFile in $suite.ReportFiles) {
        $reportTitle = [System.IO.Path]::GetFileNameWithoutExtension($reportFile.Name) -replace "-report-github$", ""
        Add-Line -Lines $lines -Text "#### $reportTitle"
        Add-Line -Lines $lines

        $reportContent = Get-Content -LiteralPath $reportFile.FullName
        foreach ($line in $reportContent) {
            Add-Line -Lines $lines -Text $line
        }

        Add-Line -Lines $lines
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

[System.IO.File]::WriteAllText(
    (Resolve-Path -LiteralPath $outputDirectory).Path + [System.IO.Path]::DirectorySeparatorChar + (Split-Path -Leaf $OutputPath),
    ($lines -join [Environment]::NewLine) + [Environment]::NewLine,
    [System.Text.Encoding]::UTF8)
