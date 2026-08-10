<#
.SYNOPSIS
Builds and validates a non-activating PoEnhance GameData candidate from explicit RePoE snapshots.

.DESCRIPTION
Invokes the existing PoEnhance.DataTool build-package pipeline with explicit current and
historical source metadata, retains an atomic source snapshot, runs focused compatibility
checks, and writes a candidate plus readiness and SHA-256 manifests. The active package is
hashed before and after and is never copied, replaced, or activated by this script.

.PARAMETER SourceRoot
Exact current RePoE Git checkout used only to verify SourceCommit and retain provenance.

.PARAMETER SourceDataRoot
Directory containing current base_items.json, mods.json, stats.json,
stat_translations.json, item_classes.json, tags.json, and mods_by_base.json.

.PARAMETER HistoricalSourceRoot
Exact historical RePoE Git checkout used only to verify HistoricalSourceCommit.

.PARAMETER HistoricalSourceDataRoot
Directory containing historical base_items.json, mods.json, stats.json, and
stat_translations.json.

.PARAMETER OutputDirectory
Deterministic candidate output directory. It must not be the repository artifacts directory.

.EXAMPLE
./scripts/Refresh-GameData.ps1 `
  -SourceRoot C:\src\repoe-current `
  -SourceDataRoot C:\exports\repoe-3.29.1.2.2 `
  -SourceCommit 34a9bd548eba7c3b62ab1d1f19a99ae8b12f1564 `
  -DataVersion 3.29.1.2.2 `
  -HistoricalSourceRoot C:\src\repoe-historical `
  -HistoricalSourceDataRoot C:\exports\repoe-3.28.0.13 `
  -HistoricalSourceCommit c50acab2ed660a70511e7f91ee09db4e632089e4 `
  -HistoricalDataVersion 3.28.0.13 `
  -OutputDirectory "$env:TEMP\PoEnhance-GameData-Candidate"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SourceRoot,
    [Parameter(Mandatory)] [string]$SourceDataRoot,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string]$SourceCommit,
    [Parameter(Mandatory)] [string]$DataVersion,
    [Parameter(Mandatory)] [string]$HistoricalSourceRoot,
    [Parameter(Mandatory)] [string]$HistoricalSourceDataRoot,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string]$HistoricalSourceCommit,
    [Parameter(Mandatory)] [string]$HistoricalDataVersion,
    [Parameter(Mandatory)] [string]$OutputDirectory,
    [string]$SourceUri = 'https://github.com/repoe-fork/repoe',
    [string]$SourceBranch = 'master',
    [string]$HistoricalSourceUri = 'https://github.com/repoe-fork/repoe',
    [string]$HistoricalSourceBranch = 'historical-snapshot',
    [string]$League,
    [string]$Patch,
    [switch]$SkipCompatibilityTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$activeArtifact = Join-Path $repoRoot 'artifacts\poenhance-game-data.json'
$artifactsDirectory = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')).TrimEnd('\')
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
$sourceRootPath = [System.IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$sourceDataRootPath = [System.IO.Path]::GetFullPath($SourceDataRoot).TrimEnd('\')
$historicalRootPath = [System.IO.Path]::GetFullPath($HistoricalSourceRoot).TrimEnd('\')
$historicalDataRootPath = [System.IO.Path]::GetFullPath($HistoricalSourceDataRoot).TrimEnd('\')

if ($outputRoot.Equals($artifactsDirectory, [StringComparison]::OrdinalIgnoreCase))
{
    throw 'OutputDirectory must not be the active repository artifacts directory.'
}
if (-not (Test-Path -LiteralPath $activeArtifact -PathType Leaf))
{
    throw "Active GameData artifact is missing: $activeArtifact"
}

function Assert-Directory([string]$Path, [string]$Label)
{
    if (-not (Test-Path -LiteralPath $Path -PathType Container))
    {
        throw "$Label directory does not exist: $Path"
    }
}

function Require-InputFile([string]$Root, [string]$Name, [string]$Role)
{
    $path = Join-Path $Root $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf))
    {
        throw "Required $Role input is missing: $path"
    }
    return [System.IO.Path]::GetFullPath($path)
}

function Assert-ExactGitCommit([string]$Checkout, [string]$ExpectedCommit, [string]$Role)
{
    $actual = (& git -C $Checkout rev-parse HEAD 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $actual.Equals($ExpectedCommit, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "$Role checkout commit mismatch. Expected $ExpectedCommit; observed $actual."
    }
}

function Assert-DirectOutputChild([string]$Path)
{
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $parent = [System.IO.Path]::GetDirectoryName($fullPath).TrimEnd('\')
    if (-not $parent.Equals($outputRoot, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Refusing to replace a path outside OutputDirectory: $fullPath"
    }
}

Assert-Directory $sourceRootPath 'Current source root'
Assert-Directory $sourceDataRootPath 'Current source data root'
Assert-Directory $historicalRootPath 'Historical source root'
Assert-Directory $historicalDataRootPath 'Historical source data root'
Assert-ExactGitCommit $sourceRootPath $SourceCommit 'Current source'
Assert-ExactGitCommit $historicalRootPath $HistoricalSourceCommit 'Historical source'

$currentInputs = [ordered]@{
    baseItems = Require-InputFile $sourceDataRootPath 'base_items.json' 'current'
    mods = Require-InputFile $sourceDataRootPath 'mods.json' 'current'
    stats = Require-InputFile $sourceDataRootPath 'stats.json' 'current'
    translations = Require-InputFile $sourceDataRootPath 'stat_translations.json' 'current'
    itemClasses = Require-InputFile $sourceDataRootPath 'item_classes.json' 'current'
    tags = Require-InputFile $sourceDataRootPath 'tags.json' 'current'
    modsByBase = Require-InputFile $sourceDataRootPath 'mods_by_base.json' 'current'
}
$historicalInputs = [ordered]@{
    baseItems = Require-InputFile $historicalDataRootPath 'base_items.json' 'historical'
    mods = Require-InputFile $historicalDataRootPath 'mods.json' 'historical'
    stats = Require-InputFile $historicalDataRootPath 'stats.json' 'historical'
    translations = Require-InputFile $historicalDataRootPath 'stat_translations.json' 'historical'
}
$semanticInput = Require-InputFile (Join-Path $repoRoot 'data\semantics') 'item-property-semantics.json' 'reviewed semantics'

[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$candidateName = "poenhance-game-data-$DataVersion-candidate.json"
$candidatePath = Join-Path $outputRoot $candidateName
$snapshotDirectory = Join-Path $outputRoot 'source-snapshot'
$buildLogPath = Join-Path $outputRoot 'build.log'
$readinessPath = Join-Path $outputRoot 'refresh-readiness.json'
$readinessMarkdownPath = Join-Path $outputRoot 'refresh-readiness.md'
$shaManifestPath = Join-Path $outputRoot 'sha256-manifest.json'

foreach ($path in @($candidatePath, $buildLogPath, $readinessPath, $readinessMarkdownPath, $shaManifestPath, $snapshotDirectory))
{
    Assert-DirectOutputChild $path
    if (Test-Path -LiteralPath $path)
    {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

$activeHashBefore = (Get-FileHash -Algorithm SHA256 -LiteralPath $activeArtifact).Hash
$dataToolArguments = @(
    'run', '--project', (Join-Path $repoRoot 'PoEnhance.DataTool'),
    '--configuration', 'Release', '--no-restore', '--', 'build-package',
    '--base-items', $currentInputs.baseItems,
    '--mods', $currentInputs.mods,
    '--stats', $currentInputs.stats,
    '--translations', $currentInputs.translations,
    '--item-classes', $currentInputs.itemClasses,
    '--tags', $currentInputs.tags,
    '--mods-by-base', $currentInputs.modsByBase,
    '--item-property-semantics', $semanticInput,
    '--output', $candidatePath,
    '--source-snapshot-dir', $snapshotDirectory,
    '--source-root', $sourceRootPath,
    '--source-data-root', $sourceDataRootPath,
    '--source-uri', $SourceUri,
    '--source-branch', $SourceBranch,
    '--source-version', $SourceCommit,
    '--data-version', $DataVersion,
    '--historical-base-items', $historicalInputs.baseItems,
    '--historical-mods', $historicalInputs.mods,
    '--historical-stats', $historicalInputs.stats,
    '--historical-translations', $historicalInputs.translations,
    '--historical-source-root', $historicalRootPath,
    '--historical-source-data-root', $historicalDataRootPath,
    '--historical-source-uri', $HistoricalSourceUri,
    '--historical-source-branch', $HistoricalSourceBranch,
    '--historical-source-version', $HistoricalSourceCommit,
    '--historical-data-version', $HistoricalDataVersion
)
if (-not [string]::IsNullOrWhiteSpace($League)) { $dataToolArguments += @('--league', $League) }
if (-not [string]::IsNullOrWhiteSpace($Patch)) { $dataToolArguments += @('--patch', $Patch) }

$buildOutput = @(& dotnet @dataToolArguments 2>&1)
$buildExitCode = $LASTEXITCODE
[System.IO.File]::WriteAllLines($buildLogPath, [string[]]$buildOutput)
if ($buildExitCode -ne 0)
{
    throw "GameData build failed with exit code $buildExitCode. See $buildLogPath"
}

$compatibilityChecks = @()
if (-not $SkipCompatibilityTests)
{
    $testRuns = @(
        @('PoEnhance.GameData.Tests\PoEnhance.GameData.Tests.csproj', 'FullyQualifiedName~StatTranslationHistoryJsonTests'),
        @('PoEnhance.DataImport.Tests\PoEnhance.DataImport.Tests.csproj', 'FullyQualifiedName~StatTranslationCompatibilityClassifierTests|FullyQualifiedName~RePoeModifierImporterTests.Import_AuditedCurrentSource_ProducesExpectedCorruptedEvidence'),
        @('PoEnhance.Core.Tests\PoEnhance.Core.Tests.csproj', 'FullyQualifiedName~ModifierHistoricalTranslationRecognitionTests|FullyQualifiedName~ParsedItemModifierCandidateResolverTests.Resolve_CorruptedImplicit|FullyQualifiedName~ParsedItemModifierCandidateResolverTests.Resolve_OrdinaryImplicit_DoesNotReuseCorruptedSourceRecord'),
        @('PoEnhance.App.Tests\PoEnhance.App.Tests.csproj', 'FullyQualifiedName~PathOfExileTradeT3ProviderBlockerTests|FullyQualifiedName~PathOfExileTradePriceCheckServiceTests.ResolveProviderComponents_CorruptedImplicit')
    )
    foreach ($testRun in $testRuns)
    {
        & dotnet test (Join-Path $repoRoot $testRun[0]) --configuration Release --no-restore --filter $testRun[1] --verbosity minimal
        $testExitCode = $LASTEXITCODE
        $compatibilityChecks += [ordered]@{
            project = $testRun[0]
            filter = $testRun[1]
            exitCode = $testExitCode
            succeeded = $testExitCode -eq 0
        }
        if ($testExitCode -ne 0)
        {
            throw "Compatibility tests failed for $($testRun[0])."
        }
    }
}

$activeHashAfter = (Get-FileHash -Algorithm SHA256 -LiteralPath $activeArtifact).Hash
if ($activeHashBefore -ne $activeHashAfter)
{
    throw 'The active GameData artifact changed during refresh; candidate is not ready.'
}
$snapshotManifestPath = Join-Path $snapshotDirectory 'source-snapshot-manifest.json'
if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $snapshotManifestPath -PathType Leaf))
{
    throw 'The build did not produce both the candidate and source snapshot manifest.'
}

$readiness = [ordered]@{
    classification = 'ReadyForCompatibilityAudit'
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    candidate = [ordered]@{
        path = $candidatePath
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidatePath).Hash.ToLowerInvariant()
    }
    currentSource = [ordered]@{
        repositoryUri = $SourceUri
        branch = $SourceBranch
        commitSha = $SourceCommit.ToLowerInvariant()
        dataVersion = $DataVersion
    }
    historicalSource = [ordered]@{
        repositoryUri = $HistoricalSourceUri
        branch = $HistoricalSourceBranch
        commitSha = $HistoricalSourceCommit.ToLowerInvariant()
        dataVersion = $HistoricalDataVersion
    }
    sourceSnapshotManifest = $snapshotManifestPath
    compatibilityChecks = $compatibilityChecks
    activeArtifact = [ordered]@{
        path = $activeArtifact
        sha256Before = $activeHashBefore.ToLowerInvariant()
        sha256After = $activeHashAfter.ToLowerInvariant()
        modified = $false
    }
    activated = $false
}
[System.IO.File]::WriteAllText(
    $readinessPath,
    ($readiness | ConvertTo-Json -Depth 12),
    [System.Text.UTF8Encoding]::new($false))

$markdown = @"
# PoEnhance GameData refresh readiness

- Status: **ReadyForCompatibilityAudit**
- Candidate: ``$candidatePath``
- Candidate SHA-256: ``$($readiness.candidate.sha256)``
- Current source: ``$SourceCommit`` / ``$DataVersion``
- Historical source: ``$HistoricalSourceCommit`` / ``$HistoricalDataVersion``
- Source snapshot: ``$snapshotManifestPath``
- Active artifact SHA-256 before/after: ``$($readiness.activeArtifact.sha256Before)`` / ``$($readiness.activeArtifact.sha256After)``
- Active artifact modified: **no**
- Candidate activated: **no**
"@
[System.IO.File]::WriteAllText($readinessMarkdownPath, $markdown, [System.Text.UTF8Encoding]::new($false))

$hashFiles = @($candidatePath, $snapshotManifestPath, $buildLogPath, $readinessPath, $readinessMarkdownPath)
$shaManifest = [ordered]@{
    algorithm = 'SHA-256'
    files = @($hashFiles | ForEach-Object {
        [ordered]@{
            path = $_
            sizeBytes = (Get-Item -LiteralPath $_).Length
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash.ToLowerInvariant()
        }
    })
}
[System.IO.File]::WriteAllText(
    $shaManifestPath,
    ($shaManifest | ConvertTo-Json -Depth 6),
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Candidate: $candidatePath"
Write-Host "Readiness: $readinessMarkdownPath"
Write-Host "SHA-256 manifest: $shaManifestPath"
Write-Host 'Activation: not performed.'
