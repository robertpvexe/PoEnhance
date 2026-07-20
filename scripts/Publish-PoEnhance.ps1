[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Version,

    [Parameter(Mandatory)]
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^\d+\.\d+\.\d+$')
{
    throw "Version '$Version' is invalid. Use a three-part numeric version such as 0.1.0."
}

if (-not [System.IO.Path]::IsPathRooted($OutputRoot))
{
    throw 'OutputRoot must be an absolute path.'
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'PoEnhance.App\PoEnhance.App.csproj'
$gameDataPath = Join-Path $repoRoot 'artifacts\poenhance-game-data.json'
if (-not (Test-Path -LiteralPath $gameDataPath -PathType Leaf))
{
    throw "GameData package is missing: $gameDataPath"
}

$runningProcesses = @(
    Get-Process -Name 'PoEnhance', 'PoEnhance.App' -ErrorAction SilentlyContinue
)
if ($runningProcesses.Count -gt 0)
{
    $processSummary = $runningProcesses |
        ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }
    throw "PoEnhance is running: $($processSummary -join ', '). Exit PoEnhance from its tray menu, then rerun this script."
}

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetFullPath($OutputRoot)) | Out-Null
$resolvedOutputRoot = (Resolve-Path -LiteralPath $OutputRoot).Path.TrimEnd('\')
$archiveDirectoryName = "PoEnhance-v$Version-win-x64"
$targetDirectory = Join-Path $resolvedOutputRoot "v$Version"
$stagingRoot = Join-Path $resolvedOutputRoot ".$archiveDirectoryName-staging"
$publishDirectory = Join-Path $stagingRoot 'publish'
$archiveRoot = Join-Path $stagingRoot $archiveDirectoryName
$stagedZipPath = Join-Path $stagingRoot "$archiveDirectoryName.zip"
$zipPath = Join-Path $resolvedOutputRoot "$archiveDirectoryName.zip"
$checksumPath = "$zipPath.sha256"
$previousDirectory = Join-Path $stagingRoot 'previous-version'

function Assert-DirectChildPath([string]$Path, [string]$ExpectedParent)
{
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $fullParent = [System.IO.Path]::GetFullPath($ExpectedParent).TrimEnd('\')
    $actualParent = [System.IO.Path]::GetDirectoryName($fullPath).TrimEnd('\')
    if (-not $actualParent.Equals($fullParent, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "Refusing to operate outside the release root. Path: $fullPath"
    }

    if ($fullPath.Equals($fullParent, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "Refusing to operate on the release root itself: $fullParent"
    }
}

function Remove-ExactReleasePath([string]$Path, [string]$ExpectedParent)
{
    Assert-DirectChildPath -Path $Path -ExpectedParent $ExpectedParent
    if (Test-Path -LiteralPath $Path)
    {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

Assert-DirectChildPath -Path $targetDirectory -ExpectedParent $resolvedOutputRoot
Assert-DirectChildPath -Path $stagingRoot -ExpectedParent $resolvedOutputRoot
Assert-DirectChildPath -Path $zipPath -ExpectedParent $resolvedOutputRoot
Assert-DirectChildPath -Path $checksumPath -ExpectedParent $resolvedOutputRoot

Remove-ExactReleasePath -Path $stagingRoot -ExpectedParent $resolvedOutputRoot
[System.IO.Directory]::CreateDirectory($publishDirectory) | Out-Null

$succeeded = $false
try
{
    $fourPartVersion = "$Version.0"
    $publishArguments = @(
        'publish',
        $projectPath,
        '--configuration', 'Release',
        '--runtime', 'win-x64',
        '--self-contained', 'true',
        '--output', $publishDirectory,
        "-p:Version=$Version",
        "-p:AssemblyVersion=$fourPartVersion",
        "-p:FileVersion=$fourPartVersion",
        "-p:InformationalVersion=$Version",
        '-p:IncludeSourceRevisionInInformationalVersion=false',
        '-p:PublishTrimmed=false',
        '-p:PublishSingleFile=false',
        '-p:DebugType=None',
        '-p:DebugSymbols=false',
        '-p:Deterministic=true',
        '-p:ContinuousIntegrationBuild=true'
    )

    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $publishedAppHostPath = Join-Path $publishDirectory 'PoEnhance.App.exe'
    $releaseExecutablePath = Join-Path $publishDirectory 'PoEnhance.exe'
    if (-not (Test-Path -LiteralPath $publishedAppHostPath -PathType Leaf))
    {
        throw 'dotnet publish did not produce the PoEnhance application host.'
    }

    Move-Item -LiteralPath $publishedAppHostPath -Destination $releaseExecutablePath

    $requiredFiles = @(
        'PoEnhance.exe',
        'PoEnhance.App.dll',
        'PoEnhance.App.deps.json',
        'PoEnhance.App.runtimeconfig.json',
        'poenhance-game-data.json',
        'hostfxr.dll',
        'coreclr.dll',
        'System.Private.CoreLib.dll'
    )
    foreach ($requiredFile in $requiredFiles)
    {
        $requiredPath = Join-Path $publishDirectory $requiredFile
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf))
        {
            throw "Published release is missing required file: $requiredFile"
        }
    }

    $sourceGameDataHash = (Get-FileHash -LiteralPath $gameDataPath -Algorithm SHA256).Hash
    $publishedGameDataPath = Join-Path $publishDirectory 'poenhance-game-data.json'
    $publishedGameDataHash = (Get-FileHash -LiteralPath $publishedGameDataPath -Algorithm SHA256).Hash
    if ($sourceGameDataHash -ne $publishedGameDataHash)
    {
        throw 'Published GameData does not match the current repository GameData package.'
    }

    $executablePath = $releaseExecutablePath
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath)
    if ($versionInfo.FileVersion -ne $fourPartVersion -or
        $versionInfo.ProductVersion -ne $Version -or
        $versionInfo.ProductName -ne 'PoEnhance')
    {
        throw "Published executable version metadata is invalid. FileVersion=$($versionInfo.FileVersion); ProductVersion=$($versionInfo.ProductVersion); ProductName=$($versionInfo.ProductName)."
    }

    $forbiddenFiles = @(
        Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
            Where-Object {
                $_.Extension -in @('.cs', '.csproj', '.xaml', '.sln', '.slnx', '.pdb') -or
                $_.Name -match '(?i)(test|audit|source\.png|Publish-PoEnhance|Generate-PoEnhanceIcon)' -or
                $_.Name -in @('provisional-game-data.json', 'price-checker-placement.json')
            }
    )
    if ($forbiddenFiles.Count -gt 0)
    {
        throw "Published release contains forbidden files: $($forbiddenFiles.FullName -join ', ')"
    }

    Move-Item -LiteralPath $publishDirectory -Destination $archiveRoot
    Compress-Archive -LiteralPath $archiveRoot -DestinationPath $stagedZipPath -CompressionLevel Optimal

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($stagedZipPath)
    try
    {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $rootPrefix = "$archiveDirectoryName/"
        if ($entryNames.Count -eq 0 -or
            @($entryNames | Where-Object { -not $_.StartsWith($rootPrefix, [System.StringComparison]::Ordinal) }).Count -gt 0)
        {
            throw "ZIP entries do not use the required root directory '$archiveDirectoryName'."
        }

        foreach ($requiredEntry in 'PoEnhance.exe', 'poenhance-game-data.json')
        {
            if ($entryNames -notcontains "$rootPrefix$requiredEntry")
            {
                throw "ZIP is missing required entry: $rootPrefix$requiredEntry"
            }
        }

        if (@($entryNames | Where-Object {
                $_ -match '^[A-Za-z]:' -or
                $_.StartsWith('/') -or
                $_ -match '(?i)(/tests?/|/audits?/|\.cs$|\.csproj$|\.xaml$|\.slnx?$)'
            }).Count -gt 0)
        {
            throw 'ZIP contains an absolute, test, audit, or source-code path.'
        }
    }
    finally
    {
        $archive.Dispose()
    }

    if (Test-Path -LiteralPath $targetDirectory)
    {
        Move-Item -LiteralPath $targetDirectory -Destination $previousDirectory
    }

    Move-Item -LiteralPath $archiveRoot -Destination $targetDirectory
    Remove-ExactReleasePath -Path $zipPath -ExpectedParent $resolvedOutputRoot
    Remove-ExactReleasePath -Path $checksumPath -ExpectedParent $resolvedOutputRoot
    Move-Item -LiteralPath $stagedZipPath -Destination $zipPath

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$zipHash  $([System.IO.Path]::GetFileName($zipPath))" |
        Set-Content -LiteralPath $checksumPath -Encoding ascii -NoNewline

    $versionDirectoryBytes = (
        Get-ChildItem -LiteralPath $targetDirectory -File -Recurse |
            Measure-Object -Property Length -Sum
    ).Sum
    $zipBytes = (Get-Item -LiteralPath $zipPath).Length
    $checksumBytes = (Get-Item -LiteralPath $checksumPath).Length

    Write-Host "Release directory: $targetDirectory ($versionDirectoryBytes bytes)"
    Write-Host "Release ZIP:       $zipPath ($zipBytes bytes)"
    Write-Host "SHA-256 file:      $checksumPath ($checksumBytes bytes)"
    Write-Host "SHA-256:           $zipHash"
    $succeeded = $true
}
finally
{
    if (Test-Path -LiteralPath $stagingRoot)
    {
        Remove-ExactReleasePath -Path $stagingRoot -ExpectedParent $resolvedOutputRoot
    }
}

if (-not $succeeded)
{
    throw 'PoEnhance release publishing did not complete.'
}
