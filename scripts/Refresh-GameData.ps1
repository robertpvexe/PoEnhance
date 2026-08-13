<#
.SYNOPSIS
Builds and validates a non-activating PoEnhance GameData candidate from explicit RePoE snapshots.

.DESCRIPTION
Invokes the existing PoEnhance.DataTool build-package pipeline with explicit current and
historical source metadata, retains an atomic source snapshot, runs focused compatibility
checks, and writes a candidate plus readiness and SHA-256 manifests. When an active package
exists it is hashed before and after; a missing active package is permitted so the tracked
bootstrap workflow can run from a fresh clone. This script never activates its candidate.

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
    [Parameter(Mandatory)] [string]$PoBSourceRoot,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string]$PoBSourceCommit,
    [Parameter(Mandatory)] [string]$PoBSourceTag,
    [Parameter(Mandatory)] [string]$OutputDirectory,
    [string]$CreatedAtUtc,
    [string]$SourceUri = 'https://github.com/repoe-fork/repoe',
    [string]$SourceBranch = 'master',
    [string]$HistoricalSourceUri = 'https://github.com/repoe-fork/repoe',
    [string]$HistoricalSourceBranch = 'historical-snapshot',
    [string]$PoBSourceUri = 'https://github.com/PathOfBuildingCommunity/PathOfBuilding',
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
$pobRootPath = [System.IO.Path]::GetFullPath($PoBSourceRoot).TrimEnd('\')

if ($outputRoot.Equals($artifactsDirectory, [StringComparison]::OrdinalIgnoreCase))
{
    throw 'OutputDirectory must not be the active repository artifacts directory.'
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
Assert-Directory $pobRootPath 'Path of Building source root'
Assert-ExactGitCommit $sourceRootPath $SourceCommit 'Current source'
Assert-ExactGitCommit $historicalRootPath $HistoricalSourceCommit 'Historical source'
Assert-ExactGitCommit $pobRootPath $PoBSourceCommit 'Path of Building source'
$pobTagCommit = (& git -C $pobRootPath rev-list -n 1 $PoBSourceTag 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or -not $pobTagCommit.Equals($PoBSourceCommit, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Path of Building tag $PoBSourceTag does not resolve to $PoBSourceCommit."
}

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
$pobEvaluatedPath = Join-Path $outputRoot 'pob-uniques.evaluated.json'

foreach ($path in @($candidatePath, $buildLogPath, $readinessPath, $readinessMarkdownPath, $shaManifestPath, $snapshotDirectory, $pobEvaluatedPath))
{
    Assert-DirectOutputChild $path
    if (Test-Path -LiteralPath $path)
    {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

$activeExistedBefore = Test-Path -LiteralPath $activeArtifact -PathType Leaf
$activeHashBefore = if ($activeExistedBefore)
{
    (Get-FileHash -Algorithm SHA256 -LiteralPath $activeArtifact).Hash
}
else
{
    $null
}
$pobRuntime = Join-Path $pobRootPath 'runtime\Path{space}of{space}Building.exe'
$pobSourceDirectory = Join-Path $pobRootPath 'src'
if (-not (Test-Path -LiteralPath $pobRuntime -PathType Leaf))
{
    throw "Pinned Path of Building checkout lacks its bundled runtime: $pobRuntime"
}
$pobLaunchPath = Join-Path $pobSourceDirectory "PoEnhanceUniqueExtract-$PID.lua"
if (Test-Path -LiteralPath $pobLaunchPath)
{
    throw "Refusing to overwrite an existing Path of Building extraction script: $pobLaunchPath"
}
try
{
    [System.IO.File]::Copy(
        (Join-Path $repoRoot 'scripts\Extract-PoBUniqueCatalog.lua'),
        $pobLaunchPath,
        $false)
    $previousOutput = $env:POENHANCE_POB_UNIQUE_OUTPUT
    $env:POENHANCE_POB_UNIQUE_OUTPUT = $pobEvaluatedPath
    try
    {
        $pobProcess = Start-Process -FilePath $pobRuntime -ArgumentList $pobLaunchPath `
            -WorkingDirectory $pobSourceDirectory -WindowStyle Hidden -Wait -PassThru
    }
    finally
    {
        $env:POENHANCE_POB_UNIQUE_OUTPUT = $previousOutput
    }
}
finally
{
    if (Test-Path -LiteralPath $pobLaunchPath)
    {
        [System.IO.File]::Delete($pobLaunchPath)
    }
}
if ($pobProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $pobEvaluatedPath -PathType Leaf))
{
    throw "Path of Building Unique extraction failed with exit code $($pobProcess.ExitCode)."
}
$pobEvaluation = Get-Content -LiteralPath $pobEvaluatedPath -Raw | ConvertFrom-Json
$pobEvaluationErrorProperty = $pobEvaluation.PSObject.Properties['error']
$pobEvaluationError = if ($null -eq $pobEvaluationErrorProperty) { $null } else { $pobEvaluationErrorProperty.Value }
if ($pobEvaluationError -or $pobEvaluation.entries.Count -eq 0)
{
    throw "Path of Building Unique extraction did not produce evaluated entries: $pobEvaluationError"
}
$pobFoulbornMapPath = Require-InputFile (Join-Path $pobRootPath 'src\Data') 'ModFoulbornMap.jsonc' 'Path of Building Foulborn relationships'

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
    '--historical-data-version', $HistoricalDataVersion,
    '--pob-uniques', $pobEvaluatedPath,
    '--pob-foulborn-map', $pobFoulbornMapPath,
    '--pob-source-root', $pobRootPath,
    '--pob-source-uri', $PoBSourceUri,
    '--pob-source-tag', $PoBSourceTag,
    '--pob-source-version', $PoBSourceCommit
)
if (-not [string]::IsNullOrWhiteSpace($League)) { $dataToolArguments += @('--league', $League) }
if (-not [string]::IsNullOrWhiteSpace($Patch)) { $dataToolArguments += @('--patch', $Patch) }
if (-not [string]::IsNullOrWhiteSpace($CreatedAtUtc))
{
    $parsedCreatedAtUtc = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse(
            $CreatedAtUtc,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal,
            [ref]$parsedCreatedAtUtc))
    {
        throw "CreatedAtUtc is invalid: $CreatedAtUtc"
    }
    $dataToolArguments += @('--created-at-utc', $parsedCreatedAtUtc.ToUniversalTime().ToString('O'))
}

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

$activeExistsAfter = Test-Path -LiteralPath $activeArtifact -PathType Leaf
$activeHashAfter = if ($activeExistsAfter)
{
    (Get-FileHash -Algorithm SHA256 -LiteralPath $activeArtifact).Hash
}
else
{
    $null
}
if ($activeExistedBefore -ne $activeExistsAfter -or $activeHashBefore -ne $activeHashAfter)
{
    throw 'The active GameData artifact changed during refresh; candidate is not ready.'
}
$activeHashBeforeNormalized = if ($null -eq $activeHashBefore) { $null } else { $activeHashBefore.ToLowerInvariant() }
$activeHashAfterNormalized = if ($null -eq $activeHashAfter) { $null } else { $activeHashAfter.ToLowerInvariant() }
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
    pathOfBuildingSource = [ordered]@{
        repositoryUri = $PoBSourceUri
        tag = $PoBSourceTag
        commitSha = $PoBSourceCommit.ToLowerInvariant()
        evaluatedEntries = $pobEvaluation.entries.Count
        evaluatedPath = $pobEvaluatedPath
        evaluatedSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $pobEvaluatedPath).Hash.ToLowerInvariant()
        foulbornRelationshipPath = $pobFoulbornMapPath
        foulbornRelationshipSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $pobFoulbornMapPath).Hash.ToLowerInvariant()
    }
    sourceSnapshotManifest = $snapshotManifestPath
    compatibilityChecks = $compatibilityChecks
    activeArtifact = [ordered]@{
        path = $activeArtifact
        existedBefore = $activeExistedBefore
        existsAfter = $activeExistsAfter
        sha256Before = $activeHashBeforeNormalized
        sha256After = $activeHashAfterNormalized
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
- Path of Building: ``$PoBSourceTag`` / ``$PoBSourceCommit`` ($($pobEvaluation.entries.Count) evaluated entries)
- Source snapshot: ``$snapshotManifestPath``
- Active artifact existed before/after: ``$activeExistedBefore`` / ``$activeExistsAfter``
- Active artifact SHA-256 before/after: ``$($readiness.activeArtifact.sha256Before)`` / ``$($readiness.activeArtifact.sha256After)``
- Active artifact modified: **no**
- Candidate activated: **no**
"@
[System.IO.File]::WriteAllText($readinessMarkdownPath, $markdown, [System.Text.UTF8Encoding]::new($false))

$hashFiles = @($candidatePath, $pobEvaluatedPath, $snapshotManifestPath, $buildLogPath, $readinessPath, $readinessMarkdownPath)
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
