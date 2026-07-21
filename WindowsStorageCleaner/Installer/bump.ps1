param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = "$root\WindowsStorageCleaner\WindowsStorageCleaner.csproj"
$versionJson = "$root\WindowsStorageCleaner\version.json"
$productWxs = "$root\Installer\Product.wxs"

if (-not $Version) {
    $current = Get-Content $versionJson -Raw | ConvertFrom-Json
    $parts = $current.version -split '\.'
    $parts[3] = [int]$parts[3] + 1
    $Version = $parts -join '.'
}

Write-Output "=== Bumping to version $Version ==="

# Update version.json
$json = Get-Content $versionJson -Raw | ConvertFrom-Json
$json.version = $Version
$json.notes = ""
$json | ConvertTo-Json | Set-Content $versionJson -Encoding UTF8

# Update .csproj
(Get-Content $csproj) -replace '<Version>.*</Version>', "<Version>$Version</Version>" -replace '<FileVersion>.*</FileVersion>', "<FileVersion>$Version</FileVersion>" | Set-Content $csproj -Encoding UTF8

# Update Product.wxs
(Get-Content $productWxs) -replace 'Version="[^"]*"', "Version=`"$Version`"" | Set-Content $productWxs -Encoding UTF8

Write-Output "=== Files patched ==="
Write-Output "  $csproj"
Write-Output "  $versionJson"
Write-Output "  $productWxs"

git add -A
git commit -m "Bump version to $Version"
git tag "v$Version"
Write-Output "=== Committed and tagged v$Version ==="
Write-Output ""
Write-Output "To push: git push --atomic origin HEAD:master v$Version"
