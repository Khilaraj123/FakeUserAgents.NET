<#
.SYNOPSIS
    Dataset maintenance script for FakeUserAgents.NET.
.DESCRIPTION
    Validates dataset recency and checks market-share distribution across all embedded entries.
.EXAMPLE
    powershell -File ./scripts/refresh-dataset.ps1
    # or if pwsh is installed:
    pwsh ./scripts/refresh-dataset.ps1
#>

param(
    [int]$MinChromeMajor = 144
)

$PSScriptRootDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $PSScriptRootDirectory
$DataPath = [System.IO.Path]::Combine($RepoRoot, "src", "FakeUserAgents.NET", "Data", "useragents.json")

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " FakeUserAgents.NET - Dataset Validator" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

if (-not (Test-Path $DataPath)) {
    Write-Error "Dataset file not found at: $DataPath"
    exit 1
}

$rawJson = Get-Content $DataPath -Raw | ConvertFrom-Json
$count = $rawJson.Count
Write-Host "Loaded $count entries from $DataPath" -ForegroundColor Green

# 1. Chrome Recency Check
$chromeEntries = $rawJson | Where-Object { $_.browser -eq "Chrome" }
$maxChromeVersion = ($chromeEntries | ForEach-Object { [int]($_.browserVersion.Split('.')[0]) } | Measure-Object -Maximum).Maximum

Write-Host "`n[Recency Check]" -ForegroundColor Yellow
Write-Host "  Newest Chrome Version: $maxChromeVersion (Required: >= $MinChromeMajor)"

if ($maxChromeVersion -lt $MinChromeMajor) {
    Write-Error "Dataset is stale! Chrome version $maxChromeVersion is below requirement $MinChromeMajor."
    exit 1
} else {
    Write-Host "  Status: FRESH (Passed)" -ForegroundColor Green
}

# 2. Market Share Weight Summary
Write-Host "`n[Market Share Weights]" -ForegroundColor Yellow
$totalWeight = ($rawJson | Measure-Object -Property weight -Sum).Sum

$browsers = "Chrome", "Safari", "Edge", "Firefox", "Opera"
foreach ($b in $browsers) {
    $bWeight = ($rawJson | Where-Object { $_.browser -eq $b } | Measure-Object -Property weight -Sum).Sum
    $percentage = ($bWeight / $totalWeight) * 100.0
    Write-Host ("  {0,-10}: {1,6:N1}% (Weight: {2,5:N1})" -f $b, $percentage, $bWeight)
}

Write-Host "`nValidation complete." -ForegroundColor Cyan
