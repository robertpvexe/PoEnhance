[CmdletBinding()]
param(
    [switch]$Apply
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$knownDirectories = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -LiteralPath $repoRoot -Directory -Force |
    Where-Object { $_.Name -like 'PoEnhance.*' } |
    ForEach-Object {
        foreach ($name in 'bin', 'obj')
        {
            $candidate = Join-Path $_.FullName $name
            if (Test-Path -LiteralPath $candidate)
            {
                $knownDirectories.Add($candidate)
            }
        }
    }

foreach ($relativePath in 'TestResults', 'artifacts\audits', 'artifacts\source-audit')
{
    $candidate = Join-Path $repoRoot $relativePath
    if (Test-Path -LiteralPath $candidate)
    {
        $knownDirectories.Add($candidate)
    }
}

$rootPrefix = $repoRoot.TrimEnd('\') + '\'
$targets = $knownDirectories |
    ForEach-Object { (Resolve-Path -LiteralPath $_).Path } |
    Sort-Object -Unique |
    Where-Object {
        $_.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        $_ -notmatch '\\.git($|\\)' -and
        $_ -notmatch '\\local-data($|\\)' -and
        $_ -notmatch '\\artifacts\\poenhance-game-data\.json$'
    }

function Get-DirectorySize([string]$path)
{
    return (Get-ChildItem -LiteralPath $path -Force -File -Recurse -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
}

$reclaimableBytes = 0L
foreach ($target in $targets)
{
    $bytes = Get-DirectorySize $target
    $reclaimableBytes += $bytes
    Write-Host ("{0,-65} {1,10:N1} MiB" -f $target.Substring($rootPrefix.Length), ($bytes / 1MB))
}

Write-Host ("Known-safe reclaimable output: {0:N1} MiB" -f ($reclaimableBytes / 1MB))
if (-not $Apply)
{
    Write-Host 'Dry run only. Re-run with -Apply to remove the listed directories.'
    return
}

foreach ($target in $targets)
{
    Remove-Item -LiteralPath $target -Recurse -Force
}

Write-Host ("Reclaimed: {0:N1} MiB" -f ($reclaimableBytes / 1MB))
Write-Host 'Largest remaining top-level directories:'
Get-ChildItem -LiteralPath $repoRoot -Directory -Force |
    ForEach-Object {
        [pscustomobject]@{
            Name = $_.Name
            MiB = [math]::Round((Get-DirectorySize $_.FullName) / 1MB, 1)
        }
    } |
    Sort-Object MiB -Descending |
    Select-Object -First 10 |
    Format-Table -AutoSize
