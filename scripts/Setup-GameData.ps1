<#
.SYNOPSIS
Acquires the pinned sources, reproduces the validated GameData twice, and activates it.

.DESCRIPTION
This is the explicit fresh-clone bootstrap for the ignored generated GameData artifact. It
clones pinned RePoE parser and hosted-export commits plus the pinned Path of Building tag into
deterministic cache paths under TEMP, verifies every input SHA-256, invokes the existing
Refresh-GameData.ps1 pipeline twice with the fixed package timestamp, requires byte-identical
output with the manually validated SHA-256, and then atomically places those bytes at
artifacts\poenhance-game-data.json. Normal application builds remain network-free.

.EXAMPLE
.\scripts\Setup-GameData.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$metadataPath = Join-Path $repoRoot 'data\game-data\sources.json'
$refreshScript = Join-Path $PSScriptRoot 'Refresh-GameData.ps1'
$activeArtifact = Join-Path $repoRoot 'artifacts\poenhance-game-data.json'
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$expectedPackageHash = $metadata.package.sha256.ToUpperInvariant()

$currentSourceRoot = Join-Path $env:TEMP 'PoEnhance-RePoE-UniqueStage-Current'
$historicalSourceRoot = Join-Path $env:TEMP 'PoEnhance-RePoE-UniqueStage-Historical'
$pobSourceRoot = Join-Path $env:TEMP 'PoEnhance-PoB-v2.67.2-b32759a'
$hostedExportRoot = Join-Path $env:TEMP 'PoEnhance-RePoE-Hosted-GameData'
# This legacy-named compatibility root is serialized in the manually validated package lineage.
# Retain it until a future stage deliberately changes the package identity.
$reproductionRoot = Join-Path $repoRoot 'artifacts\stage-e2-reproduction'
$currentDataRoot = Join-Path $reproductionRoot 'current-export-extract\data'
$historicalDataRoot = Join-Path $reproductionRoot 'historical-export-extract\data'
$firstBuildRoot = Join-Path $reproductionRoot 'build-1'
$secondBuildRoot = Join-Path $reproductionRoot 'build-2'

function Invoke-CheckedGit([string]$WorkingDirectory, [string[]]$Arguments)
{
    & git -C $WorkingDirectory @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "git failed in '$WorkingDirectory': git $($Arguments -join ' ')"
    }
}

function Assert-DirectChild([string]$Path, [string]$ExpectedParent)
{
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $fullParent = [System.IO.Path]::GetFullPath($ExpectedParent).TrimEnd('\')
    $actualParent = [System.IO.Path]::GetDirectoryName($fullPath).TrimEnd('\')
    if (-not $actualParent.Equals($fullParent, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Refusing to replace a path outside '$fullParent': $fullPath"
    }
}

function Reset-DirectChild([string]$Path, [string]$ExpectedParent)
{
    Assert-DirectChild $Path $ExpectedParent
    if (Test-Path -LiteralPath $Path)
    {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    [System.IO.Directory]::CreateDirectory($Path) | Out-Null
}

function Repair-GitRefsDirectory([string]$Checkout)
{
    $gitDirectory = Join-Path $Checkout '.git'
    if (Test-Path -LiteralPath $gitDirectory -PathType Container)
    {
        [System.IO.Directory]::CreateDirectory((Join-Path $gitDirectory 'refs\heads')) | Out-Null
        [System.IO.Directory]::CreateDirectory((Join-Path $gitDirectory 'refs\tags')) | Out-Null
    }
}

function Assert-GitCheckout(
    [string]$Checkout,
    [string]$RepositoryUri,
    [string]$CommitSha,
    [string]$Branch,
    [string]$Label)
{
    Repair-GitRefsDirectory $Checkout
    $actualHead = (& git -C $Checkout rev-parse HEAD 2>$null).Trim()
    $actualBranch = (& git -C $Checkout branch --show-current 2>$null).Trim()
    $actualRemote = (& git -C $Checkout remote get-url origin 2>$null).Trim().TrimEnd('/')
    if ($LASTEXITCODE -ne 0 -or
        -not $actualHead.Equals($CommitSha, [StringComparison]::OrdinalIgnoreCase) -or
        -not $actualBranch.Equals($Branch, [StringComparison]::Ordinal) -or
        -not $actualRemote.Equals($RepositoryUri.TrimEnd('/'), [StringComparison]::OrdinalIgnoreCase))
    {
        throw "$Label cache does not match its pin. Remove only '$Checkout' and rerun this setup command."
    }
}

function Ensure-GitCheckout(
    [string]$Checkout,
    [string]$RepositoryUri,
    [string]$CommitSha,
    [string]$Branch,
    [string]$Label)
{
    if (-not (Test-Path -LiteralPath $Checkout))
    {
        & git clone --filter=blob:none --no-checkout $RepositoryUri $Checkout
        if ($LASTEXITCODE -ne 0) { throw "Unable to clone $Label from $RepositoryUri." }
        Repair-GitRefsDirectory $Checkout
        Invoke-CheckedGit $Checkout @('checkout', '-B', $Branch, $CommitSha)
    }
    Assert-GitCheckout $Checkout $RepositoryUri $CommitSha $Branch $Label
}

function Ensure-PoBCheckout
{
    if (-not (Test-Path -LiteralPath $pobSourceRoot))
    {
        & git clone --filter=blob:none --branch $metadata.pathOfBuilding.tag --single-branch `
            $metadata.pathOfBuilding.repositoryUri $pobSourceRoot
        if ($LASTEXITCODE -ne 0) { throw 'Unable to clone the pinned Path of Building tag.' }
    }
    Repair-GitRefsDirectory $pobSourceRoot
    $head = (& git -C $pobSourceRoot rev-parse HEAD 2>$null).Trim()
    $tagCommit = (& git -C $pobSourceRoot rev-list -n 1 $metadata.pathOfBuilding.tag 2>$null).Trim()
    $remote = (& git -C $pobSourceRoot remote get-url origin 2>$null).Trim().TrimEnd('/')
    if ($LASTEXITCODE -ne 0 -or
        -not $head.Equals($metadata.pathOfBuilding.commitSha, [StringComparison]::OrdinalIgnoreCase) -or
        -not $tagCommit.Equals($metadata.pathOfBuilding.commitSha, [StringComparison]::OrdinalIgnoreCase) -or
        -not $remote.Equals($metadata.pathOfBuilding.repositoryUri.TrimEnd('/'), [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Path of Building cache does not match its pin. Remove only '$pobSourceRoot' and rerun this setup command."
    }

    $runtimeDirectory = Join-Path $pobSourceRoot 'runtime'
    $encodedRuntime = Join-Path $runtimeDirectory 'Path{space}of{space}Building.exe'
    if (-not (Test-Path -LiteralPath $encodedRuntime -PathType Leaf))
    {
        [System.IO.Directory]::CreateDirectory($runtimeDirectory) | Out-Null
        Expand-Archive -LiteralPath (Join-Path $pobSourceRoot 'runtime-win32.zip') `
            -DestinationPath $runtimeDirectory -Force
        $spacedRuntime = Join-Path $runtimeDirectory 'Path of Building.exe'
        if (-not (Test-Path -LiteralPath $spacedRuntime -PathType Leaf))
        {
            throw 'Pinned Path of Building runtime archive did not contain its executable.'
        }
        Move-Item -LiteralPath $spacedRuntime -Destination $encodedRuntime
    }
}

function Ensure-HostedExportCheckout
{
    if (-not (Test-Path -LiteralPath $hostedExportRoot))
    {
        & git clone --filter=blob:none --no-checkout `
            $metadata.currentRePoe.hostedExportRepositoryUri $hostedExportRoot
        if ($LASTEXITCODE -ne 0) { throw 'Unable to clone the pinned RePoE hosted-export repository.' }
    }
    Repair-GitRefsDirectory $hostedExportRoot
    $remote = (& git -C $hostedExportRoot remote get-url origin 2>$null).Trim().TrimEnd('/')
    if ($LASTEXITCODE -ne 0 -or
        -not $remote.Equals($metadata.currentRePoe.hostedExportRepositoryUri.TrimEnd('/'), [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Hosted-export cache has an unexpected origin. Remove only '$hostedExportRoot' and rerun."
    }
    foreach ($commit in @(
            $metadata.currentRePoe.hostedExportCommitSha,
            $metadata.historicalRePoe.hostedExportCommitSha))
    {
        & git -C $hostedExportRoot cat-file -e "$commit^{commit}" 2>$null
        if ($LASTEXITCODE -ne 0)
        {
            Invoke-CheckedGit $hostedExportRoot @('fetch', 'origin', $commit)
        }
    }
}

function Export-PinnedData(
    [string]$CommitSha,
    [object]$Files,
    [string]$DestinationRoot,
    [string]$Label)
{
    [System.IO.Directory]::CreateDirectory($DestinationRoot) | Out-Null
    $archivePath = Join-Path $reproductionRoot "$Label.zip"
    $extractRoot = Join-Path $reproductionRoot "$Label-extract"
    foreach ($path in @($archivePath, $extractRoot))
    {
        Assert-DirectChild $path $reproductionRoot
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
    $gitPaths = @($Files.PSObject.Properties.Name | ForEach-Object { "data/$_" })
    & git -c core.autocrlf=false -C $hostedExportRoot archive `
        --format=zip --output=$archivePath $CommitSha @gitPaths
    if ($LASTEXITCODE -ne 0) { throw "Unable to export pinned $Label data at $CommitSha." }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot
    foreach ($fileProperty in $Files.PSObject.Properties)
    {
        $sourcePath = Join-Path $extractRoot "data\$($fileProperty.Name)"
        $destinationPath = Join-Path $DestinationRoot $fileProperty.Name
        if (-not [System.IO.Path]::GetFullPath($sourcePath).Equals(
                [System.IO.Path]::GetFullPath($destinationPath),
                [StringComparison]::OrdinalIgnoreCase))
        {
            [System.IO.File]::Copy($sourcePath, $destinationPath, $true)
        }
        $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destinationPath).Hash
        if (-not $actualHash.Equals([string]$fileProperty.Value, [StringComparison]::OrdinalIgnoreCase))
        {
            throw "$Label input hash mismatch for $($fileProperty.Name). Expected $($fileProperty.Value); observed $actualHash."
        }
    }
}

function Invoke-ReproductionBuild([string]$OutputDirectory)
{
    & $refreshScript `
        -SourceRoot $currentSourceRoot `
        -SourceDataRoot $currentDataRoot `
        -SourceCommit $metadata.currentRePoe.commitSha `
        -DataVersion $metadata.package.dataVersion `
        -HistoricalSourceRoot $historicalSourceRoot `
        -HistoricalSourceDataRoot $historicalDataRoot `
        -HistoricalSourceCommit $metadata.historicalRePoe.commitSha `
        -HistoricalDataVersion $metadata.historicalRePoe.dataVersion `
        -HistoricalSourceBranch $metadata.historicalRePoe.branch `
        -PoBSourceRoot $pobSourceRoot `
        -PoBSourceCommit $metadata.pathOfBuilding.commitSha `
        -PoBSourceTag $metadata.pathOfBuilding.tag `
        -OutputDirectory $OutputDirectory `
        -CreatedAtUtc $metadata.package.createdAtUtc `
        -SkipCompatibilityTests
    if ($LASTEXITCODE -ne 0) { throw "GameData reproduction failed for $OutputDirectory." }
    $evaluatedUniquesPath = Join-Path $OutputDirectory 'pob-uniques.evaluated.json'
    $evaluatedUniquesHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $evaluatedUniquesPath).Hash
    if (-not $evaluatedUniquesHash.Equals(
            $metadata.pathOfBuilding.evaluatedUniquesSha256,
            [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Pinned PoB Unique evaluation hash mismatch: $evaluatedUniquesHash"
    }
    return Join-Path $OutputDirectory "poenhance-game-data-$($metadata.package.dataVersion)-candidate.json"
}

function Read-PackageContract([string]$PackagePath)
{
    $package = Get-Content -LiteralPath $PackagePath -Raw | ConvertFrom-Json
    $relationships = @($package.uniqueItems.foulbornModifierRelationships)
    return [pscustomobject]@{
        SchemaVersion = $package.manifest.schemaVersion
        DataVersion = $package.manifest.dataVersion
        CreatedAtUtc = $package.manifest.createdAtUtc
        RelationshipCount = $relationships.Count
        ExactRelationshipCount = @($relationships | Where-Object status -eq 'Exact').Count
        UnsupportedRelationshipCount = @($relationships | Where-Object status -eq 'Unsupported').Count
    }
}

function Assert-PackageContract([object]$Contract, [string]$Label)
{
    if ($Contract.SchemaVersion -ne $metadata.package.schemaVersion -or
        $Contract.DataVersion -ne $metadata.package.dataVersion -or
        $Contract.CreatedAtUtc -ne $metadata.package.createdAtUtc -or
        $Contract.RelationshipCount -ne $metadata.package.foulbornRelationshipCount -or
        $Contract.ExactRelationshipCount -ne $metadata.package.exactFoulbornRelationshipCount -or
        $Contract.UnsupportedRelationshipCount -ne $metadata.package.unsupportedFoulbornRelationshipCount)
    {
        throw "$Label metadata or Foulborn relationship counts do not match the pinned package contract."
    }
}

foreach ($command in @('git', 'dotnet'))
{
    if (-not (Get-Command $command -ErrorAction SilentlyContinue))
    {
        throw "Required command is unavailable: $command"
    }
}

Ensure-GitCheckout $currentSourceRoot $metadata.currentRePoe.repositoryUri `
    $metadata.currentRePoe.commitSha $metadata.currentRePoe.branch 'Current RePoE'
Ensure-GitCheckout $historicalSourceRoot $metadata.historicalRePoe.repositoryUri `
    $metadata.historicalRePoe.commitSha $metadata.historicalRePoe.branch 'Historical RePoE'
Ensure-PoBCheckout
Ensure-HostedExportCheckout

[System.IO.Directory]::CreateDirectory($reproductionRoot) | Out-Null
Export-PinnedData $metadata.currentRePoe.hostedExportCommitSha `
    $metadata.currentRePoe.files $currentDataRoot 'current-export'
Export-PinnedData $metadata.historicalRePoe.hostedExportCommitSha `
    $metadata.historicalRePoe.files $historicalDataRoot 'historical-export'

$foulbornMapPath = Join-Path $pobSourceRoot $metadata.pathOfBuilding.foulbornMapRelativePath
$foulbornMapHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $foulbornMapPath).Hash
if (-not $foulbornMapHash.Equals($metadata.pathOfBuilding.foulbornMapSha256, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Pinned ModFoulbornMap.jsonc hash mismatch: $foulbornMapHash"
}

if (-not $SkipRestore)
{
    & dotnet restore (Join-Path $repoRoot 'PoEnhance.slnx') --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
}

Reset-DirectChild $firstBuildRoot $reproductionRoot
Reset-DirectChild $secondBuildRoot $reproductionRoot
$firstPackage = Invoke-ReproductionBuild $firstBuildRoot
$firstHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $firstPackage).Hash
$firstSize = (Get-Item -LiteralPath $firstPackage).Length
if (-not $firstHash.Equals($expectedPackageHash, [StringComparison]::OrdinalIgnoreCase) -or
    $firstSize -ne [long]$metadata.package.sizeBytes)
{
    throw "Reproduced package differs from the validated package. Expected $expectedPackageHash / $($metadata.package.sizeBytes); observed $firstHash / $firstSize."
}
$firstContract = Read-PackageContract $firstPackage
Assert-PackageContract $firstContract 'First reproduced package'

# Retain the first package proof in memory and release its large disposable directory before
# producing build two. This keeps peak TEMP usage bounded while still proving two full builds.
Assert-DirectChild $firstBuildRoot $reproductionRoot
Remove-Item -LiteralPath $firstBuildRoot -Recurse -Force
Reset-DirectChild $secondBuildRoot $reproductionRoot
$secondPackage = Invoke-ReproductionBuild $secondBuildRoot
$secondHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $secondPackage).Hash
$secondSize = (Get-Item -LiteralPath $secondPackage).Length
if ($firstHash -ne $secondHash -or $firstSize -ne $secondSize)
{
    throw "GameData builds are not byte-identical: $firstHash / $secondHash."
}
$secondContract = Read-PackageContract $secondPackage
Assert-PackageContract $secondContract 'Second reproduced package'

$artifactsDirectory = [System.IO.Path]::GetDirectoryName($activeArtifact)
[System.IO.Directory]::CreateDirectory($artifactsDirectory) | Out-Null
$stagedActive = Join-Path $artifactsDirectory 'poenhance-game-data.stage-e4.tmp'
if (Test-Path -LiteralPath $stagedActive) { Remove-Item -LiteralPath $stagedActive }
[System.IO.File]::Copy($secondPackage, $stagedActive, $false)
Move-Item -LiteralPath $stagedActive -Destination $activeArtifact -Force
$activeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $activeArtifact).Hash
if (-not $activeHash.Equals($expectedPackageHash, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Activated GameData hash mismatch: $activeHash"
}

$verification = [ordered]@{
    schemaVersion = $secondContract.SchemaVersion
    dataVersion = $secondContract.DataVersion
    createdAtUtc = $secondContract.CreatedAtUtc
    sizeBytes = $firstSize
    sha256 = $firstHash.ToLowerInvariant()
    secondBuildSha256 = $secondHash.ToLowerInvariant()
    byteIdentical = $true
    foulbornRelationshipCount = $secondContract.RelationshipCount
    exactFoulbornRelationshipCount = $secondContract.ExactRelationshipCount
    unsupportedFoulbornRelationshipCount = $secondContract.UnsupportedRelationshipCount
    activeArtifact = $activeArtifact
    activeArtifactSha256 = $activeHash.ToLowerInvariant()
}
$verificationPath = Join-Path $reproductionRoot 'stage-e4-setup-verification.json'
[System.IO.File]::WriteAllText(
    $verificationPath,
    ($verification | ConvertTo-Json -Depth 5),
    [System.Text.UTF8Encoding]::new($false))

Write-Host "GameData:          $activeArtifact"
Write-Host "SHA-256:          $activeHash"
Write-Host "Determinism:      two byte-identical builds"
Write-Host "Verification:     $verificationPath"
